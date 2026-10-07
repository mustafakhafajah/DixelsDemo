import { useMemo, useState } from 'react'
import { useTranslation } from 'react-i18next'
import i18n from 'i18next'
import { ApiError, errorText } from '../../api/client'
import { useAvailability, useCreateBooking, useCreateBookingSeries, useRescheduleBooking, useSetAttendees, useSpaces } from '../../api/hooks'
import type { Booking, Space } from '../../api/types'
import { useSession } from '../../app/session'
import { P } from '../../auth/permissions'
import { ErrorLine, RequiredMark } from '../../components/bits'
import { DatePicker, Dropdown, TimePicker } from '../../components/pickers'
import { Modal } from '../../components/Sheet'
import { closedReason, isUtcLike, localLabel, openParts, zoneCity } from '../../lib/closedDays'
import { addDays, addMin, dayAt, dayKey, earliestStart, fromDateTime, hm, keepWindowAhead, roundUp30, stampOffset } from '../../lib/dateUtils'
import { findOverlap, validateWindowLocal } from '../../lib/laneLayout'
import { generateOccurrences } from '../../lib/recurrence'
import { useFieldErrors, type FieldError, type FieldErrors } from '../../lib/useFieldErrors'
import { modals, type BookingPrefill } from '../../state/modalStore'
import { toast } from '../../state/toastStore'
import { AttendeesField } from './AttendeesField'
import { inviteesOf, samePeople, toAttendeeInput, type Invitee } from './invitees'
import { OccurrenceList, RecurrenceFields, type OccurrenceRow } from './Recurrence'
import { defaultRecurrence, recurrenceErrors, toRule } from './recurrenceState'

const newKey = () => `idem-${crypto.randomUUID()}`

/* One option per distinct key, sorted by label (e.g. the buildings or floors that have bookable spaces). */
function uniqueBy(spaces: Space[], key: (s: Space) => string, label: (s: Space) => string) {
  const m = new Map(spaces.map((s) => [key(s), label(s)]))
  return [...m].map(([value, l]) => ({ value, label: l })).sort((a, b) => a.label.localeCompare(b.label, undefined, { numeric: true }))
}

const missing = (message: string): FieldError => ({ code: 'validation.missing_field', message })

/* Which field a window problem belongs to: a closed day is about the date, a start in the past about the
 * start, an end before the start about the end; hours and booking length are about the two together. */
const TIME_FIELD: Record<string, string> = {
  'validation.holiday_closed': 'm-date',
  'validation.missing_field': 'm-start',
  'validation.start_in_past': 'm-start',
  'validation.end_before_start': 'm-end',
}
/* The server's field names for booking errors -> this form's fields. */
const SERVER_FIELDS = { date: 'm-date', start: 'm-start', end: 'm-end', window: 'm-window', spaceId: 'm-space', attendees: 'm-attendees' }
const FIELD_IDS = ['m-date', 'm-start', 'm-end', 'm-window', 'm-building', 'm-floor', 'm-space', 'm-occurrences']

/* The server says exactly which level blocks it (space, floor or building). */
function accessError(space: Space) {
  if (space.canCurrentUserBook) return null
  return { code: 'space.not_bookable', message: space.notBookableReason ?? i18n.t('booking.notBookable', { name: space.name }) }
}

/* Why a space that was picked is no longer free for the window, shown after its name in the list. */
type KeptReason = '' | 'booked' | 'closed' | 'hours'

