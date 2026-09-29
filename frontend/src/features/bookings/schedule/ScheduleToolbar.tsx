import { useBuildings, useFloors, useUsers } from '../../../api/hooks'
import type { Space } from '../../../api/types'
import { DatePicker, Dropdown } from '../../../components/pickers'
import { useSession } from '../../../app/session'
import { dayAt, dayName, monthName, todayKey } from '../../../lib/dateUtils'
import type { ScheduleId } from '../../../state/modalStore'
import { useScheduleStore, type ScheduleConfig, type ScheduleMode } from '../../../state/scheduleStore'

export function periodLabel(cfg: ScheduleConfig): string {
  const a = dayAt(cfg.from)
  const b = dayAt(cfg.to)
  const mn = (d: Date) => monthName(d).slice(0, 3)
  if (cfg.mode === 'day') return `${dayName(a).slice(0, 3)} ${a.getUTCDate()} ${mn(a)} ${a.getUTCFullYear()}`
  if (a.getUTCFullYear() === b.getUTCFullYear() && a.getUTCMonth() === b.getUTCMonth())
    return `${a.getUTCDate()}–${b.getUTCDate()} ${mn(a)} ${a.getUTCFullYear()}`
  if (a.getUTCFullYear() === b.getUTCFullYear()) return `${a.getUTCDate()} ${mn(a)} – ${b.getUTCDate()} ${mn(b)} ${a.getUTCFullYear()}`
  return `${a.getUTCDate()} ${mn(a)} ${a.getUTCFullYear()} – ${b.getUTCDate()} ${mn(b)} ${b.getUTCFullYear()}`
}

export function ScheduleToolbar({ id, spaces }: { id: ScheduleId; spaces: Space[] }) {
  const store = useScheduleStore()
  const cfg = store.configs[id]
  const session = useSession()
  const users = useUsers(session.isAdmin)
  const buildings = useBuildings().data ?? []
  const allFloors = useFloors().data ?? []
  /* Floor stays locked until a building is picked, then lists only that building's floors. */
  const floors = allFloors.filter((f) => f.buildingId === cfg.buildingId)
    .sort((a, b) => a.name.localeCompare(b.name, undefined, { numeric: true }))
  const inScope = (s: Space, buildingId: string, floorId: string) =>
    (!buildingId || s.buildingId === buildingId) && (!floorId || s.floorId === floorId)
  /* Only spaces in the chosen building and floor; a chosen space elsewhere goes back to "All my spaces". */
  const shownSpaces = spaces.filter((s) => inScope(s, cfg.buildingId, cfg.floorId))
  const keepSpace = (buildingId: string, floorId: string) =>
    cfg.spaceId === 'all' || spaces.some((s) => s.id === cfg.spaceId && inScope(s, buildingId, floorId)) ? cfg.spaceId : 'all'
  /* Space stays locked until a floor is picked, so clearing the building or floor also clears the space. */
  const pickBuilding = (buildingId: string) => store.patch(id, { buildingId, floorId: '', spaceId: 'all' })
  const pickFloor = (floorId: string) => store.patch(id, { floorId, spaceId: floorId ? keepSpace(cfg.buildingId, floorId) : 'all' })

  const building = (
    <div style={{ minWidth: 200 }}>
      <label className="lbl" htmlFor={`${id}-building`}>Building</label>
      <Dropdown id={`${id}-building`} value={cfg.buildingId} onChange={pickBuilding}
        options={[{ value: '', label: 'All buildings' }, ...buildings.map((b) => ({ value: b.id, label: b.name }))]} />
    </div>
  )
  const floor = (
    <div style={{ minWidth: 170 }}>
      <label className="lbl" htmlFor={`${id}-floor`}>Floor</label>
      <Dropdown id={`${id}-floor`} value={cfg.floorId} onChange={pickFloor} disabled={!cfg.buildingId} placeholder="Choose a building first"
        options={cfg.buildingId ? [{ value: '', label: 'All floors' }, ...floors.map((f) => ({ value: f.id, label: `Floor ${f.name}` }))] : []} />
    </div>
  )

  const views = (
    <div className="seg" role="group" aria-label="Schedule view">
      {(['month', 'week', 'day'] as ScheduleMode[]).map((m) => (
        <button key={m} type="button" className={cfg.mode === m ? 'active' : ''} onClick={() => store.setMode(id, m)}>
          {m[0].toUpperCase() + m.slice(1)}
        </button>
      ))}
    </div>
  )
  const period = (
    <div className="sched-period">
      <button type="button" className="iconbtn" onClick={() => store.shift(id, -1)} aria-label="Previous period" title="Previous">‹</button>
      <span className="mono period-label">{periodLabel(cfg)}</span>
      <button type="button" className="iconbtn" onClick={() => store.shift(id, 1)} aria-label="Next period" title="Next">›</button>
      <button type="button" className="btn btn-sm" onClick={() => store.setPeriod(id, todayKey())}>Today</button>
    </div>
  )

  /* Employees: the building and floor on top; Month / Week / Day on the left and the period on the far right under it. */
  if (!session.isAdmin) {
    return (
      <div className="sched-toolbar">
        <div className="sched-row sched-row-left">{building}{floor}</div>
        <div className="sched-row">{views}{period}</div>
      </div>
    )
  }

  /* Admins: Building, Floor, Space and User on top; under them Month / Week / Day on the left, the period in the middle and the From / To range on the far right. */
  return (
    <div className="sched-toolbar">
      <div className="sched-row sched-row-top">
        {building}
        {floor}
        <div style={{ minWidth: 230 }}>
          <label className="lbl" htmlFor="my-space">Space</label>
          <Dropdown id="my-space" value={cfg.floorId ? cfg.spaceId : ''} onChange={(v) => store.patch(id, { spaceId: v })}
            disabled={!cfg.floorId} placeholder="Choose a floor first"
            options={cfg.floorId ? [{ value: 'all', label: 'All spaces on this floor' }, ...shownSpaces.map((s) => ({ value: s.id, label: s.name }))] : []} />
        </div>
        <div>
          <label className="lbl" htmlFor="my-user">User</label>
          <Dropdown id="my-user" style={{ width: 170 }} value={cfg.userId ?? session.userId}
            onChange={(v) => store.patch(id, { userId: v === session.userId ? null : v })}
            options={users.data
              ? users.data.map((u) => ({ value: u.id, label: `${u.id === session.userId ? 'You' : u.name}${u.isAdmin && u.id !== session.userId ? ' (admin)' : ''}` }))
              : [{ value: session.userId, label: 'You' }]} />
        </div>
      </div>
      <div className="sched-row sched-row-spread">
        {views}
        {period}
        <div className="sched-range">
          <div>
            <label className="lbl" htmlFor={`${id}-from`}>From</label>
            <DatePicker id={`${id}-from`} style={{ width: 180 }} value={cfg.from}
              onChange={(v) => store.onRangeInput(id, v, cfg.to)} />
          </div>
          <div>
            <label className="lbl" htmlFor={`${id}-to`}>To</label>
            <DatePicker id={`${id}-to`} style={{ width: 180 }} value={cfg.to} min={cfg.from}
              onChange={(v) => store.onRangeInput(id, cfg.from, v)} />
          </div>
        </div>
      </div>
    </div>
  )
}
