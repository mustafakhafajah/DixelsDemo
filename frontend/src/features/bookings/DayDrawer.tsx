import { useTranslation } from 'react-i18next'
import { lifecycleOf, type ScheduleItem } from '../../api/types'
import { useSession } from '../../app/session'
import { P } from '../../auth/permissions'
import { StatusPill } from '../../components/bits'
import { Drawer } from '../../components/Sheet'
import { isClosedDay } from '../../lib/closedDays'
import { dayAt, dayKey, durationLabel, formatDate, hm, minLabel } from '../../lib/dateUtils'
import { computeFree, resourceDayBounds } from '../../lib/laneLayout'
import { pressable } from '../../lib/pressable'
import { modals, type ScheduleId } from '../../state/modalStore'
import { useScheduleData } from './schedule/useScheduleData'
import { isInvited, openItem, opens } from './schedule/scheduleItems'
import { useBookingActions } from './useBookingActions'

export function DayDrawer({ dayKeyValue: key, scheduleId }: { dayKeyValue: string; scheduleId: ScheduleId }) {
  const { t } = useTranslation()
  const data = useScheduleData(scheduleId)
  const session = useSession()
  const actions = useBookingActions()
  const { multiSpace, single } = data
  const d = dayAt(key)
  const items = data.items.filter((i) => dayKey(i.start) === key)
  const canBook = session.can(P.Bookings.Create)
  /* Someone else's booking also needs EditAll / DeleteAll, as on the server. */
  const canEdit = (mine: boolean) => session.can(P.Bookings.Edit) && (mine || session.can(P.Bookings.EditAll))
  const canDelete = (mine: boolean) => session.can(P.Bookings.Delete) && (mine || session.can(P.Bookings.DeleteAll))

  const row = (i: ScheduleItem) => {
    const state = lifecycleOf(i)
    const isMaint = i.kind === 'maintenance'
    const mine = !isMaint && i.ownerUserId === session.userId
    const blocked = t('schedule.blocked')
    const you = t('common.you')
    const primary = isMaint ? (multiSpace ? i.spaceName : i.note || blocked) : multiSpace ? i.spaceName : mine ? you : i.ownerName
    const secondary = isMaint
      ? multiSpace ? i.note || blocked : i.scopeLabel
      : multiSpace ? (mine ? you : i.ownerName) : i.spaceName
    return (
      <div key={i.id} style={{ display: 'flex', gap: 10, alignItems: 'center', padding: '10px 0', borderBottom: '1px solid var(--line)', cursor: 'pointer' }}
        onClick={() => openItem(i)} {...(opens(i) ? pressable(() => openItem(i)) : {})}>
        <span className="mono" style={{ fontSize: 12.5, fontWeight: 600, whiteSpace: 'nowrap' }}>{hm(i.start)}–{hm(i.end)}</span>
        <span style={{ flex: 1, minWidth: 0 }}>
          <span style={{ display: 'block', fontSize: 13, fontWeight: 600 }}><bdi>{primary}</bdi></span>
          <span style={{ display: 'block', fontSize: 11.5, color: 'var(--slate)' }}><bdi>{secondary}</bdi></span>
        </span>
        {isMaint ? <span className="pill pill-inactive"><span className="dot" />{blocked}</span> : <StatusPill item={i} />}
        <span onClick={(e) => e.stopPropagation()} style={{ display: 'flex', gap: 6 }}>
          {isMaint && session.can(P.Maintenance.Delete) && i.status === 'Active' && state !== 'ended' && (
            <button type="button" className="btn btn-sm btn-danger" disabled={actions.busy} onClick={() => actions.cancelMaintenance(i.id)}>{t('common.cancel')}</button>
          )}
          {!isMaint && canEdit(mine) && state === 'scheduled' && <button type="button" className="btn btn-sm" onClick={() => modals.reschedule(i)}>{t('schedule.reschedule')}</button>}
          {!isMaint && canEdit(mine) && state === 'in_progress' && <button type="button" className="btn btn-sm" disabled={actions.busy} onClick={() => actions.endEarly(i)}>{t('schedule.endNow')}</button>}
          {!isMaint && canDelete(mine) && (state === 'scheduled' || state === 'in_progress') && (
            <button type="button" className="btn btn-sm btn-danger" disabled={actions.busy}
              onClick={() => (i.seriesId ? openItem(i) : actions.cancel(i))}>{t('common.cancel')}</button>
          )}
          {/* Invited: the only action is leaving, asked to confirm in the booking's details. */}
          {!isMaint && isInvited(i, session.userId) && (state === 'scheduled' || state === 'in_progress') && (
            <button type="button" className="btn btn-sm btn-danger" onClick={() => modals.detail('booking', i.id, true)}>{t('detail.leave')}</button>
          )}
        </span>
      </div>
    )
  }

  let freeSection = null
  /* Free windows only lead to a booking, so they are not offered to someone who cannot book. */
  if (canBook && !multiSpace && single) {
    const bounds = resourceDayBounds(single.constraints, single.id, data.busyOnSpace, key)
    const windows = computeFree(single.constraints, single.id, data.busyOnSpace, key, bounds.start, bounds.end)
      .filter((w) => w.end - w.start >= single.constraints.minBookingMinutes)
    freeSection = (
      <>
        <h3 style={{ fontSize: 12.5, margin: '18px 0 8px' }}>{t('schedule.freeWindows')}</h3>
        <div style={{ display: 'flex', gap: 6, flexWrap: 'wrap' }}>
          {windows.length ? windows.map((w) => (
            <button key={w.start} type="button" className="btn btn-sm mono" style={{ fontSize: 11.5 }}
              onClick={() => modals.booking({ spaceId: single.id, start: dayAt(key, 0, w.start), end: dayAt(key, 0, Math.min(w.end, w.start + 240)) })}>
              {minLabel(w.start)}–{minLabel(w.end)} <span style={{ color: 'var(--slate)' }}>{durationLabel(w.end - w.start)}</span>
            </button>
          )) : <span style={{ fontSize: 12, color: 'var(--slate)' }}>{isClosedDay(single.constraints, key)
            ? t('schedule.buildingClosedDay')
            : t('schedule.fullyBooked', { from: minLabel(bounds.start), to: minLabel(bounds.end) })}</span>}
        </div>
      </>
    )
  } else if (canBook && multiSpace) {
    freeSection = <p style={{ fontSize: 11.5, color: 'var(--slate)', margin: '16px 0 0' }}>{t('schedule.pickOneSpace')}</p>
  }

  return (
    <Drawer title={formatDate(d, { weekday: 'long', day: 'numeric', month: 'long', year: 'numeric' })}
      subtitle={multiSpace ? t('schedule.acrossSpaces') : single?.name} onClose={modals.close}>
      <div>
        {items.length ? items.map(row) : <p style={{ fontSize: 12.5, color: 'var(--slate)', padding: '14px 0', margin: 0 }}>{t('schedule.nothingBooked')}</p>}
      </div>
      {freeSection}
      {canBook && (
        <button type="button" className="btn btn-primary" style={{ marginTop: 18, width: '100%' }}
          onClick={() => modals.booking({ spaceId: single?.id, start: dayAt(key, 9, 0), end: dayAt(key, 10, 0) })}>
          {t('schedule.newBookingOnDay')}
        </button>
      )}
    </Drawer>
  )
}
