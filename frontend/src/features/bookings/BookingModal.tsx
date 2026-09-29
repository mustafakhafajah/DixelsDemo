import { useMemo, useState } from 'react'
import { ApiError, errorText } from '../../api/client'
import { useAvailability, useCreateBooking, useCreateBookingSeries, useRescheduleBooking, useSpaces } from '../../api/hooks'
import type { Booking, Space } from '../../api/types'
import { useSession } from '../../app/session'
import { ErrorLine, RequiredMark } from '../../components/bits'
import { DatePicker, Dropdown, TimePicker } from '../../components/pickers'
import { Modal } from '../../components/Sheet'
import { closedReason } from '../../lib/closedDays'
import { addDays, addMin, dayAt, dayKey, earliestStart, fromDateTime, hm, keepWindowAhead, roundUp30, stampOffset } from '../../lib/dateUtils'
import { findOverlap, validateWindowLocal } from '../../lib/laneLayout'
import { generateOccurrences } from '../../lib/recurrence'
import { useFieldErrors, type FieldError, type FieldErrors } from '../../lib/useFieldErrors'
import { modals, type BookingPrefill } from '../../state/modalStore'
import { toast } from '../../state/toastStore'
import { defaultRecurrence, OccurrenceList, recurrenceErrors, RecurrenceFields, toRule, type OccurrenceRow } from './Recurrence'

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
const SERVER_FIELDS = { date: 'm-date', start: 'm-start', end: 'm-end', window: 'm-window', spaceId: 'm-space' }
const FIELD_IDS = ['m-date', 'm-start', 'm-end', 'm-window', 'm-building', 'm-floor', 'm-space', 'm-occurrences']

/* The server says exactly which level blocks it (space, floor or building). */
function accessError(space: Space) {
  if (space.canCurrentUserBook) return null
  return { code: 'space.not_bookable', message: space.notBookableReason ?? `${space.name} is not bookable.` }
}

