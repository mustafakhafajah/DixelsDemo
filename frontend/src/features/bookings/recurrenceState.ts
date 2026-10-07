import i18n from 'i18next'
import { RECURRENCE_SAFETY_CAP } from '../../lib/constants'
import { addDays, dayAt, dayKey } from '../../lib/dateUtils'
import type { RecurrenceRule, RepeatFreq } from '../../lib/recurrence'
import type { FieldErrors } from '../../lib/useFieldErrors'

/* The repeat settings as the booking and maintenance forms hold them. */
export interface RecurrenceState {
  repeat: RepeatFreq
  interval: number
  byDay: number[]
  endMode: 'count' | 'until'
  count: number
  until: string
}

export function defaultRecurrence(start: Date, defaultCount: number): RecurrenceState {
  return {
    repeat: 'none',
    interval: 1,
    byDay: [start.getDay()],
    endMode: 'count',
    count: defaultCount,
    until: dayKey(addDays(start, 90)),
  }
}

export function toRule(r: RecurrenceState, start: Date): RecurrenceRule | null {
  if (r.repeat === 'none') return null
  return {
    freq: r.repeat,
    interval: Math.max(1, r.interval || 1),
    byDay: r.repeat === 'weekly' ? r.byDay : [],
    end: r.endMode === 'until'
      ? { mode: 'until', until: r.until ? dayAt(r.until, 23, 59) : dayAt(dayKey(addDays(start, 90)), 23, 59) }
      : { mode: 'count', count: Math.max(1, Math.min(RECURRENCE_SAFETY_CAP, r.count || 1)) },
  }
}

const bad = (message: string) => ({ code: 'validation.invalid_request', message })

/* What is wrong with the repeat settings, keyed by field id (<prefix>-interval, -days, -count, -until). */
export function recurrenceErrors(r: RecurrenceState, start: Date | null, idPrefix: string): FieldErrors {
  if (r.repeat === 'none') return {}
  const out: FieldErrors = {}
  if (!Number.isInteger(r.interval) || r.interval < 1 || r.interval > 52) out[`${idPrefix}-interval`] = bad(i18n.t('recurrence.intervalRange'))
  if (r.repeat === 'weekly' && !r.byDay.length) out[`${idPrefix}-days`] = bad(i18n.t('recurrence.pickDay'))
  if (r.endMode === 'count' && (!Number.isInteger(r.count) || r.count < 1 || r.count > RECURRENCE_SAFETY_CAP))
    out[`${idPrefix}-count`] = bad(i18n.t('recurrence.countRange', { max: RECURRENCE_SAFETY_CAP }))
  if (r.endMode === 'until') {
    if (!r.until) out[`${idPrefix}-until`] = bad(i18n.t('recurrence.untilMissing'))
    else if (start && r.until < dayKey(start)) out[`${idPrefix}-until`] = bad(i18n.t('recurrence.untilBeforeStart'))
  }
  return out
}
