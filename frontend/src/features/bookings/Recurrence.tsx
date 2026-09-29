import { useState, type ReactNode } from 'react'
import { ErrorLine } from '../../components/bits'
import { DatePicker, Dropdown } from '../../components/pickers'
import { WEEKDAYS } from '../../lib/closedDays'
import { RECURRENCE_SAFETY_CAP } from '../../lib/constants'
import { addDays, dayAt, dayKey, dayName, hm } from '../../lib/dateUtils'
import type { RecurrenceRule, RepeatFreq } from '../../lib/recurrence'
import type { FieldErrors } from '../../lib/useFieldErrors'

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
    byDay: [start.getUTCDay()],
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

const DAYS = WEEKDAYS
const bad = (message: string) => ({ code: 'validation.invalid_request', message })

/* What is wrong with the repeat settings, keyed by field id (<prefix>-interval, -days, -count, -until). */
export function recurrenceErrors(r: RecurrenceState, start: Date | null, idPrefix: string): FieldErrors {
  if (r.repeat === 'none') return {}
  const out: FieldErrors = {}
  if (!Number.isInteger(r.interval) || r.interval < 1 || r.interval > 52) out[`${idPrefix}-interval`] = bad('Repeat every 1 to 52.')
  if (r.repeat === 'weekly' && !r.byDay.length) out[`${idPrefix}-days`] = bad('Pick at least one day.')
  if (r.endMode === 'count' && (!Number.isInteger(r.count) || r.count < 1 || r.count > RECURRENCE_SAFETY_CAP))
    out[`${idPrefix}-count`] = bad(`Choose 1 to ${RECURRENCE_SAFETY_CAP} occurrences.`)
  if (r.endMode === 'until') {
    if (!r.until) out[`${idPrefix}-until`] = bad('Pick the last date.')
    else if (start && r.until < dayKey(start)) out[`${idPrefix}-until`] = bad('The last date must be on or after the first one.')
  }
  return out
}

