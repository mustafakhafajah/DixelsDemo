import { useMemo, useState } from 'react'
import { errorText } from '../../api/client'
import { usePreviewMaintenance, useScheduleMaintenance } from '../../api/hooks'
import { ErrorLine, plural } from '../../components/bits'
import { Modal } from '../../components/Sheet'
import { addMin, dayKey, fromDateTime, hm, roundUp30 } from '../../lib/dateUtils'
import { generateOccurrences } from '../../lib/recurrence'
import { modals, type MaintenanceTarget } from '../../state/modalStore'
import { toast } from '../../state/toastStore'
import { defaultRecurrence, OccurrenceList, RecurrenceFields, toRule } from '../bookings/Recurrence'

export function MaintenanceFormModal({ target }: { target: MaintenanceTarget }) {
  const init = useMemo(() => roundUp30(new Date()), [])
  const [date, setDate] = useState(dayKey(init))
  const [startTime, setStartTime] = useState(hm(init))
  const [endTime, setEndTime] = useState(hm(addMin(init, 60)))
  const [recur, setRecur] = useState(() => defaultRecurrence(init, 5))
  const [skip, setSkip] = useState<Record<number, boolean>>({})
  const [note, setNote] = useState('')
  const schedule = useScheduleMaintenance()

  const start = fromDateTime(date, startTime)
  const end = fromDateTime(date, endTime)
  const bad = !start || !end || end <= start
  const startMs = start?.getTime()
  const endMs = end?.getTime()

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
  const totalAffected = rows.reduce((s, _r, i) => s + affectedAt(i), 0)

  const submit = () => {
    const wanted = generated.occurrences.filter((o) => !skip[o.start.getTime()])
    if (!wanted.length) { toast('warn', 'Nothing selected', 'Tick at least one occurrence.'); return }
    schedule.mutateAsync({
      scopeType: target.scopeType,
      scopeId: target.scopeId,
      note: note.trim() || undefined,
      occurrences: wanted.map((o) => ({ startUtc: o.start.toISOString(), endUtc: o.end.toISOString() })),
    }).then((r) => {
      toast('ok', `${plural(wanted.length, 'cleaning window')} scheduled`,
        `${target.label}${r.affectedBookingsCount ? ` · ${plural(r.affectedBookingsCount, 'existing booking')} overlap and are kept` : ''}.`)
      modals.close()
    }, (e) => { const { code, message } = errorText(e); toast('err', 'Nothing scheduled', message, code) })
  }

  return (
    <Modal title="Schedule cleaning" subtitle={`Cleaning for ${target.label}`} onClose={modals.close}
      footer={(
        <>
          <button type="button" className="btn" onClick={modals.close}>Discard</button>
          <button type="button" className="btn btn-primary" disabled={bad || schedule.isPending} onClick={submit}>Schedule cleaning</button>
        </>
      )}>
      <div>
        <label className="lbl" htmlFor="mt-date">Date</label>
        <input type="date" id="mt-date" className="inp mono" value={date} onChange={(e) => setDate(e.target.value)} />
      </div>
      <div style={{ display: 'grid', gridTemplateColumns: '1fr 1fr', gap: 12 }}>
        <div>
          <label className="lbl" htmlFor="mt-start" style={{ display: 'flex', justifyContent: 'space-between', alignItems: 'center' }}>Start <span className="utcchip">UTC +00:00</span></label>
          <input type="time" id="mt-start" className="inp mono" value={startTime} onChange={(e) => setStartTime(e.target.value)} />
        </div>
        <div>
          <label className="lbl" htmlFor="mt-end" style={{ display: 'flex', justifyContent: 'space-between', alignItems: 'center' }}>End <span className="utcchip">UTC +00:00</span></label>
          <input type="time" id="mt-end" className="inp mono" value={endTime} onChange={(e) => setEndTime(e.target.value)} />
        </div>
      </div>
      <ErrorLine error={bad ? { code: 'validation.end_before_start', message: 'End must be after start.' } : null} />
      <div>
        <RecurrenceFields idPrefix="mt" value={recur} onChange={(v) => { setRecur(v); setSkip({}) }} />
        {recur.repeat !== 'none' && rows.length > 0 && (
          <OccurrenceList rows={rows} truncated={generated.truncated} summaryAlert={totalAffected > 0}
            summary={totalAffected ? `${plural(totalAffected, 'existing booking')} overlap — kept, not cancelled` : 'No existing bookings overlap'}
            onToggle={(i) => setSkip({ ...skip, [rows[i].start.getTime()]: !rows[i].skip })} />
        )}
        {recur.repeat === 'none' && !bad && totalAffected > 0 && (
          <p style={{ fontSize: 12, color: 'var(--rust)', marginTop: 8 }}>{plural(totalAffected, 'existing booking')} overlap — kept, not cancelled.</p>
        )}
      </div>
      <div>
        <label className="lbl" htmlFor="mt-note">Note</label>
        <input id="mt-note" className="inp" placeholder="Cleaning" value={note} onChange={(e) => setNote(e.target.value)} />
      </div>
    </Modal>
  )
}
