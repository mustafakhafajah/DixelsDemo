import { useState, type ReactNode } from 'react'
import { useTranslation } from 'react-i18next'
import { ErrorLine } from '../../components/bits'
import { DatePicker, Dropdown } from '../../components/pickers'
import { WEEKDAYS } from '../../lib/closedDays'
import { RECURRENCE_SAFETY_CAP } from '../../lib/constants'
import { pressable } from '../../lib/pressable'
import { dayKey, formatDate, hm, weekdayName } from '../../lib/dateUtils'
import type { RepeatFreq } from '../../lib/recurrence'
import type { FieldErrors } from '../../lib/useFieldErrors'
import type { RecurrenceState } from './recurrenceState'

export function RecurrenceFields({ value, onChange, idPrefix, errors = {} }: {
  value: RecurrenceState
  onChange: (v: RecurrenceState) => void
  idPrefix: string
  /* From recurrenceErrors: each message shows under its own field. */
  errors?: FieldErrors
}) {
  const { t } = useTranslation()
  const set = (p: Partial<RecurrenceState>) => onChange({ ...value, ...p })
  const unit = value.repeat === 'daily' ? t('recurrence.unit.day', { count: value.interval })
    : value.repeat === 'monthly' ? t('recurrence.unit.month', { count: value.interval }) : t('recurrence.unit.week', { count: value.interval })
  const toggleDay = (d: number) => set({ byDay: value.byDay.includes(d) ? value.byDay.filter((x) => x !== d) : [...value.byDay, d] })

  return (
    <>
      <div style={{ display: 'flex', gap: 10, alignItems: 'flex-end', flexWrap: 'wrap' }}>
        <div style={{ flex: 1, minWidth: 150 }}>
          <label className="lbl" htmlFor={`${idPrefix}-repeat`}>{t('recurrence.repeats')}</label>
          <Dropdown id={`${idPrefix}-repeat`} value={value.repeat} onChange={(v) => set({ repeat: v as RepeatFreq })}
            options={[
              { value: 'none', label: t('recurrence.none') },
              { value: 'daily', label: t('recurrence.daily') },
              { value: 'weekly', label: t('recurrence.weekly') },
              { value: 'monthly', label: t('recurrence.monthly') },
            ]} />
        </div>
        {value.repeat !== 'none' && (
          <div style={{ width: 150 }}>
            <label className="lbl" htmlFor={`${idPrefix}-interval`}>{t('recurrence.every')}</label>
            <div style={{ display: 'flex', gap: 6, alignItems: 'center' }}>
              <input type="number" id={`${idPrefix}-interval`} className="inp mono" min={1} max={52} value={value.interval}
                style={{ width: 64 }} onChange={(e) => set({ interval: Number(e.target.value) })} />
              <span className="mono" style={{ fontSize: 12, color: 'var(--slate)', whiteSpace: 'nowrap' }}>{unit}</span>
            </div>
            <ErrorLine id={`${idPrefix}-interval-error`} error={errors[`${idPrefix}-interval`]} />
          </div>
        )}
      </div>

      {value.repeat === 'weekly' && (
        <div style={{ marginTop: 10 }}>
          <label className="lbl">{t('recurrence.on')}</label>
          <div style={{ display: 'flex', gap: 6, flexWrap: 'wrap', alignItems: 'center' }}>
            {WEEKDAYS.map((d) => (
              <button key={d} type="button" className={`dur-chip${value.byDay.includes(d) ? ' active' : ''}`} onClick={() => toggleDay(d)}>{weekdayName(d, 'short')}</button>
            ))}
            <button type="button" className="btn btn-sm" style={{ marginInlineStart: 6 }} onClick={() => set({ byDay: [1, 2, 3, 4, 5] })}>{t('recurrence.weekdays')}</button>
          </div>
          <ErrorLine id={`${idPrefix}-days-error`} error={errors[`${idPrefix}-days`]} />
        </div>
      )}

      {value.repeat !== 'none' && (
        <div style={{ marginTop: 10 }}>
          <label className="lbl">{t('recurrence.ends')}</label>
          <div style={{ display: 'flex', gap: 10, alignItems: 'center', flexWrap: 'wrap' }}>
            <div className="seg">
              <button type="button" className={value.endMode === 'count' ? 'active' : ''} onClick={() => set({ endMode: 'count' })}>{t('recurrence.after')}</button>
              <button type="button" className={value.endMode === 'until' ? 'active' : ''} onClick={() => set({ endMode: 'until' })}>{t('recurrence.onDate')}</button>
            </div>
            {value.endMode === 'count' ? (
              <div style={{ display: 'flex', gap: 6, alignItems: 'center' }}>
                <input type="number" id={`${idPrefix}-count`} className="inp mono" min={1} max={RECURRENCE_SAFETY_CAP} value={value.count} style={{ width: 70 }}
                  onChange={(e) => set({ count: Number(e.target.value) })} aria-label={t('recurrence.occurrencesLabel')} />
                <span className="mono" style={{ fontSize: 12, color: 'var(--slate)' }}>{t('recurrence.occurrencesUnit', { count: value.count })}</span>
              </div>
            ) : (
              <div style={{ width: 190 }}>
                <DatePicker id={`${idPrefix}-until`} value={value.until} onChange={(v) => set({ until: v })} aria-label={t('common.until')} />
              </div>
            )}
          </div>
          <ErrorLine id={`${idPrefix}-count-error`} error={errors[`${idPrefix}-count`]} />
          <ErrorLine id={`${idPrefix}-until-error`} error={errors[`${idPrefix}-until`]} />
        </div>
      )}
    </>
  )
}

