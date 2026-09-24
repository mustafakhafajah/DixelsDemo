import { useMemo, useState } from 'react'
import { errorText } from '../../api/client'
import { usePreviewMaintenance, useScheduleMaintenance } from '../../api/hooks'
import { ErrorLine, plural, RequiredMark } from '../../components/bits'
import { Modal } from '../../components/Sheet'
import { addMin, dayKey, fromDateTime, hm, roundUp30 } from '../../lib/dateUtils'
import { generateOccurrences } from '../../lib/recurrence'
import { modals, type MaintenanceTarget } from '../../state/modalStore'
import { toast } from '../../state/toastStore'
import { defaultRecurrence, OccurrenceList, RecurrenceFields, toRule } from '../bookings/Recurrence'

/* The reason is stored as the window's note and shown on calendars instead of a generic label. */
const REASONS = ['Cleaning', 'Renovation', 'Out of service', 'Private event'] as const
const OTHER = 'Other'

/* "Block time": stop new bookings in a space, floor or building for a period (an hour of cleaning or
 * weeks of renovation). Bookings already made there are kept unless the admin ticks "cancel them". */
export function MaintenanceFormModal({ target }: { target: MaintenanceTarget }) {
  const init = useMemo(() => roundUp30(new Date()), [])
  const [startDate, setStartDate] = useState(dayKey(init))
  const [startTime, setStartTime] = useState(hm(init))
  const [endDate, setEndDate] = useState(dayKey(init))
  const [endTime, setEndTime] = useState(hm(addMin(init, 60)))
  const [recur, setRecur] = useState(() => defaultRecurrence(init, 5))
  const [skip, setSkip] = useState<Record<number, boolean>>({})
  const [reason, setReason] = useState<string>(REASONS[0])
  const [otherReason, setOtherReason] = useState('')
  const [cancelAffected, setCancelAffected] = useState(false)
  const schedule = useScheduleMaintenance()

  const start = fromDateTime(startDate, startTime)
  const end = fromDateTime(endDate, endTime)
  const bad = !start || !end || end <= start
  const startMs = start?.getTime()
  const endMs = end?.getTime()
  const note = reason === OTHER ? otherReason.trim() : reason

  const generated = useMemo(() => {
    if (startMs === undefined || endMs === undefined || endMs <= startMs) return { occurrences: [], truncated: false }
    const s = new Date(startMs)
    const e = new Date(endMs)
    const rule = toRule(recur, s)
    return rule ? generateOccurrences(s, e, rule) : { occurrences: [{ start: s, end: e }], truncated: false }
  }, [startMs, endMs, recur])

  const windows = generated.occurrences.map((o) => ({ startUtc: o.start.toISOString(), endUtc: o.end.toISOString() }))
  const preview = usePreviewMaintenance(bad ? null : { scopeType: target.scopeType, scopeId: target.scopeId, occurrences: windows })
  const affectedAt = (i: number) => preview.data?.perOccurrence[i]?.affectedCount ?? 0
  const rows = generated.occurrences.map((o, i) => {
    const n = affectedAt(i)
    return { start: o.start, end: o.end, skip: !!skip[o.start.getTime()], flagged: n > 0, note: n ? `${plural(n, 'booking')} affected` : undefined }
  })
  const totalAffected = rows.reduce((s, _r, i) => s + (rows[i].skip ? 0 : affectedAt(i)), 0)

  /* Moving the start keeps the length: the end moves with it, as people expect. */
  const onStartDate = (v: string) => {
    if (start && end && v) {
      const next = fromDateTime(v, startTime)
      if (next) { const moved = new Date(next.getTime() + (end.getTime() - start.getTime())); setEndDate(dayKey(moved)); setEndTime(hm(moved)) }
    }
    setStartDate(v)
  }

  const submit = () => {
    if (reason === OTHER && !note) { toast('warn', 'Reason needed', 'Type the reason for blocking this time.'); return }
    const wanted = generated.occurrences.filter((o) => !skip[o.start.getTime()])
    if (!wanted.length) { toast('warn', 'Nothing selected', 'Tick at least one occurrence.'); return }
    schedule.mutateAsync({
      scopeType: target.scopeType,
      scopeId: target.scopeId,
      note,
      cancelAffectedBookings: cancelAffected,
      occurrences: wanted.map((o) => ({ startUtc: o.start.toISOString(), endUtc: o.end.toISOString() })),
    }).then((r) => {
      const bookings = r.cancelledBookingsCount
        ? ` · ${plural(r.cancelledBookingsCount, 'booking')} cancelled`
        : r.affectedBookingsCount ? ` · ${plural(r.affectedBookingsCount, 'existing booking')} kept` : ''
      toast('ok', `Time blocked (${note})`, `${target.label}${bookings}.`)
      modals.close()
    }, (e) => { const { code, message } = errorText(e); toast('err', 'Nothing blocked', message, code) })
  }

  return (
    <Modal title="Block time" subtitle={`No new bookings in ${target.label} during this time`} onClose={modals.close}
      footer={(
        <>
          <button type="button" className="btn" onClick={modals.close}>Discard</button>
          <button type="button" className="btn btn-primary" disabled={bad || schedule.isPending} onClick={submit}>Block time</button>
        </>
      )}>
      <p className="req-note"><RequiredMark /> Required field</p>
      <div style={{ display: 'grid', gridTemplateColumns: '1fr 1fr', gap: 12 }}>
        <div>
          <label className="lbl req" htmlFor="mt-reason">Reason<RequiredMark /></label>
          <select id="mt-reason" className="inp" value={reason} onChange={(e) => setReason(e.target.value)}>
            {[...REASONS, OTHER].map((r) => <option key={r} value={r}>{r}</option>)}
          </select>
        </div>
        {reason === OTHER && (
          <div>
            <label className="lbl req" htmlFor="mt-other">Describe it<RequiredMark /></label>
            <input id="mt-other" className="inp" maxLength={500} placeholder="e.g. Fire drill" value={otherReason} onChange={(e) => setOtherReason(e.target.value)} />
          </div>
        )}
      </div>
      <div style={{ display: 'grid', gridTemplateColumns: '1fr 1fr', gap: 12 }}>
        <div>
          <label className="lbl req" htmlFor="mt-start-date">From<RequiredMark /></label>
          <input type="date" id="mt-start-date" className="inp mono" required value={startDate} onChange={(e) => onStartDate(e.target.value)} />
        </div>
        <div>
          <label className="lbl req" htmlFor="mt-start" style={{ display: 'flex', justifyContent: 'space-between', alignItems: 'center' }}>
            <span>Start time<RequiredMark /></span> <span className="utcchip">UTC +00:00</span>
          </label>
          <input type="time" id="mt-start" className="inp mono" required value={startTime} onChange={(e) => setStartTime(e.target.value)} />
        </div>
      </div>
      <div style={{ display: 'grid', gridTemplateColumns: '1fr 1fr', gap: 12 }}>
        <div>
          <label className="lbl req" htmlFor="mt-end-date">Until<RequiredMark /></label>
          <input type="date" id="mt-end-date" className="inp mono" required value={endDate} min={startDate} onChange={(e) => setEndDate(e.target.value)} />
        </div>
        <div>
          <label className="lbl req" htmlFor="mt-end" style={{ display: 'flex', justifyContent: 'space-between', alignItems: 'center' }}>
            <span>End time<RequiredMark /></span> <span className="utcchip">UTC +00:00</span>
          </label>
          <input type="time" id="mt-end" className="inp mono" required value={endTime} onChange={(e) => setEndTime(e.target.value)} />
        </div>
      </div>
      <ErrorLine error={bad ? { code: 'validation.end_before_start', message: 'The end must be after the start.' } : null} />
      <div>
        <RecurrenceFields idPrefix="mt" value={recur} onChange={(v) => { setRecur(v); setSkip({}) }} />
        {recur.repeat !== 'none' && rows.length > 0 && (
          <OccurrenceList rows={rows} truncated={generated.truncated} summaryAlert={totalAffected > 0}
            summary={totalAffected ? `${plural(totalAffected, 'existing booking')} overlap` : 'No existing bookings overlap'}
            onToggle={(i) => setSkip({ ...skip, [rows[i].start.getTime()]: !rows[i].skip })} />
        )}
      </div>
      {!bad && totalAffected > 0 && (
        <div className="muted-box" style={{ borderColor: 'var(--rust-line)', background: 'var(--rust-soft)', color: 'var(--ink)' }}>
          <strong>{plural(totalAffected, 'existing booking')}</strong> overlap this time. They are kept unless you cancel them.
          <label style={{ display: 'flex', gap: 8, alignItems: 'center', marginTop: 8, fontWeight: 600, cursor: 'pointer' }}>
            <input type="checkbox" checked={cancelAffected} onChange={(e) => setCancelAffected(e.target.checked)} />
            Also cancel {totalAffected === 1 ? 'this booking' : `these ${totalAffected} bookings`} (ones already in progress are kept)
          </label>
        </div>
      )}
    </Modal>
  )
}
