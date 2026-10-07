import { useState } from 'react'
import { useTranslation } from 'react-i18next'
import { useBooking, useBookings, useMaintenanceWindow, useSpaces } from '../../api/hooks'
import { lifecycleOf, type Booking, type Maintenance } from '../../api/types'
import { useSession } from '../../app/session'
import { P } from '../../auth/permissions'
import { Loading, StatusPill } from '../../components/bits'
import { Drawer } from '../../components/Sheet'
import { dayKey, durationLabel, parseUtc, stamp, stampOffset } from '../../lib/dateUtils'
import { modals } from '../../state/modalStore'
import { isInvited } from './schedule/scheduleItems'
import { useBookingActions } from './useBookingActions'

/* Who is invited: colleagues by name (you as "You"), then the outside guests: their addresses for the owner and
 * admins, otherwise (and once the booking is over and they're wiped) just how many. */
function Attendees({ b, myId }: { b: Booking; myId: string }) {
  const { t } = useTranslation()
  const hiddenGuests = b.guestCount - b.guests.length
  return (
    <ul style={{ listStyle: 'none', margin: 0, padding: 0, display: 'flex', flexDirection: 'column', gap: 2 }}>
      {b.attendees.map((a) => <li key={a.userId}><bdi>{a.userId === myId ? t('common.you') : a.name}</bdi></li>)}
      {b.guests.map((email) => (
        <li key={email}><bdi dir="ltr">{email}</bdi> <span className="tag">{t('booking.attendees.guest')}</span></li>
      ))}
      {hiddenGuests > 0 && (
        <li style={{ color: 'var(--slate)', fontWeight: 400 }}>
          {t(b.guests.length || b.attendees.length ? 'detail.moreGuests' : 'detail.guests', { count: hiddenGuests })}
        </li>
      )}
    </ul>
  )
}

