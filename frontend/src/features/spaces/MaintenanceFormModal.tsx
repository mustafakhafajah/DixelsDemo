import { useMemo, useState } from 'react'
import { Trans, useTranslation } from 'react-i18next'
import { usePreviewMaintenance, useScheduleMaintenance } from '../../api/hooks'
import { ErrorLine, RequiredMark } from '../../components/bits'
import { CancelMessageFields, type CancelMessageValue } from '../../components/CancelMessageFields'
import { bulkCancelDefaults } from '../../components/cancelMessage'
import { DatePicker, Dropdown, TimePicker } from '../../components/pickers'
import { Modal } from '../../components/Sheet'
import { addMin, dayKey, earliestStart, fromDateTime, hm, roundUp30 } from '../../lib/dateUtils'
import { generateOccurrences } from '../../lib/recurrence'
import { useFieldErrors, type FieldErrors } from '../../lib/useFieldErrors'
import { modals, type MaintenanceTarget } from '../../state/modalStore'
import { toast } from '../../state/toastStore'
import { OccurrenceList, RecurrenceFields } from '../bookings/Recurrence'
import { defaultRecurrence, recurrenceErrors, toRule } from '../bookings/recurrenceState'

/* The reason is stored as the window's note (in the language in use) and shown on calendars instead of a generic label. */
const REASONS = ['cleaning', 'renovation', 'outOfService', 'privateEvent'] as const
const OTHER = 'other'
type Reason = (typeof REASONS)[number] | typeof OTHER
/* The server's field names for blocked-time errors -> this form's fields. */
const SERVER_FIELDS = { start: 'mt-start', end: 'mt-end' }

/* "Block time": stop new bookings in a space, floor or building for a period (an hour of cleaning or
 * weeks of renovation). Bookings already made there are kept unless the admin ticks "cancel them". */
