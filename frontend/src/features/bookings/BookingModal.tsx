import { useMemo, useState } from 'react'
import { ApiError, errorText } from '../../api/client'
import { useBookings, useCreateBooking, useCreateBookingSeries, useRescheduleBooking, useSpaces } from '../../api/hooks'
import type { Booking, Space } from '../../api/types'
import { useSession } from '../../app/session'
import { ErrorLine, RequiredMark, shortId } from '../../components/bits'
import { Modal } from '../../components/Sheet'
import { DEFAULT_MAX_HOURS, DEFAULT_MIN_MINUTES } from '../../lib/constants'
import { addDays, addMin, dayAt, dayKey, durationLabel, fromDateTime, hm, isoZ, roundUp30, stampOffset } from '../../lib/dateUtils'
import { findOverlap, validateWindowLocal } from '../../lib/laneLayout'
import { generateOccurrences } from '../../lib/recurrence'
import { modals, type BookingPrefill } from '../../state/modalStore'
import { toast } from '../../state/toastStore'
import { defaultRecurrence, OccurrenceList, RecurrenceFields, toRule, type OccurrenceRow } from './Recurrence'

const newKey = () => `idem-${crypto.randomUUID()}`

function accessError(space: Space) {
  if (space.canCurrentUserBook) return null
  if (space.status !== 'Active') return { code: 'space.inactive', message: 'This space is inactive and cannot be booked.' }
  return { code: 'space.inactive', message: `${space.buildingName} or its floor is inactive and cannot be booked.` }
}