export function RecurrenceFields({ value, onChange, idPrefix, errors = {} }: {
  value: RecurrenceState
  onChange: (v: RecurrenceState) => void
  idPrefix: string
  /* From recurrenceErrors: each message shows under its own field. */
  errors?: FieldErrors
}) {
  const set = (p: Partial<RecurrenceState>) => onChange({ ...value, ...p })
  const unit = value.repeat === 'daily' ? 'day(s)' : value.repeat === 'monthly' ? 'month(s)' : 'week(s)'
  const toggleDay = (d: number) => set({ byDay: value.byDay.includes(d) ? value.byDay.filter((x) => x !== d) : [...value.byDay, d] })

  return (
    <>
      <div style={{ display: 'flex', gap: 10, alignItems: 'flex-end', flexWrap: 'wrap' }}>
        <div style={{ flex: 1, minWidth: 150 }}>
          <label className="lbl" htmlFor={`${idPrefix}-repeat`}>Repeats</label>
          <Dropdown id={`${idPrefix}-repeat`} value={value.repeat} onChange={(v) => set({ repeat: v as RepeatFreq })}
            options={[
              { value: 'none', label: 'Does not repeat' },
              { value: 'daily', label: 'Daily' },
              { value: 'weekly', label: 'Weekly' },
              { value: 'monthly', label: 'Monthly' },
            ]} />
        </div>
        {value.repeat !== 'none' && (
          <div style={{ width: 150 }}>
            <label className="lbl" htmlFor={`${idPrefix}-interval`}>Every</label>
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
          <label className="lbl">On</label>
          <div style={{ display: 'flex', gap: 6, flexWrap: 'wrap', alignItems: 'center' }}>
            {DAYS.map(([d, label]) => (
              <button key={d} type="button" className={`dur-chip${value.byDay.includes(d) ? ' active' : ''}`} onClick={() => toggleDay(d)}>{label}</button>
            ))}
            <button type="button" className="btn btn-sm" style={{ marginLeft: 6 }} onClick={() => set({ byDay: [1, 2, 3, 4, 5] })}>Weekdays</button>
          </div>
          <ErrorLine id={`${idPrefix}-days-error`} error={errors[`${idPrefix}-days`]} />
        </div>
      )}

      {value.repeat !== 'none' && (
        <div style={{ marginTop: 10 }}>
          <label className="lbl">Ends</label>
          <div style={{ display: 'flex', gap: 10, alignItems: 'center', flexWrap: 'wrap' }}>
            <div className="seg">
              <button type="button" className={value.endMode === 'count' ? 'active' : ''} onClick={() => set({ endMode: 'count' })}>After</button>
              <button type="button" className={value.endMode === 'until' ? 'active' : ''} onClick={() => set({ endMode: 'until' })}>On date</button>
            </div>
            {value.endMode === 'count' ? (
              <div style={{ display: 'flex', gap: 6, alignItems: 'center' }}>
                <input type="number" id={`${idPrefix}-count`} className="inp mono" min={1} max={RECURRENCE_SAFETY_CAP} value={value.count} style={{ width: 70 }}
                  onChange={(e) => set({ count: Number(e.target.value) })} aria-label="Occurrences" />
                <span className="mono" style={{ fontSize: 12, color: 'var(--slate)' }}>occurrences</span>
              </div>
            ) : (
              <div style={{ width: 190 }}>
                <DatePicker id={`${idPrefix}-until`} value={value.until} onChange={(v) => set({ until: v })} aria-label="Until" />
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
  const [open, setOpen] = useState(false)
  const excluded = rows.filter((o) => o.skip).length
  return (
    <div style={{ border: '1px solid var(--line)', borderRadius: 8, overflow: 'hidden', marginTop: 12 }}>
      <button type="button" className="occ-head" aria-expanded={open} onClick={() => setOpen(!open)}
        style={{ width: '100%', padding: '9px 11px', background: 'var(--surface-2)', border: 'none', borderBottom: open ? '1px solid var(--line)' : 'none',
          display: 'flex', alignItems: 'center', gap: 8, cursor: 'pointer', fontFamily: 'inherit', textAlign: 'left' }}>
        <span aria-hidden style={{ display: 'inline-block', width: 10, fontSize: 10, color: 'var(--slate)', transform: open ? 'rotate(90deg)' : 'none', transition: 'transform .15s' }}>▶</span>
        <span style={{ fontSize: 12, fontWeight: 600, color: 'var(--ink)' }}>
          {rows.length} occurrences{excluded ? ` · ${excluded} excluded` : ''}
        </span>
        <span style={{ marginLeft: 'auto', fontSize: 11.5, color: summaryAlert ? 'var(--rust)' : 'var(--slate)' }}>{summary}</span>
        <span style={{ fontSize: 11.5, fontWeight: 600, color: 'var(--accent-dark)', whiteSpace: 'nowrap' }}>{open ? 'Hide' : 'Show'}</span>
      </button>
      {open && (
        <div style={{ maxHeight: 240, overflowY: 'auto' }}>
          {truncated && (
            <div style={{ padding: '7px 11px', fontSize: 11.5, color: 'var(--rust)', background: 'var(--rust-soft)', borderBottom: '1px solid var(--rust-line)' }}>
              Showing the first {RECURRENCE_SAFETY_CAP} occurrences — narrow the end date or occurrence count to see fewer.
            </div>
          )}
          {rows.map((o, i) => (
            <div key={o.start.getTime()} className="occ-row" onClick={() => onToggle(i)}
              style={{ display: 'flex', gap: 9, alignItems: 'center', padding: '7px 11px', borderBottom: '1px solid var(--line)', fontSize: 12.5, background: o.flagged ? 'var(--rust-soft)' : undefined }}>
              <span className={`occ-bar${o.skip ? '' : ' on'}`} />
              <span className="mono" style={{ fontSize: 12, ...(o.skip ? { color: 'var(--slate-2)', textDecoration: 'line-through' } : {}) }}>
                {dayKey(o.start)} {hm(o.start)}–{hm(o.end)}
              </span>
              <span style={{ color: 'var(--slate)', fontSize: 11.5 }}>{dayName(o.start).slice(0, 3)}</span>
              {o.note ? <span style={{ marginLeft: 'auto', fontSize: 11, color: 'var(--rust)', fontWeight: 600 }}>{o.note}</span>
                : o.skip ? <span style={{ marginLeft: 'auto', fontSize: 11, color: 'var(--slate-2)' }}>Excluded</span> : null}
            </div>
          ))}
        </div>
      )}
    </div>
  )
}