export function BookingModal({ prefill, editing }: { prefill: BookingPrefill; editing: Booking | null }) {
  const { t } = useTranslation()
  const session = useSession()
  /* See who booked what (ViewAll) / may hold several spaces at once (MultipleSpaces). */
  const seesAll = session.can(P.Bookings.ViewAll)
  const multipleSpaces = session.can(P.Bookings.MultipleSpaces)
  const spacesQ = useSpaces()
  /* A new booking never starts in the past: a prefill from an earlier slot is moved up to now. */
  const earliest = earliestStart()
  const wantedStart = prefill.start ?? roundUp30(new Date())
  const wantedEnd = prefill.end ?? addMin(wantedStart, 60)
  const initStart = !editing && wantedStart < earliest ? earliest : wantedStart
  const initEnd = new Date(initStart.getTime() + (wantedEnd.getTime() - wantedStart.getTime()))

  const [date, setDate] = useState(dayKey(initStart))
  const [startTime, setStartTime] = useState(hm(initStart))
  const [endTime, setEndTime] = useState(hm(initEnd))
  const [chosenSpaceId, setChosenSpaceId] = useState(editing?.spaceId ?? prefill.spaceId ?? '')
  /* null = not touched yet, so a prefilled space can fill in its building and floor once spaces load. */
  const [chosenBuildingId, setChosenBuildingId] = useState<string | null>(null)
  const [chosenFloorId, setChosenFloorId] = useState<string | null>(null)
  const [recur, setRecur] = useState(() => defaultRecurrence(initStart, 4))
  const [skipOverrides, setSkipOverrides] = useState<Record<number, boolean>>({})
  const [idempotencyKey] = useState(newKey)
  const [conflict, setConflict] = useState<{ message: string; suggested: { start: Date; end: Date } | null } | null>(null)
  const [invitees, setInvitees] = useState<Invitee[]>(() => (editing ? inviteesOf(editing) : []))
  /* Guests' addresses only reach the owner and admins; without them the list can't be edited safely. */
  const guestsHidden = !!editing && editing.guestCount > editing.guests.length

  const start = fromDateTime(date, startTime)
  const end = fromDateTime(date, endTime)

  /* Past times are not offered at all: on the first bookable day the start begins at "now",
   * and the end always begins one step after the start. */
  const startMin = date === dayKey(earliest) ? hm(earliest) : null
  const endMin = start ? hm(addMin(start, 15)) : null
  const applyWindow = (day: string, s: string, e: string) => {
    const next = keepWindowAhead(day, s, e, earliest)
    setStartTime(next.startTime); setEndTime(next.endTime)
  }
  const onDate = (v: string) => { setDate(v); applyWindow(v, startTime, endTime); edited() }
  /* Moving the start keeps the booking's length, so the end never lands before it. */
  const onStart = (v: string) => {
    const s = fromDateTime(date, v)
    const len = start && end && end > start ? end.getTime() - start.getTime() : 3600000
    edited()
    if (!s) return setStartTime(v)
    const e = new Date(s.getTime() + len)
    setStartTime(v); setEndTime(dayKey(e) === date ? hm(e) : '23:45')
  }
  const hasWindow = !!start && !!end && end > start

  const rule = !editing && hasWindow ? toRule(recur, start) : null
  /* The memos below read the window from the form's own values (date and times), so they are only worked out
   * again when those change, not on every render. */
  const generated = useMemo(() => {
    const s = fromDateTime(date, startTime)
    const e = fromDateTime(date, endTime)
    if (editing || !s || !e || e <= s) return null
    const r = toRule(recur, s)
    return r ? generateOccurrences(s, e, r) : null
  }, [editing, date, startTime, endTime, recur])

  const rangeFrom = start ? dayAt(dayKey(start)) : undefined
  const lastOcc = generated?.occurrences.at(-1)?.end ?? end
  const rangeTo = lastOcc ? addDays(dayAt(dayKey(lastOcc)), 1) : undefined
  const bookingsQ = useAvailability({ from: rangeFrom, to: rangeTo }, { seesAll, userId: session.userId }, hasWindow)
  const bookings = useMemo(() => bookingsQ.data ?? [], [bookingsQ.data])

  const spaces = useMemo(() => spacesQ.data ?? [], [spacesQ.data])
  const eligible = useMemo(() => spaces.filter((s) => s.canCurrentUserBook), [spaces])

  /* Building -> floor -> space: each picker stays locked until the one above it is chosen. */
  const seed = spaces.find((s) => s.id === (editing?.spaceId ?? prefill.spaceId))
  const buildingId = chosenBuildingId ?? seed?.buildingId ?? ''
  const floorId = chosenFloorId ?? (seed && seed.buildingId === buildingId ? seed.floorId : '')
  const buildingOptions = uniqueBy(eligible, (s) => s.buildingId, (s) => s.buildingName)
  const floorOptions = buildingId ? uniqueBy(eligible.filter((s) => s.buildingId === buildingId), (s) => s.floorId, (s) => t('common.floorName', { name: s.floorName })) : []

  /* Only spaces that are eligible AND free for the entered window; the previously chosen one is
   * kept (annotated) rather than vanishing mid-edit, as in the mock. */
  const options = useMemo(() => {
    if (editing) {
      const s = spaces.find((x) => x.id === editing.spaceId)
      return s ? [{ space: s, reason: '' as KeptReason }] : []
    }
    const start = fromDateTime(date, startTime)
    const end = fromDateTime(date, endTime)
    const hasWindow = !!start && !!end && end > start
    const free = (s: Space) => !hasWindow ||
      (!findOverlap(bookings, s.id, start!, end!) && !validateWindowLocal(s.constraints, s.name, start, end, true))
    if (!floorId) return []
    const onFloor = eligible.filter((s) => s.floorId === floorId)
    const list = onFloor.filter(free).map((space) => ({ space, reason: '' as KeptReason }))
    const kept = onFloor.find((s) => s.id === chosenSpaceId)
    if (kept && !list.some((o) => o.space.id === kept.id)) {
      list.unshift({ space: kept, reason: findOverlap(bookings, kept.id, start!, end!) ? 'booked'
        : closedReason(kept.constraints, start!, end!) ? 'closed' : 'hours' })
    }
    return list
  }, [editing, spaces, eligible, floorId, bookings, date, startTime, endTime, chosenSpaceId])

  const spaceId = options.some((o) => o.space.id === chosenSpaceId) ? chosenSpaceId : ''
  const space = spaces.find((s) => s.id === spaceId) ?? null
  const c = space?.constraints
  /* Times are picked in UTC. Once a space is chosen, only times inside its building's opening hours (on the
   * building's clock) are offered, and each pick also shows that clock when it reads differently from UTC.
   * On a closed day nothing is open; the date's error explains it, so the lists stay whole. */
  const openToday = c ? openParts(c, date) : []
  const startAllowed = openToday.length ? (m: number) => openToday.some((p) => p.start <= m && m < p.end) : undefined
  const endAllowed = openToday.length ? (m: number) => openToday.some((p) => p.start < m && m <= p.end) : undefined
  const localHint = (d: Date | null) => c && d && !isUtcLike(c.timeZone, d)
    ? <p className="card-sub" style={{ margin: '4px 0 0' }}>{t('booking.localTime', { time: localLabel(d, c.timeZone), zone: zoneCity(c.timeZone) })}</p>
    : null
  const timeError = validateWindowLocal(c ?? null, space?.name ?? '', start, end)
  const spaceError = !editing && space ? accessError(space) : null
  const recurErrors = !editing ? recurrenceErrors(recur, start, 'm') : {}
  /* The room's seats (0 = not set); the person booking takes one. */
  const capacity = space?.capacity ?? 0
  const seatsError: FieldError | null = space && capacity > 0 && invitees.length + 1 > capacity
    ? { code: 'booking.over_capacity', message: t('booking.attendees.overCapacity', { space: space.name, count: capacity }) }
    : null
  /* Checked as the form is filled in, so these show straight away and keep the button off. */
  const liveErrors: FieldErrors = {
    ...(timeError ? { [TIME_FIELD[timeError.code] ?? 'm-window']: timeError } : {}),
    ...(spaceError ? { 'm-space': spaceError } : {}),
    ...(seatsError ? { 'm-attendees': seatsError } : {}),
    ...recurErrors,
  }
  const fields = useFieldErrors()
  /* What the server said last wins over the live checks, until the form is changed again. */
  const shown: FieldErrors = { ...liveErrors, ...fields.errors }
  const edited = () => { fields.clear(...FIELD_IDS); setConflict(null) }

  /* t is a dependency so the notes follow a change of language. */
  const occurrences: OccurrenceRow[] = useMemo(() => {
    if (!generated || !spaceId) return []
    const space = spaces.find((s) => s.id === spaceId)
    return generated.occurrences.map((o) => {
      const clash = findOverlap(bookings, spaceId, o.start, o.end)
      /* Without Bookings.MultipleSpaces you can't hold two spaces at once, so your other bookings count. */
      const self = !clash && !multipleSpaces
        ? bookings.find((b) => b.ownerUserId === session.userId && b.spaceId !== spaceId && b.start < o.end && o.start < b.end)
        : undefined
      const hit = clash ?? self
      /* The building is closed that day (holiday or weekly closed day): it can't be booked at all. */
      const closed = space ? closedReason(space.constraints, o.start, o.end) : null
      const at = o.start.getTime()
      return {
        start: o.start,
        end: o.end,
        flagged: !!hit || !!closed,
        skip: closed ? true : skipOverrides[at] ?? !!hit,
        note: closed ? t('booking.occurrence.closed', { when: closed })
          : hit ? (hit.spaceId !== spaceId ? t('booking.occurrence.youHave', { space: hit.spaceName })
            : (seesAll ? t('booking.occurrence.takenBy', { name: hit.ownerName }) : t('booking.occurrence.alreadyBooked'))) : undefined,
      }
    })
  }, [generated, bookings, spaceId, spaces, session.userId, seesAll, multipleSpaces, skipOverrides, t])

  const create = useCreateBooking()
  const createSeries = useCreateBookingSeries()
  const reschedule = useRescheduleBooking()
  const setAttendees = useSetAttendees()
  const busy = create.isPending || createSeries.isPending || reschedule.isPending || setAttendees.isPending

  const showError = (e: unknown) => {
    const { message } = errorText(e)
    if (e instanceof ApiError && e.code === 'booking.conflict' && start && end && space) {
      const clashId = e.data.conflictingBookingId as string | undefined
      const clash = bookings.find((b) => b.id === clashId)
      const clashEnd = clash?.end ?? (e.data.conflictEnd ? new Date(String(e.data.conflictEnd)) : null)
      const suggested = clashEnd ? { start: clashEnd, end: addMin(clashEnd, (end.getTime() - start.getTime()) / 60000) } : null
      const stillBusy = suggested && findOverlap(bookings, space.id, suggested.start, suggested.end)
      setConflict({
        message: clash
          ? seesAll
            ? t('booking.conflict.byOwner', { space: space.name, owner: clash.ownerName, from: hm(clash.start), to: hm(clash.end) })
            : t('booking.conflict.taken', { space: space.name, from: hm(clash.start), to: hm(clash.end) })
          : message,
        suggested: stillBusy ? null : suggested,
      })
    } else fields.fromServer(e, SERVER_FIELDS)
  }

  const submit = async () => {
    const found: FieldErrors = {
      'm-building': buildingId ? undefined : missing(t('booking.pickBuilding')),
      'm-floor': !buildingId || floorId ? undefined : missing(t('booking.pickFloor')),
      'm-space': !floorId || spaceId ? undefined
        : missing(options.length ? t('booking.pickSpace') : t('booking.noFreeSpace')),
    }
    if (fields.show(found) || !start || !end || !space) return
    setConflict(null)
    const attendees = invitees.map(toAttendeeInput)
    try {
      if (editing) {
        /* Only what changed is saved, so nobody gets an email about a change that didn't happen. People first:
         * they don't change the booking's version, so a time refused afterwards can simply be tried again. */
        const timeChanged = start.getTime() !== editing.start.getTime() || end.getTime() !== editing.end.getTime()
        const peopleChanged = !guestsHidden && !samePeople(invitees, inviteesOf(editing))
        if (peopleChanged) await setAttendees.mutateAsync({ id: editing.id, attendees })
        if (timeChanged) {
          await reschedule.mutateAsync({ id: editing.id, startUtc: start.toISOString(), endUtc: end.toISOString(), expectedVersion: editing.version })
          toast('ok', t('booking.toast.rescheduled'), t('booking.toast.rescheduledMessage', { space: editing.spaceName, start: stampOffset(start), end: hm(end) }))
        } else if (peopleChanged) {
          toast('ok', t('booking.toast.peopleSaved'), t('booking.toast.peopleSavedMessage', { space: editing.spaceName }))
        }
        modals.close()
        return
      }
      if (rule && occurrences.length) {
        const wanted = occurrences.filter((o) => !o.skip)
        if (!wanted.length) { fields.show({ 'm-occurrences': missing(t('recurrence.tickOne')) }); return }
        const r = await createSeries.mutateAsync({
          spaceId: space.id,
          occurrences: wanted.map((o) => ({ startUtc: o.start.toISOString(), endUtc: o.end.toISOString() })),
          attendees,
        })
        if (r.created.length) {
          toast('ok', t('booking.toast.seriesConfirmed', { count: r.created.length }),
            r.skipped.length ? t('booking.toast.seriesOnSkipped', { space: space.name, count: r.skipped.length }) : t('booking.toast.seriesOn', { space: space.name }))
          modals.close()
        } else if (r.skipped[0]) {
          /* Nothing could be booked: say why under the occurrence list. */
          fields.show({ 'm-occurrences': { code: r.skipped[0].errorCode, message: r.skipped[0].errorMessage } })
        }
        return
      }
      await create.mutateAsync({
        spaceId: space.id, startUtc: start.toISOString(), endUtc: end.toISOString(),
        idempotencyKey, attendees,
      })
      toast('ok', t('booking.toast.confirmed'), t('booking.toast.confirmedMessage', { space: space.name, start: stampOffset(start), end: hm(end) }))
      modals.close()
    } catch (e) {
      showError(e)
    }
  }

  const onBehalf = editing && editing.ownerUserId !== session.userId
  const closedDays = space ? occurrences.filter((o) => closedReason(space.constraints, o.start, o.end)).length : 0
  const clashes = occurrences.filter((o) => o.flagged).length - closedDays
  const keptNote = (reason: KeptReason) => (reason ? ` — ${t(`booking.kept.${reason}`)}` : '')

  return (
    <Modal
      title={editing ? t('booking.rescheduleTitle') : t('booking.newTitle')}
      subtitle={editing
        ? onBehalf ? t('booking.rescheduleSubtitleOnBehalf', { space: editing.spaceName, owner: editing.ownerName }) : t('booking.rescheduleSubtitle', { space: editing.spaceName })
        : t('booking.newSubtitle')}
      onClose={modals.close}
      footer={(
        <>
          <button type="button" className="btn" onClick={modals.close}>{t('common.discard')}</button>
          <button type="button" className="btn btn-primary" disabled={busy || Object.keys(liveErrors).length > 0} onClick={submit}>
            {editing ? t('booking.saveWindow') : t('booking.make')}
          </button>
        </>
      )}
    >
      <p className="req-note"><span className="req-mark" aria-hidden="true">*</span> {t('common.requiredField')}</p>
      <div>
        <label className="lbl req" htmlFor="m-date">{t('common.date')}<RequiredMark /></label>
        <DatePicker id="m-date" value={date} min={dayKey(earliest)} onChange={onDate} />
        <ErrorLine id="m-date-error" error={shown['m-date']} />
      </div>
      <div className="form-cols" style={{ display: 'grid', gridTemplateColumns: '1fr 1fr', gap: 12 }}>
        <div>
          <label className="lbl req" htmlFor="m-start" style={{ display: 'flex', justifyContent: 'space-between', alignItems: 'center' }}>
            <span>{t('common.start')}<RequiredMark /></span> <span className="utcchip">UTC +00:00</span>
          </label>
          <TimePicker id="m-start" aria-label={t('common.start')} value={startTime} min={startMin} allowed={startAllowed} onChange={onStart} />
          {localHint(start)}
          <ErrorLine id="m-start-error" error={shown['m-start']} />
        </div>
        <div>
          <label className="lbl req" htmlFor="m-end" style={{ display: 'flex', justifyContent: 'space-between', alignItems: 'center' }}>
            <span>{t('common.end')}<RequiredMark /></span> <span className="utcchip">UTC +00:00</span>
          </label>
          <TimePicker id="m-end" aria-label={t('common.end')} value={endTime} min={endMin} allowed={endAllowed} onChange={(v) => { setEndTime(v); edited() }} />
          {localHint(end)}
          <ErrorLine id="m-end-error" error={shown['m-end']} />
        </div>
      </div>
      {/* Problems with the window as a whole (hours, length, already booked) sit right under Start and End. */}
      {shown['m-window'] && <div style={{ marginTop: -6 }}><ErrorLine id="m-window-error" error={shown['m-window']} /></div>}
      {conflict && (
        <div style={{ background: 'var(--rust-soft)', border: '1px solid var(--rust-line)', borderRadius: 9, padding: '12px 13px', marginTop: -6 }} role="alert">
          <div style={{ display: 'flex', gap: 8, alignItems: 'center', marginBottom: 6 }}>
            <strong style={{ fontSize: 13, color: 'var(--rust)' }}>{t('booking.slotTaken')}</strong>
          </div>
          <p style={{ fontSize: 12.5, margin: '0 0 9px' }}>{conflict.message}</p>
          <div style={{ display: 'flex', gap: 8 }}>
            {conflict.suggested && (
              <button type="button" className="btn btn-sm" onClick={() => {
                const s = conflict.suggested!
                setDate(dayKey(s.start)); setStartTime(hm(s.start)); setEndTime(hm(s.end)); edited()
              }}>
                {t('booking.moveTo', { from: hm(conflict.suggested.start), to: hm(conflict.suggested.end) })}
              </button>
            )}
            <button type="button" className="btn btn-sm" onClick={() => setConflict(null)}>{t('common.dismiss')}</button>
          </div>
        </div>
      )}

      {!editing && (
        <div style={{ borderTop: '1px solid var(--line)', paddingTop: 14 }}>
          <RecurrenceFields idPrefix="m" value={recur} errors={recurErrors} onChange={(v) => { setRecur(v); setSkipOverrides({}); fields.clear('m-occurrences') }} />
          {rule && occurrences.length > 0 && (
            <OccurrenceList
              rows={occurrences}
              truncated={!!generated?.truncated}
              summaryAlert={clashes + closedDays > 0}
              summary={[
                clashes ? t('booking.clashes', { count: clashes }) : '',
                closedDays ? t('booking.closedDays', { count: closedDays }) : '',
              ].filter(Boolean).join(' · ') || t('booking.noClashes')}
              onToggle={(i) => { setSkipOverrides({ ...skipOverrides, [occurrences[i].start.getTime()]: !occurrences[i].skip }); fields.clear('m-occurrences') }}
            />
          )}
          <ErrorLine id="m-occurrences-error" error={shown['m-occurrences']} />
        </div>
      )}

      <div style={{ borderTop: '1px solid var(--line)', paddingTop: 14 }}>
        <div className="form-cols" style={{ display: 'grid', gridTemplateColumns: '1fr 1fr', gap: 12, marginBottom: 12 }}>
          <div>
            <label className="lbl req" htmlFor="m-building">{t('common.building')}<RequiredMark /></label>
            <Dropdown id="m-building" value={buildingId} disabled={!!editing} placeholder={t('common.chooseBuilding')}
              options={buildingOptions}
              onChange={(v) => { setChosenBuildingId(v); setChosenFloorId(''); setChosenSpaceId(''); edited() }} />
            <ErrorLine id="m-building-error" error={shown['m-building']} />
          </div>
          <div>
            <label className="lbl req" htmlFor="m-floor">{t('common.floor')}<RequiredMark /></label>
            <Dropdown id="m-floor" value={floorId} disabled={!!editing || !buildingId}
              placeholder={buildingId ? t('common.chooseFloor') : t('common.chooseBuildingFirst')}
              options={floorOptions}
              onChange={(v) => { setChosenBuildingId(buildingId); setChosenFloorId(v); setChosenSpaceId(''); edited() }} />
            <ErrorLine id="m-floor-error" error={shown['m-floor']} />
          </div>
        </div>
        <label className="lbl req" htmlFor="m-space">{t('common.space')}<RequiredMark /></label>
        <Dropdown id="m-space" value={spaceId} disabled={!!editing || !floorId || !options.length}
          placeholder={!floorId ? t('common.chooseFloorFirst') : options.length ? t('booking.chooseSpace') : t('booking.noFreeSpaces')}
          options={options.map(({ space: s, reason }) => ({ value: s.id, label: `${s.name}${keptNote(reason)}` }))}
          onChange={(v) => { setChosenSpaceId(v); edited() }} />
        <ErrorLine id="m-space-error" error={shown['m-space']
          ?? (spacesQ.isError ? { code: 'network.error', message: t('booking.spacesLoadError', { message: errorText(spacesQ.error).message }) } : undefined)} />
        <p style={{ fontSize: 11.5, color: 'var(--slate)', margin: '6px 0 0' }}>
          {space
            ? `${t('booking.spaceInfo', { type: space.typeName, building: space.buildingName, floor: space.floorName, tz: space.timeZone })}${space.note ? ` · ${space.note}` : ''}`
            : spacesQ.isLoading ? t('booking.loadingSpaces')
              : floorId && !options.length ? t('booking.noFreeOnFloor') : ''}
        </p>
      </div>

      <div style={{ borderTop: '1px solid var(--line)', paddingTop: 14 }}>
        <label className="lbl" htmlFor="m-attendees">{t('booking.attendees.label')}</label>
        {guestsHidden
          ? <p className="muted-box" style={{ margin: 0 }}>{t('booking.attendees.guestsHidden')}</p>
          : <AttendeesField id="m-attendees" value={invitees} capacity={capacity} describedBy="m-attendees-error"
              onChange={(v) => { setInvitees(v); fields.clear('m-attendees') }} />}
        <ErrorLine id="m-attendees-error" error={shown['m-attendees']} />
      </div>
    </Modal>
  )
}
