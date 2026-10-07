import { useEffect, useRef, useState } from 'react'
import { useTranslation } from 'react-i18next'
import { useBooking, useBookings, useMaintenanceWindow, useSpaces } from '../../api/hooks'
import { lifecycleOf, myResponse, type AttendeeResponse, type Booking, type Maintenance, type Reply } from '../../api/types'
import { useSession } from '../../app/session'
import { P } from '../../auth/permissions'
import { Loading, StatusPill } from '../../components/bits'
import { CancelMessageFields, type CancelMessageValue } from '../../components/CancelMessageFields'
import { bookingCancelDefaults } from '../../components/cancelMessage'
import { Drawer } from '../../components/Sheet'
import { dayKey, durationLabel, parseUtc, stamp } from '../../lib/dateUtils'
import { modals } from '../../state/modalStore'
import { ResponseIcon } from './Replies'
import { useBookingActions } from './useBookingActions'

const SUMMARY_ORDER: AttendeeResponse[] = ['accepted', 'tentative', 'declined', 'none']
const RADIO_ROW = { border: 0, margin: '0 0 12px', padding: 0, display: 'flex', gap: 14, flexWrap: 'wrap', fontSize: 12.5 } as const
const RADIO = { display: 'flex', gap: 6, alignItems: 'center', cursor: 'pointer' } as const

/* Who is invited: colleagues by name (you as "You"), then the outside guests: their addresses for the owner and
 * admins, otherwise (and once the booking is over and they're wiped) just how many. With showReplies (the owner and
 * admins) everyone's answer is shown too, guests' included, under a one-line count, like a Teams meeting's tracking. */
function Attendees({ b, myId, showReplies }: { b: Booking; myId: string; showReplies: boolean }) {
  const { t } = useTranslation()
  const hiddenGuests = b.guestCount - b.guests.length
  const counts: Record<AttendeeResponse, number> = { accepted: 0, tentative: 0, declined: 0, none: 0 }
  for (const a of [...b.attendees, ...b.guests]) counts[a.response ?? 'none']++
  const summary = SUMMARY_ORDER.filter((r) => counts[r] > 0).map((r) => t(`detail.replySummary.${r}`, { count: counts[r] })).join(' · ')
  return (
    <>
      {showReplies && summary && <div style={{ fontSize: 11.5, color: 'var(--slate)', fontWeight: 400, marginBottom: 4 }}>{summary}</div>}
      <ul style={{ listStyle: 'none', margin: 0, padding: 0, display: 'flex', flexDirection: 'column', gap: 2 }}>
        {b.attendees.map((a) => (
          <li key={a.userId} style={{ display: 'flex', alignItems: 'center', gap: 6, flexWrap: 'wrap' }}>
            {showReplies && <ResponseIcon response={a.response ?? 'none'} />}
            <bdi>{a.userId === myId ? t('common.you') : a.name}</bdi>
            {showReplies && <span style={{ fontSize: 11.5, color: 'var(--slate)', fontWeight: 400 }}>{t(`detail.response.${a.response ?? 'none'}`)}</span>}
          </li>
        ))}
        {b.guests.map((g) => (
          <li key={g.email} style={{ display: 'flex', alignItems: 'center', gap: 6, flexWrap: 'wrap' }}>
            {showReplies && <ResponseIcon response={g.response ?? 'none'} />}
            <bdi dir="ltr">{g.email}</bdi> <span className="tag">{t('booking.attendees.guest')}</span>
            {showReplies && <span style={{ fontSize: 11.5, color: 'var(--slate)', fontWeight: 400 }}>{t(`detail.response.${g.response ?? 'none'}`)}</span>}
          </li>
        ))}
        {hiddenGuests > 0 && (
          <li style={{ color: 'var(--slate)', fontWeight: 400 }}>
            {t(b.guests.length || b.attendees.length ? 'detail.moreGuests' : 'detail.guests', { count: hiddenGuests })}
          </li>
        )}
      </ul>
    </>
  )
}

/* An invited person's answer, shown only: answering and changing it happen through the Accept / Tentative / Decline
 * buttons in the invitation email. */
