import { useState } from 'react'
import { useBooking, useBookings, useMaintenanceWindow, useSpaces } from '../../api/hooks'
import { lifecycleOf, type Booking, type Maintenance } from '../../api/types'
import { useSession } from '../../app/session'
import { Loading, shortId, StatusPill } from '../../components/bits'
import { Drawer } from '../../components/Sheet'
import { dayKey, durationLabel, parseUtc, stamp, stampOffset } from '../../lib/dateUtils'
import { modals } from '../../state/modalStore'
import { useBookingActions } from './useBookingActions'

function BookingDetail({ b }: { b: Booking }) {
  const session = useSession()
  const spaces = useSpaces()
  const actions = useBookingActions()
  const space = spaces.data?.find((s) => s.id === b.spaceId)
  const state = lifecycleOf(b)
  const mine = b.ownerUserId === session.userId
  const may = mine || session.isAdmin
  const series = useBookings({ from: b.start }, !!b.seriesId)
  const laterInSeries = b.seriesId ? (series.data ?? []).filter((x) => x.seriesId === b.seriesId && x.id !== b.id).length : 0
  const [askSeries, setAskSeries] = useState(false)

  let note = ''
  if (state === 'ended') note = 'This booking has ended. Ended bookings are locked and cannot be changed.'
  else if (state === 'cancelled') note = 'Cancelled. The record is kept and the window is bookable again.'
  else if (state === 'in_progress') note = 'This booking has already started, so it can only be cancelled or ended early.'
  else if (!mine && session.isAdmin) note = `Owned by ${b.ownerName}. As the space administrator you can reschedule or cancel it.`
  else if (!mine) note = `Owned by ${b.ownerName}. You can only change your own bookings.`

  const onCancel = () => (laterInSeries > 0 ? setAskSeries(true) : actions.cancel(b))

  return (
    <>
      <div style={{ display: 'flex', gap: 8, alignItems: 'center', marginBottom: 14, flexWrap: 'wrap' }}>
        <StatusPill item={b} />
        <span className="tag mono">v{b.version}</span>
        {b.seriesId && <span className="tag mono">{shortId(b.seriesId, 'SR')}</span>}
        {b.parking && <span className="tag">🚗 Parking</span>}
      </div>
      <dl className="kv" style={{ marginBottom: 16 }}>
        <dt>Space</dt>
        <dd>{b.spaceName}{space && <div style={{ fontSize: 11.5, color: 'var(--slate)', fontWeight: 400 }}>{space.buildingName} · Floor {space.floorName} · {space.timeZone}</div>}</dd>
        <dt>Booked by</dt>
        <dd>{b.ownerName}</dd>
        <dt>Start</dt><dd className="mono">{stampOffset(b.start)}</dd>
        <dt>End</dt><dd className="mono">{stampOffset(b.end)}</dd>
        <dt>Duration</dt><dd>{durationLabel((b.end.getTime() - b.start.getTime()) / 60000)}</dd>
        <dt>Created</dt><dd className="mono" style={{ fontWeight: 400 }}>{stamp(parseUtc(b.creationTime))}</dd>
        {b.lastModificationTime && <><dt>Updated</dt><dd className="mono" style={{ fontWeight: 400 }}>{stamp(parseUtc(b.lastModificationTime))}</dd></>}
      </dl>
      {note && <p className="muted-box" style={{ margin: '0 0 14px' }}>{note}</p>}

      {askSeries ? (
        <div className="muted-box" style={{ marginBottom: 18 }}>
          <p style={{ margin: '0 0 10px', color: 'var(--ink)' }}>This booking is part of a repeating series. What should be cancelled?</p>
          <div style={{ display: 'flex', gap: 8, flexWrap: 'wrap' }}>
            <button type="button" className="btn btn-sm btn-danger" disabled={actions.busy} onClick={() => actions.cancel(b).then(() => setAskSeries(false))}>Only this one</button>
            <button type="button" className="btn btn-sm btn-danger" disabled={actions.busy} onClick={() => actions.cancelSeriesFrom(b).then(() => setAskSeries(false))}>This and all later ones</button>
            <button type="button" className="btn btn-sm" onClick={() => setAskSeries(false)}>Keep it</button>
          </div>
        </div>
      ) : (
        <div style={{ display: 'flex', gap: 8, marginBottom: 18 }}>
          {may && state === 'scheduled' && <button type="button" className="btn" onClick={() => modals.reschedule(b)}>Reschedule</button>}
          {may && state === 'in_progress' && <button type="button" className="btn" disabled={actions.busy} onClick={() => actions.endEarly(b)}>End now</button>}
          {may && (state === 'scheduled' || state === 'in_progress') && <button type="button" className="btn btn-danger" disabled={actions.busy} onClick={onCancel}>Cancel booking</button>}
          {(!may || state === 'ended' || state === 'cancelled') && <span style={{ fontSize: 12, color: 'var(--slate-2)' }}>No actions available.</span>}
        </div>
      )}

      <details>
        <summary style={{ cursor: 'pointer', fontSize: 12, fontWeight: 600, color: 'var(--slate)' }}>Raw record</summary>
        <pre className="json" style={{ marginTop: 9 }}>{JSON.stringify({ ...b, start: b.start.toISOString(), end: b.end.toISOString(), lifecycle: state }, null, 2)}</pre>
      </details>
    </>
  )
}

