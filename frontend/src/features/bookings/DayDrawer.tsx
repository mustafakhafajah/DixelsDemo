import { lifecycleOf, type ScheduleItem } from '../../api/types'
import { useSession } from '../../app/session'
import { StatusPill } from '../../components/bits'
import { Drawer } from '../../components/Sheet'
import { dayAt, dayKey, dayName, durationLabel, hm, minLabel, monthName } from '../../lib/dateUtils'
import { computeFree, resourceDayBounds } from '../../lib/laneLayout'
import { modals, type ScheduleId } from '../../state/modalStore'
import { useScheduleData } from './schedule/useScheduleData'
import { openItem } from './schedule/ScheduleCalendar'
import { useBookingActions } from './useBookingActions'

export function DayDrawer({ dayKeyValue: key, scheduleId }: { dayKeyValue: string; scheduleId: ScheduleId }) {
  const data = useScheduleData(scheduleId)
  const session = useSession()
  const actions = useBookingActions()
  const { multiSpace, single } = data
  const d = dayAt(key)
  const items = data.items.filter((i) => dayKey(i.start) === key)

  const row = (i: ScheduleItem) => {
    const state = lifecycleOf(i)
    const isMaint = i.kind === 'maintenance'
    const mine = !isMaint && i.ownerUserId === session.userId
    const may = !isMaint && (mine || session.isAdmin)
    const primary = isMaint ? (multiSpace ? i.spaceName : i.note || 'Blocked') : multiSpace ? i.spaceName : mine ? 'You' : i.ownerName
    const secondary = isMaint
      ? multiSpace ? i.note || 'Blocked' : i.scopeLabel
      : multiSpace ? (mine ? 'You' : i.ownerName) : i.spaceName
    return (
      <div key={i.id} style={{ display: 'flex', gap: 10, alignItems: 'center', padding: '10px 0', borderBottom: '1px solid var(--line)', cursor: 'pointer' }}
        onClick={() => openItem(i)}>
        <span className="mono" style={{ fontSize: 12.5, fontWeight: 600, whiteSpace: 'nowrap' }}>{hm(i.start)}–{hm(i.end)}</span>
        <span style={{ flex: 1, minWidth: 0 }}>
          <span style={{ display: 'block', fontSize: 13, fontWeight: 600 }}>{primary}</span>
          <span style={{ display: 'block', fontSize: 11.5, color: 'var(--slate)' }}>{secondary}</span>
        </span>
        {isMaint ? <span className="pill pill-inactive"><span className="dot" />Blocked</span> : <StatusPill item={i} />}
        <span onClick={(e) => e.stopPropagation()} style={{ display: 'flex', gap: 6 }}>
          {isMaint && session.isAdmin && i.status === 'Active' && state !== 'ended' && (
            <button type="button" className="btn btn-sm btn-danger" disabled={actions.busy} onClick={() => actions.cancelMaintenance(i.id)}>Cancel</button>
          )}
          {!isMaint && may && state === 'scheduled' && <button type="button" className="btn btn-sm" onClick={() => modals.reschedule(i)}>Reschedule</button>}
          {!isMaint && may && state === 'in_progress' && <button type="button" className="btn btn-sm" disabled={actions.busy} onClick={() => actions.endEarly(i)}>End now</button>}
          {!isMaint && may && (state === 'scheduled' || state === 'in_progress') && (
            <button type="button" className="btn btn-sm btn-danger" disabled={actions.busy}
              onClick={() => (i.seriesId ? openItem(i) : actions.cancel(i))}>Cancel</button>
          )}
        </span>
      </div>
    )
  }

  let freeSection = null
  if (!multiSpace && single) {
    const bounds = resourceDayBounds(single.constraints, single.id, data.busyOnSpace, key)
    const windows = computeFree(single.constraints, single.id, data.busyOnSpace, key, bounds.start, bounds.end)
      .filter((w) => w.end - w.start >= single.constraints.minBookingMinutes)
    freeSection = (
      <>
        <h3 style={{ fontSize: 12.5, margin: '18px 0 8px' }}>Free windows</h3>
        <div style={{ display: 'flex', gap: 6, flexWrap: 'wrap' }}>
          {windows.length ? windows.map((w) => (
            <button key={w.start} type="button" className="btn btn-sm mono" style={{ fontSize: 11.5 }}
              onClick={() => modals.booking({ spaceId: single.id, start: dayAt(key, 0, w.start), end: dayAt(key, 0, Math.min(w.end, w.start + 240)) })}>
              {minLabel(w.start)}–{minLabel(w.end)} <span style={{ color: 'var(--slate)' }}>{durationLabel(w.end - w.start)}</span>
            </button>
          )) : <span style={{ fontSize: 12, color: 'var(--slate)' }}>Fully booked between {minLabel(bounds.start)} and {minLabel(bounds.end)}.</span>}
        </div>
      </>
    )
  } else if (multiSpace) {
    freeSection = <p style={{ fontSize: 11.5, color: 'var(--slate)', margin: '16px 0 0' }}>Pick one specific space to see its free windows here.</p>
  }

  return (
    <Drawer title={`${dayName(d)}, ${d.getUTCDate()} ${monthName(d)} ${d.getUTCFullYear()}`}
      subtitle={multiSpace ? 'Across all your spaces' : single?.name} onClose={modals.close}>
      <div>
        {items.length ? items.map(row) : <p style={{ fontSize: 12.5, color: 'var(--slate)', padding: '14px 0', margin: 0 }}>Nothing booked this day.</p>}
      </div>
      {freeSection}
      <button type="button" className="btn btn-primary" style={{ marginTop: 18, width: '100%' }}
        onClick={() => modals.booking({ spaceId: single?.id, start: dayAt(key, 9, 0), end: dayAt(key, 10, 0) })}>
        New booking on this day
      </button>
    </Drawer>
  )
}