function YourAnswer({ current }: { current: AttendeeResponse }) {
  const { t } = useTranslation()
  return (
    <div className="muted-box" style={{ marginBottom: 18, color: 'var(--ink)' }}>
      <p style={{ margin: 0, display: 'flex', alignItems: 'center', gap: 6 }}>
        <ResponseIcon response={current} />
        {current === 'none' ? t('detail.reply.notAnswered') : t('detail.reply.yourAnswer', { answer: t(`detail.response.${current}`) })}
      </p>
      <p style={{ margin: '6px 0 0', fontSize: 12, color: 'var(--slate)' }}>{t('detail.reply.viaEmail')}</p>
    </div>
  )
}

function BookingDetail({ b, respond, respondSeries, startCancel }: { b: Booking; respond?: Reply; respondSeries: boolean; startCancel: boolean }) {
  const { t } = useTranslation()
  const session = useSession()
  const spaces = useSpaces()
  const actions = useBookingActions()
  const space = spaces.data?.find((s) => s.id === b.spaceId)
  const state = lifecycleOf(b)
  const mine = b.ownerUserId === session.userId
  /* Your answer when you're invited (declined included: it stays on your list and can be answered again). */
  const reply = mine ? null : myResponse(b, session.userId)
  const invited = reply !== null
  /* Mirrors the server: someone else's booking needs ViewAll to see who made it, EditAll to change it and
   * DeleteAll to cancel it, on top of the own-booking Edit / Delete. People invited see who booked it, and may
   * only answer the invitation. */
  const seesOwner = mine || invited || session.can(P.Bookings.ViewAll)
  const mayEdit = session.can(P.Bookings.Edit) && (mine || session.can(P.Bookings.EditAll))
  const mayCancel = session.can(P.Bookings.Delete) && (mine || session.can(P.Bookings.DeleteAll))
  const series = useBookings({ from: b.start }, !!b.seriesId)
  const laterInSeries = b.seriesId ? (series.data ?? []).filter((x) => x.seriesId === b.seriesId && x.id !== b.id).length : 0
  const open = state === 'scheduled' || state === 'in_progress'
  const showReply = invited && open
  const showReschedule = mayEdit && state === 'scheduled'
  const showEndNow = mayEdit && state === 'in_progress'
  const showCancel = mayCancel && open

  /* Cancelling always asks first, with the email everyone on the booking gets (and for a series, how much goes). */
  const [askCancel, setAskCancel] = useState(startCancel)
  const [seriesScope, setSeriesScope] = useState<'one' | 'later'>('one')
  /* null until edited, so the ready-made text follows the language in use. */
  const [message, setMessage] = useState<CancelMessageValue | null>(null)
  const shownMessage = message ?? bookingCancelDefaults(b, session.name)
  const confirmCancel = () => {
    const run = laterInSeries > 0 && seriesScope === 'later' ? actions.cancelSeriesFrom(b, shownMessage) : actions.cancel(b, shownMessage)
    void run.then((ok) => { if (ok) setAskCancel(false) })
  }

  /* From an invitation email's Accept / Tentative / Decline link (the only place to answer): recorded once, for the
   * later dates too when the invitation was for a repeating booking. */
  const replied = useRef(false)
  useEffect(() => {
    if (!respond || replied.current || !showReply) return
    replied.current = true
    void actions.respond(b, respond, respondSeries)
  }, [respond, respondSeries, showReply, actions, b])

  let note = ''
  if (state === 'ended') note = t('detail.note.ended')
  else if (state === 'cancelled') note = t('detail.note.cancelled')
  else if (invited) note = t('detail.note.invitedBy', { name: b.ownerName })
  else if (state === 'in_progress') note = t('detail.note.inProgress')
  else if (!mine && seesOwner) note = t('detail.note.ownedBy', { name: b.ownerName })
  else if (!mine) note = t('detail.note.someoneElse')

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
        <dt>{t('common.start')}</dt><dd className="mono">{stamp(b.start)}</dd>
        <dt>{t('common.end')}</dt><dd className="mono">{stamp(b.end)}</dd>
        <dt>{t('detail.duration')}</dt><dd>{durationLabel((b.end.getTime() - b.start.getTime()) / 60000)}</dd>
        {(b.attendees.length > 0 || b.guestCount > 0) && (
          <><dt>{t('detail.attendees')}</dt><dd><Attendees b={b} myId={session.userId} showReplies={mine || session.can(P.Bookings.ViewAll)} /></dd></>
        )}
        <dt>{t('detail.created')}</dt><dd className="mono" style={{ fontWeight: 400 }}>{stamp(parseUtc(b.creationTime))}</dd>
        {b.lastModificationTime && <><dt>{t('detail.updated')}</dt><dd className="mono" style={{ fontWeight: 400 }}>{stamp(parseUtc(b.lastModificationTime))}</dd></>}
      </dl>
      {note && <p className="muted-box" style={{ margin: '0 0 14px' }}>{note}</p>}

      {showReply && <YourAnswer current={reply} />}

      {askCancel && showCancel ? (
        <div className="muted-box" style={{ marginBottom: 18, color: 'var(--ink)' }}>
          <p style={{ margin: '0 0 10px' }}>{laterInSeries > 0 ? t('detail.seriesQuestion') : t('detail.cancelQuestion')}</p>
          {laterInSeries > 0 && (
            <fieldset style={RADIO_ROW} aria-label={t('detail.seriesQuestion')}>
              <label style={RADIO}>
                <input type="radio" name={`cancel-scope-${b.id}`} checked={seriesScope === 'one'} onChange={() => setSeriesScope('one')} />{t('detail.onlyThis')}
              </label>
              <label style={RADIO}>
                <input type="radio" name={`cancel-scope-${b.id}`} checked={seriesScope === 'later'} onChange={() => setSeriesScope('later')} />{t('detail.thisAndLater')}
              </label>
            </fieldset>
          )}
          <CancelMessageFields idPrefix={`cancel-${b.id}`} value={shownMessage} onChange={setMessage} />
          <div style={{ display: 'flex', gap: 8, flexWrap: 'wrap', marginTop: 12 }}>
            <button type="button" className="btn btn-sm btn-danger" disabled={actions.busy} onClick={confirmCancel}>{t('detail.cancelBooking')}</button>
            <button type="button" className="btn btn-sm" onClick={() => setAskCancel(false)}>{t('detail.keepIt')}</button>
          </div>
        </div>
      ) : (
        <div style={{ display: 'flex', gap: 8, marginBottom: 18 }}>
          {showReschedule && <button type="button" className="btn" onClick={() => modals.reschedule(b)}>{t('schedule.reschedule')}</button>}
          {showEndNow && <button type="button" className="btn" disabled={actions.busy} onClick={() => actions.endEarly(b)}>{t('schedule.endNow')}</button>}
          {showCancel && <button type="button" className="btn btn-danger" disabled={actions.busy} onClick={() => setAskCancel(true)}>{t('detail.cancelBooking')}</button>}
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
        <dt>{t('common.start')}</dt><dd className="mono">{stamp(m.start)}</dd>
        <dt>{t('common.end')}</dt><dd className="mono">{stamp(m.end)}</dd>
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

/* respond: an answer to record straight away (from an invitation email; respondSeries: for the later dates too);
 * cancel: start on the cancel step. */
export function DetailDrawer({ entity, id, respond, respondSeries = false, cancel = false }: {
  entity: 'booking' | 'maintenance'
  id: string
  respond?: Reply
  respondSeries?: boolean
  cancel?: boolean
}) {
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
      {b ? <BookingDetail b={b} respond={respond} respondSeries={respondSeries} startCancel={cancel} /> : m ? <MaintenanceDetail m={m} />
        /* An invitation link for a booking you can no longer see: you were taken off it, or it is gone. */
        : failed ? <p className="muted-box">{respond ? t('detail.cantRespond') : t('detail.loadFailed')}</p> : <Loading />}
    </Drawer>
  )
}