export function MaintenanceFormModal({ target }: { target: MaintenanceTarget }) {
  const { t } = useTranslation()
  const init = useMemo(() => roundUp30(new Date()), [])
  /* Blocked time starts now at the earliest: past days and times are not offered. */
  const earliest = earliestStart()
  const [startDate, setStartDate] = useState(dayKey(init))
  const [startTime, setStartTime] = useState(hm(init))
  const [endDate, setEndDate] = useState(dayKey(init))
  const [endTime, setEndTime] = useState(hm(addMin(init, 60)))
  const [recur, setRecur] = useState(() => defaultRecurrence(init, 5))
  const [skip, setSkip] = useState<Record<number, boolean>>({})
  const [reason, setReason] = useState<Reason>(REASONS[0])
  const [otherReason, setOtherReason] = useState('')
  const [cancelAffected, setCancelAffected] = useState(false)
  /* The email to everyone on a cancelled booking; null until edited, so it follows the reason picked. */
  const [cancelMessage, setCancelMessage] = useState<CancelMessageValue | null>(null)
  const schedule = useScheduleMaintenance()

  const start = fromDateTime(startDate, startTime)
  const end = fromDateTime(endDate, endTime)
  const bad = !start || !end || end <= start
  const fields = useFieldErrors()
  const recurErrors = recurrenceErrors(recur, start, 'mt')
  /* Checked as the form is filled in, so these show straight away and keep the button off. */
  const liveErrors: FieldErrors = {
    ...(bad ? { 'mt-end': { code: 'validation.end_before_start', message: t('maintenance.endAfterStart') } } : {}),
    ...recurErrors,
  }
  const shown: FieldErrors = { ...liveErrors, ...fields.errors }
  const edited = () => fields.clear('mt-start', 'mt-end', 'mt-occurrences')
  const note = reason === OTHER ? otherReason.trim() : t(`maintenance.reasons.${reason}`)
  const shownMessage = cancelMessage ?? bulkCancelDefaults(target.label, { note })

  /* Read from the form's own values (dates and times), so it is only worked out again when those change. */
  const generated = useMemo(() => {
    const s = fromDateTime(startDate, startTime)
    const e = fromDateTime(endDate, endTime)
    if (!s || !e || e <= s) return { occurrences: [], truncated: false }
    const rule = toRule(recur, s)
    return rule ? generateOccurrences(s, e, rule) : { occurrences: [{ start: s, end: e }], truncated: false }
  }, [startDate, startTime, endDate, endTime, recur])

  const windows = generated.occurrences.map((o) => ({ startUtc: o.start.toISOString(), endUtc: o.end.toISOString() }))
  const preview = usePreviewMaintenance(bad ? null : { scopeType: target.scopeType, scopeId: target.scopeId, occurrences: windows })
  const affectedAt = (i: number) => preview.data?.perOccurrence[i]?.affectedCount ?? 0
  const rows = generated.occurrences.map((o, i) => {
    const n = affectedAt(i)
    return { start: o.start, end: o.end, skip: !!skip[o.start.getTime()], flagged: n > 0, note: n ? t('maintenance.affected', { count: n }) : undefined }
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
      'mt-other': reason === OTHER && !note ? { code: 'validation.missing_field', message: t('maintenance.otherMissing') } : undefined,
      'mt-occurrences': wanted.length ? undefined : { code: 'validation.missing_field', message: t('recurrence.tickOne') },
    }
    if (fields.show(found)) return
    schedule.mutateAsync({
      scopeType: target.scopeType,
      scopeId: target.scopeId,
      note,
      cancelAffectedBookings: cancelAffected,
      message: cancelAffected ? shownMessage : undefined,
      occurrences: wanted.map((o) => ({ startUtc: o.start.toISOString(), endUtc: o.end.toISOString() })),
    }).then((r) => {
      const message = r.cancelledBookingsCount
        ? t('maintenance.toast.withCancelled', { label: target.label, count: r.cancelledBookingsCount })
        : r.affectedBookingsCount ? t('maintenance.toast.withKept', { label: target.label, count: r.affectedBookingsCount })
          : t('maintenance.toast.plain', { label: target.label })
      toast('ok', t('maintenance.toast.blocked', { reason: note }), message)
      modals.close()
    }, (e) => fields.fromServer(e, SERVER_FIELDS, t('maintenance.toast.nothingBlocked')))
  }

  return (
    <Modal title={t('maintenance.title')} subtitle={t('maintenance.subtitle', { label: target.label })} onClose={modals.close}
      footer={(
        <>
          <button type="button" className="btn" onClick={modals.close}>{t('common.discard')}</button>
          <button type="button" className="btn btn-primary" disabled={Object.keys(liveErrors).length > 0 || schedule.isPending} onClick={submit}>{t('maintenance.title')}</button>
        </>
      )}>
      <p className="req-note"><RequiredMark /> {t('common.requiredField')}</p>
      <div className="form-cols" style={{ display: 'grid', gridTemplateColumns: '1fr 1fr', gap: 12 }}>
        <div>
          <label className="lbl req" htmlFor="mt-reason">{t('common.reason')}<RequiredMark /></label>
          <Dropdown id="mt-reason" value={reason} onChange={(v) => { setReason(v as Reason); fields.clear('mt-other') }}
            options={([...REASONS, OTHER] as Reason[]).map((r) => ({ value: r, label: t(`maintenance.reasons.${r}`) }))} />
        </div>
        {reason === OTHER && (
          <div>
            <label className="lbl req" htmlFor="mt-other">{t('maintenance.describe')}<RequiredMark /></label>
            <input id="mt-other" className="inp" dir="auto" maxLength={500} placeholder={t('maintenance.otherPlaceholder')} value={otherReason} onChange={(e) => { setOtherReason(e.target.value); fields.clear('mt-other') }} />
            <ErrorLine id="mt-other-error" error={shown['mt-other']} />
          </div>
        )}
      </div>
      <div className="form-cols" style={{ display: 'grid', gridTemplateColumns: '1fr 1fr', gap: 12 }}>
        <div>
          <label className="lbl req" htmlFor="mt-start-date">{t('common.from')}<RequiredMark /></label>
          <DatePicker id="mt-start-date" value={startDate} min={dayKey(earliest)} onChange={onStartDate} />
        </div>
        <div>
          <label className="lbl req" htmlFor="mt-start">{t('common.startTime')}<RequiredMark /></label>
          <TimePicker id="mt-start" aria-label={t('common.startTime')} value={startTime} min={startDate === dayKey(earliest) ? hm(earliest) : null} onChange={onStartTime} />
          <ErrorLine id="mt-start-error" error={shown['mt-start']} />
        </div>
      </div>
      <div className="form-cols" style={{ display: 'grid', gridTemplateColumns: '1fr 1fr', gap: 12 }}>
        <div>
          <label className="lbl req" htmlFor="mt-end-date">{t('common.until')}<RequiredMark /></label>
          <DatePicker id="mt-end-date" value={endDate} min={startDate} onChange={(v) => { setEndDate(v); edited() }} />
        </div>
        <div>
          <label className="lbl req" htmlFor="mt-end">{t('common.endTime')}<RequiredMark /></label>
          <TimePicker id="mt-end" aria-label={t('common.endTime')} value={endTime} min={start && endDate === startDate ? hm(addMin(start, 15)) : null} onChange={(v) => { setEndTime(v); edited() }} />
          <ErrorLine id="mt-end-error" error={shown['mt-end']} />
        </div>
      </div>
      <div>
        <RecurrenceFields idPrefix="mt" value={recur} errors={recurErrors} onChange={(v) => { setRecur(v); setSkip({}); fields.clear('mt-occurrences') }} />
        {recur.repeat !== 'none' && rows.length > 0 && (
          <OccurrenceList rows={rows} truncated={generated.truncated} summaryAlert={totalAffected > 0}
            summary={totalAffected ? t('maintenance.overlapCount', { count: totalAffected }) : t('maintenance.noOverlap')}
            onToggle={(i) => { setSkip({ ...skip, [rows[i].start.getTime()]: !rows[i].skip }); fields.clear('mt-occurrences') }} />
        )}
        <ErrorLine id="mt-occurrences-error" error={shown['mt-occurrences']} />
      </div>
      {!bad && totalAffected > 0 && (
        <div className="muted-box" style={{ borderColor: 'var(--rust-line)', background: 'var(--rust-soft)', color: 'var(--ink)' }}>
          <Trans i18nKey="maintenance.overlapKept" count={totalAffected} components={{ b: <strong /> }} />
          <label style={{ display: 'flex', gap: 8, alignItems: 'center', marginTop: 8, fontWeight: 600, cursor: 'pointer' }}>
            <input type="checkbox" checked={cancelAffected} onChange={(e) => setCancelAffected(e.target.checked)} />
            {t('maintenance.alsoCancel', { count: totalAffected })}
          </label>
          {cancelAffected && (
            <div style={{ marginTop: 10 }}>
              <CancelMessageFields idPrefix="mt-cancel" value={shownMessage} onChange={setCancelMessage} />
            </div>
          )}
        </div>
      )}
    </Modal>
  )
}
