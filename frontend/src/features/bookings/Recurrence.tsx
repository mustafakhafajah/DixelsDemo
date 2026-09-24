import type { ReactNode } from 'react'
import { RECURRENCE_SAFETY_CAP } from '../../lib/constants'
import { addDays, dayAt, dayKey, dayName, hm } from '../../lib/dateUtils'
import type { RecurrenceRule, RepeatFreq } from '../../lib/recurrence'

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

const DAYS: [number, string][] = [[1, 'Mon'], [2, 'Tue'], [3, 'Wed'], [4, 'Thu'], [5, 'Fri'], [6, 'Sat'], [0, 'Sun']]

export function RecurrenceFields({ value, onChange, idPrefix }: { value: RecurrenceState; onChange: (v: RecurrenceState) => void; idPrefix: string }) {
  const set = (p: Partial<RecurrenceState>) => onChange({ ...value, ...p })
  const unit = value.repeat === 'daily' ? 'day(s)' : value.repeat === 'monthly' ? 'month(s)' : 'week(s)'
  const toggleDay = (d: number) => set({ byDay: value.byDay.includes(d) ? value.byDay.filter((x) => x !== d) : [...value.byDay, d] })

  return (
    <>
      <div style={{ display: 'flex', gap: 10, alignItems: 'flex-end', flexWrap: 'wrap' }}>
        <div style={{ flex: 1, minWidth: 150 }}>
          <label className="lbl" htmlFor={`${idPrefix}-repeat`}>Repeats</label>
          <select id={`${idPrefix}-repeat`} className="inp" value={value.repeat} onChange={(e) => set({ repeat: e.target.value as RepeatFreq })}>
            <option value="none">Does not repeat</option>
            <option value="daily">Daily</option>
            <option value="weekly">Weekly</option>
            <option value="monthly">Monthly</option>
          </select>
        </div>
        {value.repeat !== 'none' && (
          <div style={{ width: 150 }}>
            <label className="lbl" htmlFor={`${idPrefix}-interval`}>Every</label>
            <div style={{ display: 'flex', gap: 6, alignItems: 'center' }}>
              <input type="number" id={`${idPrefix}-interval`} className="inp mono" min={1} max={52} value={value.interval}
                style={{ width: 64 }} onChange={(e) => set({ interval: Number(e.target.value) })} />
              <span className="mono" style={{ fontSize: 12, color: 'var(--slate)', whiteSpace: 'nowrap' }}>{unit}</span>
            </div>
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
                <input type="number" className="inp mono" min={1} max={200} value={value.count} style={{ width: 70 }}
                  onChange={(e) => set({ count: Number(e.target.value) })} aria-label="Occurrences" />
                <span className="mono" style={{ fontSize: 12, color: 'var(--slate)' }}>occurrences</span>
              </div>
            ) : (
              <div style={{ width: 170 }}>
                <input type="date" className="inp mono" value={value.until} onChange={(e) => set({ until: e.target.value })} aria-label="Until" />
              </div>
            )}
          </div>
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

export function OccurrenceList({ rows, summary, summaryAlert, truncated, onToggle }: {
  rows: OccurrenceRow[]
  summary: ReactNode
  summaryAlert: boolean
  truncated: boolean
  onToggle: (i: number) => void
}) {
  return (
    <div style={{ border: '1px solid var(--line)', borderRadius: 8, overflow: 'hidden', marginTop: 12, maxHeight: 280, overflowY: 'auto' }}>
      <div style={{ padding: '8px 11px', background: 'var(--surface-2)', borderBottom: '1px solid var(--line)', display: 'flex', justifyContent: 'space-between', gap: 8, position: 'sticky', top: 0 }}>
        <span style={{ fontSize: 12, fontWeight: 600 }}>{rows.length} occurrences</span>
        <span style={{ fontSize: 11.5, color: summaryAlert ? 'var(--rust)' : 'var(--slate)' }}>{summary}</span>
      </div>
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
  )
}
