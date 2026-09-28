import { useUsers } from '../../../api/hooks'
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

  return (
    <div className="sched-toolbar">
      {/* Space, user and date range are admin tools; employees move through their own schedule
          with the Month / Week / Day buttons and the arrows. */}
      {session.isAdmin && (
        <div style={{ minWidth: 230 }}>
          <label className="lbl" htmlFor="my-space">Space</label>
          <Dropdown id="my-space" value={cfg.spaceId} onChange={(v) => store.patch(id, { spaceId: v })}
            options={[{ value: 'all', label: 'All my spaces' }, ...spaces.map((s) => ({ value: s.id, label: s.name }))]} />
        </div>
      )}
      {session.isAdmin && (
        <div>
          <label className="lbl" htmlFor="my-user">User</label>
          <Dropdown id="my-user" style={{ width: 170 }} value={cfg.userId ?? session.userId}
            onChange={(v) => store.patch(id, { userId: v === session.userId ? null : v })}
            options={users.data
              ? users.data.map((u) => ({ value: u.id, label: `${u.id === session.userId ? 'You' : u.name}${u.isAdmin && u.id !== session.userId ? ' (admin)' : ''}` }))
              : [{ value: session.userId, label: 'You' }]} />
        </div>
      )}
      {session.isAdmin && (
        <>
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
        </>
      )}
      <div className="sched-nav">
        <div className="seg" role="group" aria-label="Schedule view">
          {(['month', 'week', 'day'] as ScheduleMode[]).map((m) => (
            <button key={m} type="button" className={cfg.mode === m ? 'active' : ''} onClick={() => store.setMode(id, m)}>
              {m[0].toUpperCase() + m.slice(1)}
            </button>
          ))}
        </div>
        <button type="button" className="iconbtn" onClick={() => store.shift(id, -1)} aria-label="Previous period" title="Previous">‹</button>
        <span className="mono period-label">{periodLabel(cfg)}</span>
        <button type="button" className="iconbtn" onClick={() => store.shift(id, 1)} aria-label="Next period" title="Next">›</button>
        <button type="button" className="btn btn-sm" onClick={() => store.setPeriod(id, todayKey())}>Today</button>
      </div>
    </div>
  )
}
