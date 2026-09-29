import { useMemo, useState } from 'react'
import { usePreviewMaintenance, useScheduleMaintenance } from '../../api/hooks'
import { ErrorLine, plural, RequiredMark } from '../../components/bits'
import { DatePicker, Dropdown, TimePicker } from '../../components/pickers'
import { Modal } from '../../components/Sheet'
import { addMin, dayKey, earliestStart, fromDateTime, hm, roundUp30 } from '../../lib/dateUtils'
import { generateOccurrences } from '../../lib/recurrence'
import { useFieldErrors, type FieldErrors } from '../../lib/useFieldErrors'
import { modals, type MaintenanceTarget } from '../../state/modalStore'
import { toast } from '../../state/toastStore'
import { defaultRecurrence, OccurrenceList, recurrenceErrors, RecurrenceFields, toRule } from '../bookings/Recurrence'

/* The reason is stored as the window's note and shown on calendars instead of a generic label. */
const REASONS = ['Cleaning', 'Renovation', 'Out of service', 'Private event'] as const
const OTHER = 'Other'
/* The server's field names for blocked-time errors -> this form's fields. */
const SERVER_FIELDS = { start: 'mt-start', end: 'mt-end' }

/* "Block time": stop new bookings in a space, floor or building for a period (an hour of cleaning or
 * weeks of renovation). Bookings already made there are kept unless the admin ticks "cancel them". */
export function MaintenanceFormModal({ target }: { target: MaintenanceTarget }) {
  const init = useMemo(() => roundUp30(new Date()), [])
  /* Blocked time starts now at the earliest: past days and times are not offered. */
  const earliest = earliestStart()
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
  const fields = useFieldErrors()
  const recurErrors = recurrenceErrors(recur, start, 'mt')
  /* Checked as the form is filled in, so these show straight away and keep the button off. */
  const liveErrors: FieldErrors = {
    ...(bad ? { 'mt-end': { code: 'validation.end_before_start', message: 'The end must be after the start.' } } : {}),
    ...recurErrors,
  }
  const shown: FieldErrors = { ...liveErrors, ...fields.errors }
  const edited = () => fields.clear('mt-start', 'mt-end', 'mt-occurrences')
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
    edited()
    if (start && end && v) {
      const next = fromDateTime(v, startTime)
      if (next) { const moved = new Date(next.getTime() + (end.getTime() - start.getTime())); setEndDate(dayKey(moved)); setEndTime(hm(moved)) }
    }
    setStartDate(v)
    /* Moved onto today: a start that is now in the past moves up to now, keeping the length. */
    const s = fromDateTime(v, startTime)
    if (s && s < earliest) moveStart(earliest)
  }
  const moveStart = (next: Date) => {
    const len = start && end && end > start ? end.getTime() - start.getTime() : 3600000
    const moved = new Date(next.getTime() + len)
    setStartDate(dayKey(next)); setStartTime(hm(next)); setEndDate(dayKey(moved)); setEndTime(hm(moved))
  }
  const onStartTime = (v: string) => { edited(); const s = fromDateTime(startDate, v); if (s) moveStart(s); else setStartTime(v) }

  const submit = () => {
    const wanted = generated.occurrences.filter((o) => !skip[o.start.getTime()])
    const found: FieldErrors = {
      'mt-other': reason === OTHER && !note ? { code: 'validation.missing_field', message: 'Type the reason for blocking this time.' } : undefined,
      'mt-occurrences': wanted.length ? undefined : { code: 'validation.missing_field', message: 'Tick at least one occurrence.' },
    }
    if (fields.show(found)) return
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
    }, (e) => fields.fromServer(e, SERVER_FIELDS, 'Nothing blocked'))
  }

  return (
    <Modal title="Block time" subtitle={`No new bookings in ${target.label} during this time`} onClose={modals.close}
      footer={(
        <>
          <button type="button" className="btn" onClick={modals.close}>Discard</button>
          <button type="button" className="btn btn-primary" disabled={Object.keys(liveErrors).length > 0 || schedule.isPending} onClick={submit}>Block time</button>
        </>
      )}>
      <p className="req-note"><RequiredMark /> Required field</p>
      <div className="form-cols" style={{ display: 'grid', gridTemplateColumns: '1fr 1fr', gap: 12 }}>
        <div>
          <label className="lbl req" htmlFor="mt-reason">Reason<RequiredMark /></label>
          <Dropdown id="mt-reason" value={reason} onChange={(v) => { setReason(v); fields.clear('mt-other') }}
            options={[...REASONS, OTHER].map((r) => ({ value: r, label: r }))} />
        </div>
        {reason === OTHER && (
          <div>
            <label className="lbl req" htmlFor="mt-other">Describe it<RequiredMark /></label>
            <input id="mt-other" className="inp" maxLength={500} placeholder="e.g. Fire drill" value={otherReason} onChange={(e) => { setOtherReason(e.target.value); fields.clear('mt-other') }} />
            <ErrorLine id="mt-other-error" error={shown['mt-other']} />
          </div>
        )}
      </div>
      <div className="form-cols" style={{ display: 'grid', gridTemplateColumns: '1fr 1fr', gap: 12 }}>
        <div>
          <label className="lbl req" htmlFor="mt-start-date">From<RequiredMark /></label>
          <DatePicker id="mt-start-date" value={startDate} min={dayKey(earliest)} onChange={onStartDate} />
        </div>
        <div>
          <label className="lbl req" htmlFor="mt-start" style={{ display: 'flex', justifyContent: 'space-between', alignItems: 'center' }}>
            <span>Start time<RequiredMark /></span> <span className="utcchip">UTC +00:00</span>
          </label>
          <TimePicker id="mt-start" aria-label="Start time" value={startTime} min={startDate === dayKey(earliest) ? hm(earliest) : null} onChange={onStartTime} />
          <ErrorLine id="mt-start-error" error={shown['mt-start']} />
        </div>
      </div>
      <div className="form-cols" style={{ display: 'grid', gridTemplateColumns: '1fr 1fr', gap: 12 }}>
        <div>
          <label className="lbl req" htmlFor="mt-end-date">Until<RequiredMark /></label>
          <DatePicker id="mt-end-date" value={endDate} min={startDate} onChange={(v) => { setEndDate(v); edited() }} />
        </div>
        <div>
          <label className="lbl req" htmlFor="mt-end" style={{ display: 'flex', justifyContent: 'space-between', alignItems: 'center' }}>
            <span>End time<RequiredMark /></span> <span className="utcchip">UTC +00:00</span>
          </label>
          <TimePicker id="mt-end" aria-label="End time" value={endTime} min={start && endDate === startDate ? hm(addMin(start, 15)) : null} onChange={(v) => { setEndTime(v); edited() }} />
          <ErrorLine id="mt-end-error" error={shown['mt-end']} />
        </div>
      </div>
      <div>
        <RecurrenceFields idPrefix="mt" value={recur} errors={recurErrors} onChange={(v) => { setRecur(v); setSkip({}); fields.clear('mt-occurrences') }} />
        {recur.repeat !== 'none' && rows.length > 0 && (
          <OccurrenceList rows={rows} truncated={generated.truncated} summaryAlert={totalAffected > 0}
            summary={totalAffected ? `${plural(totalAffected, 'existing booking')} overlap` : 'No existing bookings overlap'}
            onToggle={(i) => { setSkip({ ...skip, [rows[i].start.getTime()]: !rows[i].skip }); fields.clear('mt-occurrences') }} />
        )}
        <ErrorLine id="mt-occurrences-error" error={shown['mt-occurrences']} />
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