export function BookingModal({ prefill, editing }: { prefill: BookingPrefill; editing: Booking | null }) {
  const session = useSession()
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
  const ruleKey = JSON.stringify(rule)
  const startMs = start?.getTime()
  const endMs = end?.getTime()
  const generated = useMemo(() => {
    const r = JSON.parse(ruleKey)
    if (!r || startMs === undefined || endMs === undefined) return null
    if (r.end.mode === 'until') r.end.until = new Date(r.end.until)
    return generateOccurrences(new Date(startMs), new Date(endMs), r)
  }, [ruleKey, startMs, endMs])

  const rangeFrom = start ? dayAt(dayKey(start)) : undefined
  const lastOcc = generated?.occurrences.at(-1)?.end ?? end
  const rangeTo = lastOcc ? addDays(dayAt(dayKey(lastOcc)), 1) : undefined
  const bookingsQ = useAvailability({ from: rangeFrom, to: rangeTo }, session, hasWindow)
  const bookings = useMemo(() => bookingsQ.data ?? [], [bookingsQ.data])

  const spaces = useMemo(() => spacesQ.data ?? [], [spacesQ.data])
  const eligible = useMemo(() => spaces.filter((s) => s.canCurrentUserBook), [spaces])

  /* Building -> floor -> space: each picker stays locked until the one above it is chosen. */
  const seed = spaces.find((s) => s.id === (editing?.spaceId ?? prefill.spaceId))
  const buildingId = chosenBuildingId ?? seed?.buildingId ?? ''
  const floorId = chosenFloorId ?? (seed && seed.buildingId === buildingId ? seed.floorId : '')
  const buildingOptions = uniqueBy(eligible, (s) => s.buildingId, (s) => s.buildingName)
  const floorOptions = buildingId ? uniqueBy(eligible.filter((s) => s.buildingId === buildingId), (s) => s.floorId, (s) => `Floor ${s.floorName}`) : []

  /* Only spaces that are eligible AND free for the entered window; the previously chosen one is
   * kept (annotated) rather than vanishing mid-edit, as in the mock. */
  const options = useMemo(() => {
    if (editing) {
      const s = spaces.find((x) => x.id === editing.spaceId)
      return s ? [{ space: s, reason: '' }] : []
    }
    const free = (s: Space) => !hasWindow ||
      (!findOverlap(bookings, s.id, start!, end!) && !validateWindowLocal(s.constraints, s.name, start, end, true))
    if (!floorId) return []
    const onFloor = eligible.filter((s) => s.floorId === floorId)
    const list = onFloor.filter(free).map((space) => ({ space, reason: '' }))
    const kept = onFloor.find((s) => s.id === chosenSpaceId)
    if (kept && !list.some((o) => o.space.id === kept.id)) {
      list.unshift({ space: kept, reason: findOverlap(bookings, kept.id, start!, end!) ? ' — booked at this time'
        : closedReason(kept.constraints, start!, end!) ? ' — closed that day' : ' — outside its hours' })
    }
    return list
  }, [editing, spaces, eligible, floorId, bookings, hasWindow, start, end, chosenSpaceId])

  const spaceId = options.some((o) => o.space.id === chosenSpaceId) ? chosenSpaceId : ''
  const space = spaces.find((s) => s.id === spaceId) ?? null
  const c = space?.constraints
  const timeError = validateWindowLocal(c ?? null, space?.name ?? '', start, end)
  const spaceError = !editing && space ? accessError(space) : null
  const recurErrors = !editing ? recurrenceErrors(recur, start, 'm') : {}
  /* Checked as the form is filled in, so these show straight away and keep the button off. */
  const liveErrors: FieldErrors = {
    ...(timeError ? { [TIME_FIELD[timeError.code] ?? 'm-window']: timeError } : {}),
    ...(spaceError ? { 'm-space': spaceError } : {}),
    ...recurErrors,
  }
  const fields = useFieldErrors()
  /* What the server said last wins over the live checks, until the form is changed again. */
  const shown: FieldErrors = { ...liveErrors, ...fields.errors }
  const edited = () => { fields.clear(...FIELD_IDS); setConflict(null) }

  const occurrences: OccurrenceRow[] = useMemo(() => {
    if (!generated || !spaceId) return []
    return generated.occurrences.map((o) => {
      const clash = findOverlap(bookings, spaceId, o.start, o.end)
      /* Employees can't hold two spaces at once; admins can, so their other bookings don't count. */
      const self = !clash && !session.isAdmin
        ? bookings.find((b) => b.ownerUserId === session.userId && b.spaceId !== spaceId && b.start < o.end && o.start < b.end)
        : undefined
      const hit = clash ?? self
      /* The building is closed that day (holiday or weekly closed day): it can't be booked at all. */
      const closed = space ? closedReason(space.constraints, o.start, o.end) : null
      const t = o.start.getTime()
      return {
        start: o.start,
        end: o.end,
        flagged: !!hit || !!closed,
        skip: closed ? true : skipOverrides[t] ?? !!hit,
        note: closed ? `closed ${closed}`
          : hit ? (hit.spaceId !== spaceId ? `you have ${hit.spaceName} then` : (session.isAdmin ? `taken by ${hit.ownerName}` : 'already booked')) : undefined,
      }
    })
  }, [generated, bookings, spaceId, space, session.userId, session.isAdmin, skipOverrides])

  const create = useCreateBooking()
  const createSeries = useCreateBookingSeries()
  const reschedule = useRescheduleBooking()
  const busy = create.isPending || createSeries.isPending || reschedule.isPending

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
          ? `${space.name} is already booked${session.isAdmin ? ` by ${clash.ownerName}` : ''} from ${hm(clash.start)} to ${hm(clash.end)} UTC. Bookings on one space may never overlap.`
          : message,
        suggested: stillBusy ? null : suggested,
      })
    } else fields.fromServer(e, SERVER_FIELDS)
  }

  const submit = async () => {
    const found: FieldErrors = {
      'm-building': buildingId ? undefined : missing('Pick a building.'),
      'm-floor': !buildingId || floorId ? undefined : missing('Pick a floor.'),
      'm-space': !floorId || spaceId ? undefined
        : missing(options.length ? 'Pick a space.' : 'No space on this floor is free then. Try another time or floor.'),
    }
    if (fields.show(found) || !start || !end || !space) return
    setConflict(null)
    try {
      if (editing) {
        await reschedule.mutateAsync({ id: editing.id, startUtc: start.toISOString(), endUtc: end.toISOString(), expectedVersion: editing.version })
        toast('ok', 'Booking rescheduled', `${editing.spaceName} · now ${stampOffset(start)} → ${hm(end)}.`)
        modals.close()
        return
      }
      if (rule && occurrences.length) {
        const wanted = occurrences.filter((o) => !o.skip)
        if (!wanted.length) { fields.show({ 'm-occurrences': missing('Tick at least one occurrence.') }); return }
        const r = await createSeries.mutateAsync({
          spaceId: space.id,
          occurrences: wanted.map((o) => ({ startUtc: o.start.toISOString(), endUtc: o.end.toISOString() })),
        })
        if (r.created.length) {
          toast('ok', `${r.created.length} bookings confirmed`, `Series on ${space.name}${r.skipped.length ? ` · ${r.skipped.length} skipped` : ''}.`)
          modals.close()
        } else if (r.skipped[0]) {
          /* Nothing could be booked: say why under the occurrence list. */
          fields.show({ 'm-occurrences': { code: r.skipped[0].errorCode, message: r.skipped[0].errorMessage } })
        }
        return
      }
      await create.mutateAsync({
        spaceId: space.id, startUtc: start.toISOString(), endUtc: end.toISOString(),
        idempotencyKey,
      })
      toast('ok', 'Booking confirmed', `${space.name} · ${stampOffset(start)} → ${hm(end)}.`)
      modals.close()
    } catch (e) {
      showError(e)
    }
  }

  const onBehalf = editing && editing.ownerUserId !== session.userId
  const closedDays = space ? occurrences.filter((o) => closedReason(space.constraints, o.start, o.end)).length : 0
  const clashes = occurrences.filter((o) => o.flagged).length - closedDays

  return (
    <Modal
      title={editing ? 'Reschedule booking' : 'New booking'}
      subtitle={editing
        ? `${editing.spaceName}${onBehalf ? ` · owned by ${editing.ownerName}` : ''} · moving the window only`
        : 'Reserve one space for one window of time.'}
      onClose={modals.close}
      footer={(
        <>
          <button type="button" className="btn" onClick={modals.close}>Discard</button>
          <button type="button" className="btn btn-primary" disabled={busy || Object.keys(liveErrors).length > 0} onClick={submit}>
            {editing ? 'Save new window' : 'Make a booking'}
          </button>
        </>
      )}
    >
      <p className="req-note"><span className="req-mark" aria-hidden="true">*</span> Required field</p>
      <div>
        <label className="lbl req" htmlFor="m-date">Date<RequiredMark /></label>
        <DatePicker id="m-date" value={date} min={dayKey(earliest)} onChange={onDate} />
        <ErrorLine id="m-date-error" error={shown['m-date']} />
      </div>
      <div className="form-cols" style={{ display: 'grid', gridTemplateColumns: '1fr 1fr', gap: 12 }}>
        <div>
          <label className="lbl req" htmlFor="m-start" style={{ display: 'flex', justifyContent: 'space-between', alignItems: 'center' }}>
            <span>Start<RequiredMark /></span> <span className="utcchip">UTC +00:00</span>
          </label>
          <TimePicker id="m-start" aria-label="Start" value={startTime} min={startMin} onChange={onStart} />
          <ErrorLine id="m-start-error" error={shown['m-start']} />
        </div>
        <div>
          <label className="lbl req" htmlFor="m-end" style={{ display: 'flex', justifyContent: 'space-between', alignItems: 'center' }}>
            <span>End<RequiredMark /></span> <span className="utcchip">UTC +00:00</span>
          </label>
          <TimePicker id="m-end" aria-label="End" value={endTime} min={endMin} onChange={(v) => { setEndTime(v); edited() }} />
          <ErrorLine id="m-end-error" error={shown['m-end']} />
        </div>
      </div>
      {/* Problems with the window as a whole (hours, length, already booked) sit right under Start and End. */}
      {shown['m-window'] && <div style={{ marginTop: -6 }}><ErrorLine id="m-window-error" error={shown['m-window']} /></div>}
      {conflict && (
        <div style={{ background: 'var(--rust-soft)', border: '1px solid var(--rust-line)', borderRadius: 9, padding: '12px 13px', marginTop: -6 }} role="alert">
          <div style={{ display: 'flex', gap: 8, alignItems: 'center', marginBottom: 6 }}>
            <strong style={{ fontSize: 13, color: 'var(--rust)' }}>Slot already booked</strong>
          </div>
          <p style={{ fontSize: 12.5, margin: '0 0 9px' }}>{conflict.message}</p>
          <div style={{ display: 'flex', gap: 8 }}>
            {conflict.suggested && (
              <button type="button" className="btn btn-sm" onClick={() => {
                const s = conflict.suggested!
                setDate(dayKey(s.start)); setStartTime(hm(s.start)); setEndTime(hm(s.end)); edited()
              }}>
                Move to {hm(conflict.suggested.start)}–{hm(conflict.suggested.end)}
              </button>
            )}
            <button type="button" className="btn btn-sm" onClick={() => setConflict(null)}>Dismiss</button>
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
                clashes ? `${clashes} clash with an existing booking and are excluded` : '',
                closedDays ? `${closedDays} fall on a day the building is closed and are left out` : '',
              ].filter(Boolean).join(' · ') || 'No clashes'}
              onToggle={(i) => { setSkipOverrides({ ...skipOverrides, [occurrences[i].start.getTime()]: !occurrences[i].skip }); fields.clear('m-occurrences') }}
            />
          )}
          <ErrorLine id="m-occurrences-error" error={shown['m-occurrences']} />
        </div>
      )}

      <div style={{ borderTop: '1px solid var(--line)', paddingTop: 14 }}>
        <div className="form-cols" style={{ display: 'grid', gridTemplateColumns: '1fr 1fr', gap: 12, marginBottom: 12 }}>
          <div>
            <label className="lbl req" htmlFor="m-building">Building<RequiredMark /></label>
            <Dropdown id="m-building" value={buildingId} disabled={!!editing} placeholder="Choose a building"
              options={buildingOptions}
              onChange={(v) => { setChosenBuildingId(v); setChosenFloorId(''); setChosenSpaceId(''); edited() }} />
            <ErrorLine id="m-building-error" error={shown['m-building']} />
          </div>
          <div>
            <label className="lbl req" htmlFor="m-floor">Floor<RequiredMark /></label>
            <Dropdown id="m-floor" value={floorId} disabled={!!editing || !buildingId}
              placeholder={buildingId ? 'Choose a floor' : 'Choose a building first'}
              options={floorOptions}
              onChange={(v) => { setChosenBuildingId(buildingId); setChosenFloorId(v); setChosenSpaceId(''); edited() }} />
            <ErrorLine id="m-floor-error" error={shown['m-floor']} />
          </div>
        </div>
        <label className="lbl req" htmlFor="m-space">Space<RequiredMark /></label>
        <Dropdown id="m-space" value={spaceId} disabled={!!editing || !floorId || !options.length}
          placeholder={!floorId ? 'Choose a floor first' : options.length ? 'Choose a space' : 'No free spaces at this time'}
          options={options.map(({ space: s, reason }) => ({ value: s.id, label: `${s.name}${reason}` }))}
          onChange={(v) => { setChosenSpaceId(v); edited() }} />
        <ErrorLine id="m-space-error" error={shown['m-space']
          ?? (spacesQ.isError ? { code: 'network.error', message: `Couldn't load the spaces: ${errorText(spacesQ.error).message}` } : undefined)} />
        <p style={{ fontSize: 11.5, color: 'var(--slate)', margin: '6px 0 0' }}>
          {space
            ? `${space.typeName} · ${space.buildingName}, floor ${space.floorName} · local zone ${space.timeZone}${space.note ? ` · ${space.note}` : ''}`
            : spacesQ.isLoading ? 'Loading spaces…'
              : floorId && !options.length ? 'No spaces on this floor are free for this time — try a different window or floor.' : ''}
        </p>
      </div>
    </Modal>
  )
}