function BookingDetail({ b, refuse }: { b: Booking; refuse: boolean }) {
  const { t } = useTranslation()
  const session = useSession()
  const spaces = useSpaces()
  const actions = useBookingActions()
  const space = spaces.data?.find((s) => s.id === b.spaceId)
  const state = lifecycleOf(b)
  const mine = b.ownerUserId === session.userId
  const invited = isInvited(b, session.userId)
  /* Mirrors the server: someone else's booking needs ViewAll to see who made it, EditAll to change it and
   * DeleteAll to cancel it, on top of the own-booking Edit / Delete. People invited see who booked it, and may
   * only take themselves off it. */
  const seesOwner = mine || invited || session.can(P.Bookings.ViewAll)
  const mayEdit = session.can(P.Bookings.Edit) && (mine || session.can(P.Bookings.EditAll))
  const mayCancel = session.can(P.Bookings.Delete) && (mine || session.can(P.Bookings.DeleteAll))
  const series = useBookings({ from: b.start }, !!b.seriesId)
  const laterInSeries = b.seriesId ? (series.data ?? []).filter((x) => x.seriesId === b.seriesId && x.id !== b.id).length : 0
  const [askSeries, setAskSeries] = useState(false)
  const open = state === 'scheduled' || state === 'in_progress'
  const showLeave = invited && open
  /* From an invitation's Refuse link: ask at once. */
  const [askLeave, setAskLeave] = useState(refuse && showLeave)

  let note = ''
  if (state === 'ended') note = t('detail.note.ended')
  else if (state === 'cancelled') note = t('detail.note.cancelled')
  else if (invited) note = t('detail.note.invitedBy', { name: b.ownerName })
  else if (state === 'in_progress') note = t('detail.note.inProgress')
  else if (!mine && seesOwner) note = t('detail.note.ownedBy', { name: b.ownerName })
  else if (!mine) note = t('detail.note.someoneElse')

  const onCancel = () => (laterInSeries > 0 ? setAskSeries(true) : actions.cancel(b))
  const leave = (wholeSeries: boolean) => actions.leave(b, wholeSeries).then((ok) => { if (ok) modals.close() })
  const showReschedule = mayEdit && state === 'scheduled'
  const showEndNow = mayEdit && state === 'in_progress'
  const showCancel = mayCancel && open

  return (
    <>
      <div style={{ display: 'flex', gap: 8, alignItems: 'center', marginBottom: 14, flexWrap: 'wrap' }}>
        <StatusPill item={b} />
        {b.seriesId && <span className="tag">{t('detail.repeating')}</span>}
      </div>
      <dl className="kv" style={{ marginBottom: 16 }}>
        <dt>{t('common.space')}</dt>
        <dd><bdi>{b.spaceName}</bdi>{space && <div style={{ fontSize: 11.5, color: 'var(--slate)', fontWeight: 400 }}>{t('detail.spacePlace', { building: space.buildingName, floor: space.floorName, tz: space.timeZone })}</div>}</dd>
        <dt>{t('detail.bookedBy')}</dt>
        <dd><bdi>{seesOwner ? b.ownerName : t('detail.someoneElse')}</bdi></dd>
        <dt>{t('common.start')}</dt><dd className="mono">{stampOffset(b.start)}</dd>
        <dt>{t('common.end')}</dt><dd className="mono">{stampOffset(b.end)}</dd>
        <dt>{t('detail.duration')}</dt><dd>{durationLabel((b.end.getTime() - b.start.getTime()) / 60000)}</dd>
        {(b.attendees.length > 0 || b.guestCount > 0) && <><dt>{t('detail.attendees')}</dt><dd><Attendees b={b} myId={session.userId} /></dd></>}
        <dt>{t('detail.created')}</dt><dd className="mono" style={{ fontWeight: 400 }}>{stamp(parseUtc(b.creationTime))}</dd>
        {b.lastModificationTime && <><dt>{t('detail.updated')}</dt><dd className="mono" style={{ fontWeight: 400 }}>{stamp(parseUtc(b.lastModificationTime))}</dd></>}
      </dl>
      {note && <p className="muted-box" style={{ margin: '0 0 14px' }}>{note}</p>}

      {askLeave && showLeave ? (
        <div className="muted-box" style={{ marginBottom: 18 }} role="alert">
          <p style={{ margin: '0 0 10px', color: 'var(--ink)' }}>
            {laterInSeries > 0 ? t('detail.leaveSeriesQuestion') : t('detail.leaveQuestion', { name: b.ownerName })}
          </p>
          <div style={{ display: 'flex', gap: 8, flexWrap: 'wrap' }}>
            <button type="button" className="btn btn-sm btn-danger" disabled={actions.busy} onClick={() => leave(false)}>
              {laterInSeries > 0 ? t('detail.onlyThis') : t('detail.leave')}
            </button>
            {laterInSeries > 0 && (
              <button type="button" className="btn btn-sm btn-danger" disabled={actions.busy} onClick={() => leave(true)}>{t('detail.thisAndLater')}</button>
            )}
            <button type="button" className="btn btn-sm" onClick={() => setAskLeave(false)}>{t('detail.stayInvited')}</button>
          </div>
        </div>
      ) : askSeries && showCancel ? (
        <div className="muted-box" style={{ marginBottom: 18 }}>
          <p style={{ margin: '0 0 10px', color: 'var(--ink)' }}>{t('detail.seriesQuestion')}</p>
          <div style={{ display: 'flex', gap: 8, flexWrap: 'wrap' }}>
            <button type="button" className="btn btn-sm btn-danger" disabled={actions.busy} onClick={() => actions.cancel(b).then(() => setAskSeries(false))}>{t('detail.onlyThis')}</button>
            <button type="button" className="btn btn-sm btn-danger" disabled={actions.busy} onClick={() => actions.cancelSeriesFrom(b).then(() => setAskSeries(false))}>{t('detail.thisAndLater')}</button>
            <button type="button" className="btn btn-sm" onClick={() => setAskSeries(false)}>{t('detail.keepIt')}</button>
          </div>
        </div>
      ) : (
        <div style={{ display: 'flex', gap: 8, marginBottom: 18 }}>
          {showReschedule && <button type="button" className="btn" onClick={() => modals.reschedule(b)}>{t('schedule.reschedule')}</button>}
          {showEndNow && <button type="button" className="btn" disabled={actions.busy} onClick={() => actions.endEarly(b)}>{t('schedule.endNow')}</button>}
          {showCancel && <button type="button" className="btn btn-danger" disabled={actions.busy} onClick={onCancel}>{t('detail.cancelBooking')}</button>}
          {showLeave && <button type="button" className="btn btn-danger" disabled={actions.busy} onClick={() => setAskLeave(true)}>{t('detail.leave')}</button>}
          {!showReschedule && !showEndNow && !showCancel && !showLeave && <span style={{ fontSize: 12, color: 'var(--slate-2)' }}>{t('detail.noActions')}</span>}
        </div>
      )}
    </>
  )
}

