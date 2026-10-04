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
import { useBookingActions } from './useBookingActions'

function BookingDetail({ b }: { b: Booking }) {
  const { t } = useTranslation()
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
  if (state === 'ended') note = t('detail.note.ended')
  else if (state === 'cancelled') note = t('detail.note.cancelled')
  else if (state === 'in_progress') note = t('detail.note.inProgress')
  else if (!mine && session.isAdmin) note = t('detail.note.ownedBy', { name: b.ownerName })
  else if (!mine) note = t('detail.note.someoneElse')

  const onCancel = () => (laterInSeries > 0 ? setAskSeries(true) : actions.cancel(b))
  const showReschedule = may && session.can(P.Bookings.Edit) && state === 'scheduled'
  const showEndNow = may && session.can(P.Bookings.Edit) && state === 'in_progress'
  const showCancel = may && session.can(P.Bookings.Delete) && (state === 'scheduled' || state === 'in_progress')

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
        <dd><bdi>{may ? b.ownerName : t('detail.someoneElse')}</bdi></dd>
        <dt>{t('common.start')}</dt><dd className="mono">{stampOffset(b.start)}</dd>
        <dt>{t('common.end')}</dt><dd className="mono">{stampOffset(b.end)}</dd>
        <dt>{t('detail.duration')}</dt><dd>{durationLabel((b.end.getTime() - b.start.getTime()) / 60000)}</dd>
        <dt>{t('detail.created')}</dt><dd className="mono" style={{ fontWeight: 400 }}>{stamp(parseUtc(b.creationTime))}</dd>
        {b.lastModificationTime && <><dt>{t('detail.updated')}</dt><dd className="mono" style={{ fontWeight: 400 }}>{stamp(parseUtc(b.lastModificationTime))}</dd></>}
      </dl>
      {note && <p className="muted-box" style={{ margin: '0 0 14px' }}>{note}</p>}

      {askSeries && showCancel ? (
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
          {!showReschedule && !showEndNow && !showCancel && <span style={{ fontSize: 12, color: 'var(--slate-2)' }}>{t('detail.noActions')}</span>}
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

export function DetailDrawer({ entity, id }: { entity: 'booking' | 'maintenance'; id: string }) {
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
      {b ? <BookingDetail b={b} /> : m ? <MaintenanceDetail m={m} /> : failed ? <p className="muted-box">{t('detail.loadFailed')}</p> : <Loading />}
    </Drawer>
  )
}