function MaintenanceDetail({ m }: { m: Maintenance }) {
  const session = useSession()
  const actions = useBookingActions()
  const state = lifecycleOf(m)
  const label = state === 'cancelled' ? 'Cancelled' : state === 'ended' ? 'Ended' : state === 'in_progress' ? 'In progress' : 'Scheduled'
  const cls = state === 'cancelled' ? 'pill-cancelled' : state === 'ended' ? 'pill-ended' : state === 'in_progress' ? 'pill-inprog' : 'pill-confirmed'
  return (
    <>
      <div style={{ display: 'flex', gap: 8, alignItems: 'center', marginBottom: 14 }}>
        <span className={`pill ${cls}`}><span className="dot" />{label}</span>
        <span className="tag">{m.scopeType} blocked</span>
        {m.seriesId && <span className="tag mono">{shortId(m.seriesId, 'MS')}</span>}
      </div>
      <dl className="kv" style={{ marginBottom: 16 }}>
        <dt>Scope</dt><dd>{m.scopeLabel}</dd>
        <dt>Space</dt><dd>{m.spaceName}</dd>
        <dt>Start</dt><dd className="mono">{stampOffset(m.start)}</dd>
        <dt>End</dt><dd className="mono">{stampOffset(m.end)}</dd>
        <dt>Reason</dt><dd>{m.note || 'Blocked'}</dd>
        <dt>Created</dt><dd className="mono" style={{ fontWeight: 400 }}>{stamp(parseUtc(m.creationTime))}</dd>
      </dl>
      <div style={{ display: 'flex', gap: 8 }}>
        {session.isAdmin && m.status === 'Active' && state !== 'ended'
          ? <button type="button" className="btn btn-danger" disabled={actions.busy} onClick={() => actions.cancelMaintenance(m.id)}>Unblock this time</button>
          : <span style={{ fontSize: 12, color: 'var(--slate-2)' }}>No actions available.</span>}
      </div>
    </>
  )
}

export function DetailDrawer({ entity, id }: { entity: 'booking' | 'maintenance'; id: string }) {
  const booking = useBooking(entity === 'booking' ? id : null)
  const maint = useMaintenanceWindow(entity === 'maintenance' ? id : null)
  const b = booking.data
  const m = maint.data
  const title = entity === 'booking' ? shortId(id) : shortId(id, 'MT')
  const subtitle = b ? `${b.spaceName} · ${dayKey(b.start)}` : m ? `${m.note || 'Blocked'} · ${m.scopeLabel}` : ''
  const failed = booking.error || maint.error

  return (
    <Drawer title={title} titleClassName="mono" subtitle={subtitle} onClose={modals.close}>
      {b ? <BookingDetail b={b} /> : m ? <MaintenanceDetail m={m} /> : failed ? <p className="muted-box">This record could not be loaded.</p> : <Loading />}
    </Drawer>
  )
}