export interface OccurrenceRow {
  start: Date
  end: Date
  skip: boolean
  flagged: boolean
  note?: ReactNode
}

/* Collapsed by default: a long series shows one summary row instead of pushing the form down forever.
 * Click the header to review (and exclude) individual occurrences. */
export function OccurrenceList({ rows, summary, summaryAlert, truncated, onToggle }: {
  rows: OccurrenceRow[]
  summary: ReactNode
  summaryAlert: boolean
  truncated: boolean
  onToggle: (i: number) => void
}) {
  const { t, i18n } = useTranslation()
  const [open, setOpen] = useState(false)
  const excluded = rows.filter((o) => o.skip).length
  /* The closed triangle points along the reading direction; open, it points down. */
  const rtl = i18n.dir() === 'rtl'
  return (
    <div style={{ border: '1px solid var(--line)', borderRadius: 8, overflow: 'hidden', marginTop: 12 }}>
      <button type="button" className="occ-head" aria-expanded={open} onClick={() => setOpen(!open)}
        style={{ width: '100%', padding: '9px 11px', background: 'var(--surface-2)', border: 'none', borderBottom: open ? '1px solid var(--line)' : 'none',
          display: 'flex', alignItems: 'center', gap: 8, cursor: 'pointer', fontFamily: 'inherit', textAlign: 'start' }}>
        <span aria-hidden style={{ display: 'inline-block', width: 10, fontSize: 10, color: 'var(--slate)', transform: open ? `rotate(${rtl ? -90 : 90}deg)` : 'none', transition: 'transform .15s' }}>{rtl ? '◀' : '▶'}</span>
        <span style={{ fontSize: 12, fontWeight: 600, color: 'var(--ink)' }}>
          {t('recurrence.occurrences', { count: rows.length })}{excluded ? ` · ${t('recurrence.excludedCount', { count: excluded })}` : ''}
        </span>
        <span style={{ marginInlineStart: 'auto', fontSize: 11.5, color: summaryAlert ? 'var(--rust)' : 'var(--slate)' }}>{summary}</span>
        <span style={{ fontSize: 11.5, fontWeight: 600, color: 'var(--accent-dark)', whiteSpace: 'nowrap' }}>{open ? t('common.hide') : t('common.show')}</span>
      </button>
      {open && (
        <div style={{ maxHeight: 240, overflowY: 'auto' }}>
          {truncated && (
            <div style={{ padding: '7px 11px', fontSize: 11.5, color: 'var(--rust)', background: 'var(--rust-soft)', borderBottom: '1px solid var(--rust-line)' }}>
              {t('recurrence.truncated', { max: RECURRENCE_SAFETY_CAP })}
            </div>
          )}
          {rows.map((o, i) => (
            <div key={o.start.getTime()} className="occ-row" onClick={() => onToggle(i)}
              {...pressable(() => onToggle(i))} role="checkbox" aria-checked={!o.skip}
              style={{ display: 'flex', gap: 9, alignItems: 'center', padding: '7px 11px', borderBottom: '1px solid var(--line)', fontSize: 12.5, background: o.flagged ? 'var(--rust-soft)' : undefined }}>
              <span className={`occ-bar${o.skip ? '' : ' on'}`} />
              <span className="mono" style={{ fontSize: 12, ...(o.skip ? { color: 'var(--slate-2)', textDecoration: 'line-through' } : {}) }}>
                {dayKey(o.start)} {hm(o.start)}–{hm(o.end)}
              </span>
              <span style={{ color: 'var(--slate)', fontSize: 11.5 }}>{formatDate(o.start, { weekday: 'short' })}</span>
              {o.note ? <span style={{ marginInlineStart: 'auto', fontSize: 11, color: 'var(--rust)', fontWeight: 600 }}>{o.note}</span>
                : o.skip ? <span style={{ marginInlineStart: 'auto', fontSize: 11, color: 'var(--slate-2)' }}>{t('recurrence.excluded')}</span> : null}
            </div>
          ))}
        </div>
      )}
    </div>
  )
}