function MaintenanceDetail({ m }: { m: Maintenance }) {
  const { t } = useTranslation()
  const session = useSession()
  const actions = useBookingActions()
  const state = lifecycleOf(m)
  const label = state === 'cancelled' ? t('status.cancelled') : state === 'ended' ? t('status.ended') : state === 'in_progress' ? t('status.inProgress') : t('status.scheduled')
  const cls = state === 'cancelled' ? 'pill-cancelled' : state === 'ended' ? 'pill-ended' : state === 'in_progress' ? 'pill-inprog' : 'pill-confirmed'
  return (
    <>
      <div style={{ display: 'flex', gap: 8, alignItems: 'center', marginBottom: 14 }}>
        <span className={`pill ${cls}`}><span className="dot" />{label}</span>
        <span className="tag">{t(`detail.scopeBlocked.${m.scopeType}`)}</span>
        {m.seriesId && <span className="tag">{t('detail.repeating')}</span>}
      </div>
      <dl className="kv" style={{ marginBottom: 16 }}>
        <dt>{t('detail.scope')}</dt><dd><bdi>{m.scopeLabel}</bdi></dd>
        <dt>{t('common.space')}</dt><dd><bdi>{m.spaceName}</bdi></dd>
        <dt>{t('common.start')}</dt><dd className="mono">{stampOffset(m.start)}</dd>
        <dt>{t('common.end')}</dt><dd className="mono">{stampOffset(m.end)}</dd>
        <dt>{t('common.reason')}</dt><dd><bdi>{m.note || t('schedule.blocked')}</bdi></dd>
        <dt>{t('detail.created')}</dt><dd className="mono" style={{ fontWeight: 400 }}>{stamp(parseUtc(m.creationTime))}</dd>
      </dl>
      <div style={{ display: 'flex', gap: 8 }}>
        {session.can(P.Maintenance.Delete) && m.status === 'Active' && state !== 'ended'
          ? <button type="button" className="btn btn-danger" disabled={actions.busy} onClick={() => actions.cancelMaintenance(m.id)}>{t('detail.unblock')}</button>
          : <span style={{ fontSize: 12, color: 'var(--slate-2)' }}>{t('detail.noActions')}</span>}
      </div>
    </>
  )
}

export function DetailDrawer({ entity, id, refuse = false }: { entity: 'booking' | 'maintenance'; id: string; refuse?: boolean }) {
  const { t } = useTranslation()
  const booking = useBooking(entity === 'booking' ? id : null)
  const maint = useMaintenanceWindow(entity === 'maintenance' ? id : null)
  const b = booking.data
  const m = maint.data
  const title = entity === 'booking' ? t('detail.bookingTitle') : t('detail.blockedTitle')
  const subtitle = b ? `${b.spaceName} · ${dayKey(b.start)}` : m ? `${m.note || t('schedule.blocked')} · ${m.scopeLabel}` : ''
  const failed = booking.error || maint.error

  return (
    <Drawer title={title} subtitle={subtitle} onClose={modals.close}>
      {b ? <BookingDetail b={b} refuse={refuse} /> : m ? <MaintenanceDetail m={m} />
        /* A Refuse link used twice: the first time already took you off it. */
        : failed ? <p className="muted-box">{refuse ? t('detail.alreadyLeft') : t('detail.loadFailed')}</p> : <Loading />}
    </Drawer>
  )
}
