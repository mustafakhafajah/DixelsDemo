import { useUsers } from '../../../api/hooks'
import type { Space } from '../../../api/types'
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
  const users = useUsers(id === 'my' && session.isAdmin)

  return (
    <div className="sched-toolbar">
      {id === 'my' ? (
        <>
          <div style={{ minWidth: 230 }}>
            <label className="lbl" htmlFor="my-space">Space</label>
            <select id="my-space" className="inp" value={cfg.spaceId} onChange={(e) => store.patch(id, { spaceId: e.target.value })}>
              <option value="all">All my spaces</option>
              {spaces.map((s) => <option key={s.id} value={s.id}>{s.name}</option>)}
            </select>
          </div>
          {session.isAdmin && (
            <div>
              <label className="lbl" htmlFor="my-user">User</label>
              <select id="my-user" className="inp" style={{ width: 170 }} value={cfg.userId ?? session.userId}
                onChange={(e) => store.patch(id, { userId: e.target.value === session.userId ? null : e.target.value })}>
                {(users.data ?? []).map((u) => (
                  <option key={u.id} value={u.id}>{u.id === session.userId ? 'You' : u.name}{u.isAdmin && u.id !== session.userId ? ' (admin)' : ''}</option>
                ))}
                {!users.data && <option value={session.userId}>You</option>}
              </select>
            </div>
          )}
        </>
      ) : (
        <div style={{ minWidth: 230 }}>
          <label className="lbl" htmlFor="adm-space">Inspect a space's schedule</label>
          <select id="adm-space" className="inp" value={cfg.spaceId} onChange={(e) => store.patch(id, { spaceId: e.target.value })}>
            <option value="">Choose a space…</option>
            {spaces.map((s) => <option key={s.id} value={s.id}>{s.name}{s.status !== 'Active' ? ' — inactive' : ''}</option>)}
          </select>
        </div>
      )}
      <div>
        <label className="lbl" htmlFor={`${id}-from`}>From</label>
        <input type="date" id={`${id}-from`} className="inp mono" style={{ width: 150 }} value={cfg.from}
          onChange={(e) => store.onRangeInput(id, e.target.value, cfg.to)} />
      </div>
      <div>
        <label className="lbl" htmlFor={`${id}-to`}>To</label>
        <input type="date" id={`${id}-to`} className="inp mono" style={{ width: 150 }} value={cfg.to}
          onChange={(e) => store.onRangeInput(id, cfg.from, e.target.value)} />
      </div>
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