export function BookingModal({ prefill, editing }: { prefill: BookingPrefill; editing: Booking | null }) {
  const session = useSession()
  const spacesQ = useSpaces()
  const initStart = prefill.start ?? roundUp30(new Date())
  const initEnd = prefill.end ?? addMin(initStart, 60)

  const [date, setDate] = useState(dayKey(initStart))
  const [startTime, setStartTime] = useState(hm(initStart))
  const [endTime, setEndTime] = useState(hm(initEnd))
  const [chosenSpaceId, setChosenSpaceId] = useState(editing?.spaceId ?? prefill.spaceId ?? '')
  const [recur, setRecur] = useState(() => defaultRecurrence(initStart, 4))
  const [skipOverrides, setSkipOverrides] = useState<Record<number, boolean>>({})
  const [parking, setParking] = useState(false)
  const [idempotencyKey] = useState(newKey)
  const [conflict, setConflict] = useState<{ message: string; suggested: { start: Date; end: Date } | null } | null>(null)

  const start = fromDateTime(date, startTime)
  const end = fromDateTime(date, endTime)
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
  const bookingsQ = useBookings({ from: rangeFrom, to: rangeTo }, hasWindow)
  const bookings = useMemo(() => bookingsQ.data ?? [], [bookingsQ.data])

  const spaces = useMemo(() => spacesQ.data ?? [], [spacesQ.data])
  const eligible = useMemo(() => spaces.filter((s) => s.canCurrentUserBook), [spaces])

  /* Only spaces that are eligible AND free for the entered window; the previously chosen one is
   * kept (annotated) rather than vanishing mid-edit, as in the mock. */
  const options = useMemo(() => {
    if (editing) {
      const s = spaces.find((x) => x.id === editing.spaceId)
      return s ? [{ space: s, reason: '' }] : []
    }
    const free = (s: Space) => !hasWindow ||
      (!findOverlap(bookings, s.id, start!, end!) && !validateWindowLocal(s.constraints, s.name, start, end, true))
    const list = eligible.filter(free).map((space) => ({ space, reason: '' }))
    const kept = eligible.find((s) => s.id === chosenSpaceId)
    if (kept && !list.some((o) => o.space.id === kept.id)) {
      list.unshift({ space: kept, reason: findOverlap(bookings, kept.id, start!, end!) ? ' — booked at this time' : ' — outside its hours' })
    }
    return list
  }, [editing, spaces, eligible, bookings, hasWindow, start, end, chosenSpaceId])

  const spaceId = options.some((o) => o.space.id === chosenSpaceId) ? chosenSpaceId : options[0]?.space.id ?? ''
  const space = spaces.find((s) => s.id === spaceId) ?? null
  const c = space?.constraints
  const timeError = validateWindowLocal(c ?? null, space?.name ?? '', start, end)
  const spaceError = !editing && space ? accessError(space) : null

  const occurrences: OccurrenceRow[] = useMemo(() => {
    if (!generated || !spaceId) return []
    return generated.occurrences.map((o) => {
      const clash = findOverlap(bookings, spaceId, o.start, o.end)
      const self = !clash ? bookings.find((b) => b.ownerUserId === session.userId && b.spaceId !== spaceId && b.start < o.end && o.start < b.end) : undefined
      const hit = clash ?? self
      const t = o.start.getTime()
      return {
        start: o.start,
        end: o.end,
        flagged: !!hit,
        skip: skipOverrides[t] ?? !!hit,
        note: hit ? (hit.spaceId !== spaceId ? `you have ${hit.spaceName} then` : `taken by ${hit.ownerName}`) : undefined,
      }
    })
  }, [generated, bookings, spaceId, session.userId, skipOverrides])

  const create = useCreateBooking()
  const createSeries = useCreateBookingSeries()
  const reschedule = useRescheduleBooking()
  const busy = create.isPending || createSeries.isPending || reschedule.isPending

  const showError = (e: unknown) => {
    const { code, message } = errorText(e)
    if (e instanceof ApiError && code === 'booking.conflict' && start && end && space) {
      const clashId = e.data.conflictingBookingId as string | undefined
      const clash = bookings.find((b) => b.id === clashId)
      const clashEnd = clash?.end ?? (e.data.conflictEnd ? new Date(String(e.data.conflictEnd)) : null)
      const suggested = clashEnd ? { start: clashEnd, end: addMin(clashEnd, (end.getTime() - start.getTime()) / 60000) } : null
      const stillBusy = suggested && findOverlap(bookings, space.id, suggested.start, suggested.end)
      setConflict({
        message: clash
          ? `${space.name} is held by ${clash.ownerName} from ${hm(clash.start)} to ${hm(clash.end)} UTC (${shortId(clash.id)}). Bookings on one space may never overlap.`
          : message,
        suggested: stillBusy ? null : suggested,
      })
      toast('err', 'Conflict: slot already booked', message, code)
    } else toast('err', 'Request rejected', message, code)
  }

  const submit = async () => {
    if (!start || !end || !space) return
    setConflict(null)
    try {
      if (editing) {
        await reschedule.mutateAsync({ id: editing.id, startUtc: start.toISOString(), endUtc: end.toISOString(), expectedVersion: editing.version })
        toast('ok', 'Booking rescheduled', `${shortId(editing.id)} now ${stampOffset(start)} → ${hm(end)}.`)
        modals.close()
        return
      }
      if (rule && occurrences.length) {
        const wanted = occurrences.filter((o) => !o.skip)
        if (!wanted.length) { toast('warn', 'Nothing selected', 'Tick at least one occurrence.'); return }
        const r = await createSeries.mutateAsync({
          spaceId: space.id,
          parking: session.isAdmin && parking,
          occurrences: wanted.map((o) => ({ startUtc: o.start.toISOString(), endUtc: o.end.toISOString() })),
        })
        if (r.created.length) {
          toast('ok', `${r.created.length} bookings confirmed`, `Series on ${space.name}${r.skipped.length ? ` · ${r.skipped.length} skipped` : ''}.`)
          modals.close()
        } else if (r.skipped[0]) {
          toast('err', 'Nothing booked', r.skipped[0].errorMessage, r.skipped[0].errorCode)
        }
        return
      }
      const b = await create.mutateAsync({
        spaceId: space.id, startUtc: start.toISOString(), endUtc: end.toISOString(),
        parking: session.isAdmin && parking, idempotencyKey,
      })
      toast('ok', 'Booking confirmed', `${shortId(b.id)} · ${space.name} · ${stampOffset(start)} → ${hm(end)}.`)
      modals.close()
    } catch (e) {
      showError(e)
    }
  }

  const onBehalf = editing && editing.ownerUserId !== session.userId
  const clashes = occurrences.filter((o) => o.flagged).length

  return (
    <Modal
      title={editing ? 'Reschedule booking' : 'New booking'}
      subtitle={editing
        ? `${shortId(editing.id)} · ${editing.spaceName}${onBehalf ? ` · owned by ${editing.ownerName}` : ''} · moving the window only`
        : 'Reserve one space for one window of time.'}
      onClose={modals.close}
      footer={(
        <>
          <button type="button" className="btn" onClick={modals.close}>Discard</button>
          <button type="button" className="btn btn-primary" disabled={busy || !!timeError || !!spaceError || !spaceId} onClick={submit}>
            {editing ? 'Save new window' : 'Confirm booking'}
          </button>
        </>
      )}
    >
      <p className="req-note"><span className="req-mark" aria-hidden="true">*</span> Required field</p>
      <div>
        <label className="lbl req" htmlFor="m-date">Date<RequiredMark /></label>
        <input type="date" id="m-date" className="inp mono" required value={date} onChange={(e) => setDate(e.target.value)} />
      </div>
      <div style={{ display: 'grid', gridTemplateColumns: '1fr 1fr', gap: 12 }}>
        <div>
          <label className="lbl req" htmlFor="m-start" style={{ display: 'flex', justifyContent: 'space-between', alignItems: 'center' }}>
            <span>Start<RequiredMark /></span> <span className="utcchip">UTC +00:00</span>
          </label>
          <input type="time" id="m-start" className="inp mono" required value={startTime} onChange={(e) => setStartTime(e.target.value)} />
        </div>
        <div>
          <label className="lbl req" htmlFor="m-end" style={{ display: 'flex', justifyContent: 'space-between', alignItems: 'center' }}>
            <span>End<RequiredMark /></span> <span className="utcchip">UTC +00:00</span>
          </label>
          <input type="time" id="m-end" className="inp mono" required value={endTime} onChange={(e) => setEndTime(e.target.value)} />
        </div>
      </div>
      <div style={{ marginTop: -6 }}>
        {hasWindow && (
          <p style={{ fontSize: 12, color: 'var(--slate)' }}>
            Duration <strong>{durationLabel((end!.getTime() - start!.getTime()) / 60000)}</strong> · allowed {c?.minBookingMinutes ?? DEFAULT_MIN_MINUTES}m
            to {c?.maxBookingHours ?? DEFAULT_MAX_HOURS}h · <span className="mono">{isoZ(start!)}</span> → <span className="mono">{isoZ(end!)}</span>
          </p>
        )}
        <ErrorLine error={timeError} />
      </div>

      {!editing && (
        <div style={{ borderTop: '1px solid var(--line)', paddingTop: 14 }}>
          <RecurrenceFields idPrefix="m" value={recur} onChange={(v) => { setRecur(v); setSkipOverrides({}) }} />
          {rule && occurrences.length > 0 && (
            <OccurrenceList
              rows={occurrences}
              truncated={!!generated?.truncated}
              summaryAlert={clashes > 0}
              summary={clashes ? `${clashes} clash with an existing booking and are excluded` : 'No clashes'}
              onToggle={(i) => setSkipOverrides({ ...skipOverrides, [occurrences[i].start.getTime()]: !occurrences[i].skip })}
            />
          )}
        </div>
      )}

      <div style={{ borderTop: '1px solid var(--line)', paddingTop: 14 }}>
        <label className="lbl req" htmlFor="m-space">Space<RequiredMark /></label>
        <select id="m-space" className="inp" required value={spaceId} disabled={!!editing || !options.length}
          onChange={(e) => setChosenSpaceId(e.target.value)}>
          {options.map(({ space: s, reason }) => <option key={s.id} value={s.id}>{s.name}{reason}</option>)}
        </select>
        <p style={{ fontSize: 11.5, color: 'var(--slate)', margin: '6px 0 0' }}>
          {space
            ? `${space.typeName} · ${space.buildingName}, floor ${space.floorName} · local zone ${space.timeZone}${space.note ? ` · ${space.note}` : ''}`
            : spacesQ.isLoading ? 'Loading spaces…' : 'No spaces are free for this time — try a different window.'}
        </p>
        <ErrorLine error={spaceError} />
      </div>

      {session.isAdmin && !editing && (
        <div>
          <label style={{ display: 'flex', gap: 8, alignItems: 'center', fontSize: 13, fontWeight: 500, cursor: 'pointer' }}>
            <input type="checkbox" checked={parking} onChange={(e) => setParking(e.target.checked)} />
            🚗 Reserve parking for this booking
          </label>
          <p style={{ fontSize: 11.5, color: 'var(--slate)', margin: '5px 0 0' }}>Management-only — reserved for the visitor or guest attending this booking.</p>
        </div>
      )}

      {conflict && (
        <div style={{ background: 'var(--rust-soft)', border: '1px solid var(--rust-line)', borderRadius: 9, padding: '12px 13px' }}>
          <div style={{ display: 'flex', gap: 8, alignItems: 'center', marginBottom: 6 }}>
            <span className="errcode">409 booking.conflict</span>
            <strong style={{ fontSize: 13, color: 'var(--rust)' }}>Slot already booked</strong>
          </div>
          <p style={{ fontSize: 12.5, margin: '0 0 9px' }}>{conflict.message}</p>
          <div style={{ display: 'flex', gap: 8 }}>
            {conflict.suggested && (
              <button type="button" className="btn btn-sm" onClick={() => {
                const s = conflict.suggested!
                setDate(dayKey(s.start)); setStartTime(hm(s.start)); setEndTime(hm(s.end)); setConflict(null)
              }}>
                Move to {hm(conflict.suggested.start)}–{hm(conflict.suggested.end)}
              </button>
            )}
            <button type="button" className="btn btn-sm" onClick={() => setConflict(null)}>Dismiss</button>
          </div>
        </div>
      )}
    </Modal>
  )
}
