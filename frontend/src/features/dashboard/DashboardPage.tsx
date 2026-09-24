import { useMemo } from 'react'
import { useNavigate } from 'react-router-dom'
import { useBookings, useBuildings, useSpaces } from '../../api/hooks'
import { lifecycleOf, type Booking, type Building, type Space } from '../../api/types'
import { useSession } from '../../app/session'
import { Loading, StatusPill } from '../../components/bits'
import { addDays, addMin, dayAt, dayKey, hm, relative, roundUp30, stampOffset, todayKey } from '../../lib/dateUtils'
import { findOverlap } from '../../lib/laneLayout'
import { modals } from '../../state/modalStore'
import { useBookingActions } from '../bookings/useBookingActions'

function Bars({ rows, max, fmt }: { rows: { name: string; count: number }[]; max: number; fmt?: (n: number) => string }) {
  return (
    <div style={{ padding: '12px 16px' }}>
      {rows.map((r) => (
        <div key={r.name} className="bar-row">
          <span style={{ overflow: 'hidden', textOverflow: 'ellipsis', whiteSpace: 'nowrap' }}>{r.name}</span>
          <div className="bar-track"><div className="bar-fill" style={{ width: `${(r.count / max) * 100}%` }} /></div>
          <span className="mono" style={{ textAlign: 'right' }}>{fmt ? fmt(r.count) : r.count}</span>
        </div>
      ))}
      {!rows.length && <p style={{ fontSize: 12, color: 'var(--slate)' }}>No data yet.</p>}
    </div>
  )
}

function AdminStats({ all, spaces, buildings }: { all: Booking[]; spaces: Space[]; buildings: Building[] }) {
  const now = new Date()
  const weekAgo = addDays(now, -7)
  const confirmed = all.filter((b) => b.status === 'Confirmed')
  const util = spaces.filter((s) => s.status === 'Active').map((s) => {
    const hoursPerDay = Math.max(1, (s.constraints.closeMinute - s.constraints.openMinute) / 60)
    const mins = confirmed.filter((b) => b.spaceId === s.id && b.start >= weekAgo && b.start < now)
      .reduce((sum, b) => sum + (b.end.getTime() - b.start.getTime()) / 60000, 0)
    return { name: s.name, pct: Math.min(100, Math.round((mins / (7 * hoursPerDay * 60)) * 100)) }
  }).sort((a, b) => b.pct - a.pct)

  const count = (key: (b: Booking) => string | null | undefined) => {
    const m: Record<string, number> = {}
    confirmed.forEach((b) => { const k = key(b); if (k) m[k] = (m[k] ?? 0) + 1 })
    return m
  }
  const spaceBuilding = Object.fromEntries(spaces.map((s) => [s.id, s.buildingName]))
  const byBuilding = count((b) => spaceBuilding[b.spaceId])
  const bldgRows = buildings.map((b) => ({ name: b.name, count: byBuilding[b.name] ?? 0 })).sort((a, b) => b.count - a.count)
  const bldgMax = Math.max(1, ...bldgRows.map((r) => r.count))
  const cancelled = all.length - confirmed.length
  const cancelRate = all.length ? Math.round((cancelled / all.length) * 100) : 0
  const trend = Array.from({ length: 14 }, (_, i) => {
    const key = dayKey(addDays(now, i - 13))
    const day = all.filter((b) => dayKey(b.start) === key)
    return day.length ? Math.round((day.filter((b) => b.status === 'Cancelled').length / day.length) * 100) : 0
  })
  const trendMax = Math.max(1, ...trend)

  return (
    <>
      <h2 style={{ fontSize: 15, margin: '22px 0 12px' }}>Statistics</h2>
      <div style={{ display: 'grid', gridTemplateColumns: '1fr 1fr', gap: 16 }}>
        <div className="card">
          <div className="card-head"><div><h3 className="card-title">Space utilization</h3><p className="card-sub">Booked hours vs. available hours, last 7 days.</p></div></div>
          <Bars rows={util.map((u) => ({ name: u.name, count: u.pct }))} max={100} fmt={(n) => `${n}%`} />
        </div>
        <div className="card">
          <div className="card-head"><div><h3 className="card-title">Cancellation rate</h3><p className="card-sub">{cancelRate}% of all bookings ever made · daily rate, last 14 days</p></div></div>
          <div style={{ padding: '14px 16px' }}>
            <div className="trend-row">
              {trend.map((v, i) => (
                <div key={i} className={`trend-bar${v > trendMax * 0.7 ? ' high' : ''}`} style={{ height: Math.max(6, (v / trendMax) * 44) }} title={`${v}%`} />
              ))}
            </div>
            <p style={{ fontSize: 11, color: 'var(--slate-2)', margin: '8px 0 0' }}>14 days ago → today</p>
          </div>
        </div>
        <div className="card" style={{ gridColumn: '1 / -1' }}>
          <div className="card-head"><div><h3 className="card-title">Bookings by building</h3><p className="card-sub">All confirmed bookings, all time.</p></div></div>
          <Bars rows={bldgRows} max={bldgMax} />
        </div>
      </div>
    </>
  )
}

export function DashboardPage() {
  const session = useSession()
  const navigate = useNavigate()
  const actions = useBookingActions()
  const spacesQ = useSpaces()
  const buildingsQ = useBuildings()
  const weekAgo = useMemo(() => addDays(dayAt(todayKey()), -7), [])
  const nowWindow = useMemo(() => { const n = new Date(); return { from: n, to: addMin(n, 30) } }, [])

  const allQ = useBookings({ includeCancelled: true }, session.isAdmin)
  const mineQ = useBookings({ from: weekAgo, ownerUserId: session.userId }, !session.isAdmin && !!session.userId)
  const nowQ = useBookings(nowWindow)

  const scopeQ = session.isAdmin ? allQ : mineQ
  if (!scopeQ.data || !spacesQ.data) return <Loading />

  const now = new Date()
  const today = dayKey(now)
  const spaces = spacesQ.data
  const spaceById = Object.fromEntries(spaces.map((s) => [s.id, s]))
  const scope = scopeQ.data.filter((b) => b.status === 'Confirmed')
  const upcoming = scope.filter((b) => b.end > now).sort((a, b) => a.start.getTime() - b.start.getTime())
  const next = upcoming[0]
  const todayAll = scope.filter((b) => dayKey(b.start) === today)
  const todayMine = todayAll.filter((b) => b.ownerUserId === session.userId)
  const hoursWeek = scope.filter((b) => b.start >= addDays(now, -7)).reduce((s, b) => s + (b.end.getTime() - b.start.getTime()) / 3600000, 0)
  const bookable = spaces.filter((s) => s.canCurrentUserBook)
  const busyNow = nowQ.data ?? []
  const freeNow = bookable.filter((s) => !findOverlap(busyNow, s.id, now, addMin(now, 30))).length
  const hour = now.getUTCHours()
  const greet = hour < 12 ? 'Good morning' : hour < 18 ? 'Good afternoon' : 'Good evening'
  const firstName = session.name.split(/[\s.]+/).filter(Boolean).slice(-1)[0] ?? session.name
  const buildings = buildingsQ.data ?? []

  const stats: [string, string][] = session.isAdmin ? [
    [`${todayAll.length}`, 'Bookings today, estate-wide'],
    [`${freeNow}/${bookable.length}`, 'Spaces free right now'],
    [`${buildings.length}`, 'Buildings'],
    [`${spaces.filter((s) => s.status === 'Inactive').length}`, 'Inactive spaces'],
  ] : [
    [`${upcoming.length}`, 'Upcoming bookings'],
    [`${todayMine.length}`, 'Yours today'],
    [`${hoursWeek.toFixed(1)}h`, 'Reserved this week'],
    [`${freeNow}/${bookable.length}`, 'Free right now'],
  ]
  const list = (session.isAdmin ? todayAll : todayMine).sort((a, b) => a.start.getTime() - b.start.getTime())
  const canAct = (b: Booking) => b.ownerUserId === session.userId || session.isAdmin

  return (
    <section>
      <div style={{ marginBottom: 16 }}>
        <h2 style={{ fontSize: 20, margin: '0 0 3px', letterSpacing: '-.015em' }}>{greet}, {firstName}.</h2>
        <p style={{ fontSize: 13, color: 'var(--slate)' }}>
          {session.isAdmin
            ? `${todayAll.length} bookings across ${spaces.length} spaces in ${buildings.length} buildings today.`
            : next ? `Your next booking is ${relative(next.start)}.` : 'Nothing booked yet. Find a space to get started.'}
        </p>
      </div>

      <div style={{ display: 'grid', gridTemplateColumns: 'repeat(4,1fr)', gap: 12, marginBottom: 16 }}>
        {stats.map(([v, l]) => <div key={l} className="stat"><b>{v}</b><span>{l}</span></div>)}
      </div>

      <div style={{ display: 'grid', gridTemplateColumns: '1fr 1fr', gap: 16, alignItems: 'start' }}>
        <div style={{ display: 'flex', flexDirection: 'column', gap: 16 }}>
          {next ? (
            <div className="card" style={{ overflow: 'hidden' }}>
              <div className="card-head">
                <div><h3 className="card-title">Next up</h3><p className="card-sub">{relative(next.start)}</p></div>
                <StatusPill item={next} />
              </div>
              <div style={{ padding: '14px 16px' }}>
                <div style={{ fontSize: 16, fontWeight: 600, marginBottom: 3 }}>{next.spaceName}</div>
                <div style={{ fontSize: 12.5, color: 'var(--slate)', marginBottom: 11 }}>
                  {spaceById[next.spaceId]?.buildingName} · Floor {spaceById[next.spaceId]?.floorName}
                  {session.isAdmin && next.ownerUserId !== session.userId ? ` · ${next.ownerName}` : ''}
                </div>
                <div className="mono" style={{ fontSize: 13, marginBottom: 13 }}>{stampOffset(next.start)} → {hm(next.end)}</div>
                <div style={{ display: 'flex', gap: 8 }}>
                  <button type="button" className="btn btn-sm" onClick={() => modals.detail('booking', next.id)}>Open</button>
                  {canAct(next) && lifecycleOf(next) === 'scheduled' && <button type="button" className="btn btn-sm" onClick={() => modals.reschedule(next)}>Reschedule</button>}
                  {canAct(next) && (
                    <button type="button" className="btn btn-sm btn-danger" disabled={actions.busy}
                      onClick={() => (next.seriesId ? modals.detail('booking', next.id) : actions.cancel(next))}>Cancel</button>
                  )}
                </div>
              </div>
            </div>
          ) : (
            <div className="card" style={{ padding: '26px 18px', textAlign: 'center' }}>
              <p style={{ fontWeight: 600, margin: '0 0 4px' }}>No upcoming bookings</p>
              <p style={{ fontSize: 12.5, color: 'var(--slate)', margin: '0 0 14px' }}>Search for a free space and reserve it.</p>
              <button type="button" className="btn btn-accent btn-sm" onClick={() => navigate('/app/find')}>Find a space</button>
            </div>
          )}

          <div className="card" style={{ overflow: 'hidden' }}>
            <div className="card-head">
              <div>
                <h3 className="card-title">{session.isAdmin ? 'Everything booked today' : 'Your schedule today'}</h3>
                <p className="card-sub">{today} · UTC</p>
              </div>
            </div>
            {list.length ? list.map((b) => {
              const st = lifecycleOf(b)
              const past = st === 'ended'
              return (
                <div key={b.id} style={{ display: 'flex', gap: 12, alignItems: 'center', padding: '11px 16px', borderBottom: '1px solid var(--line)', cursor: 'pointer', opacity: past ? 0.6 : 1 }}
                  onClick={() => modals.detail('booking', b.id)}>
                  <span className="mono" style={{ fontSize: 12.5, fontWeight: 600 }}>{hm(b.start)}</span>
                  <span style={{ width: 3, height: 26, borderRadius: 2, background: st === 'in_progress' ? 'var(--copper)' : past ? 'var(--slate-2)' : 'var(--accent)' }} />
                  <span style={{ flex: 1, minWidth: 0 }}>
                    <span style={{ display: 'block', fontSize: 13, fontWeight: 600 }}>{b.spaceName}</span>
                    <span style={{ display: 'block', fontSize: 11.5, color: 'var(--slate)' }}>{hm(b.start)}–{hm(b.end)} · {b.ownerUserId === session.userId ? 'You' : b.ownerName}</span>
                  </span>
                  {st === 'in_progress' && <span className="pill pill-inprog"><span className="dot" />Now</span>}
                </div>
              )
            }) : <p style={{ padding: '24px 16px', textAlign: 'center', fontSize: 12.5, color: 'var(--slate)' }}>Nothing on the calendar today.</p>}
          </div>
        </div>

        <div className="card" style={{ overflow: 'hidden' }}>
          <div className="card-head">
            <div><h3 className="card-title">Free right now</h3><p className="card-sub">Available for at least the next 30 minutes.</p></div>
          </div>
          <div style={{ padding: '6px 0' }}>
            {bookable.map((s) => {
              const busy = findOverlap(busyNow, s.id, now, addMin(now, 30))
              return (
                <div key={s.id} style={{ display: 'flex', gap: 10, alignItems: 'center', padding: '9px 16px' }}>
                  <span style={{ width: 7, height: 7, borderRadius: 99, background: busy ? 'var(--rust)' : 'var(--accent)' }} />
                  <span style={{ flex: 1, minWidth: 0 }}>
                    <span style={{ display: 'block', fontSize: 13, fontWeight: 600 }}>{s.name}</span>
                    <span style={{ display: 'block', fontSize: 11.5, color: 'var(--slate)' }}>{busy ? `Busy until ${hm(busy.end)}` : 'Free now'}</span>
                  </span>
                  {!busy && (
                    <button type="button" className="btn btn-sm" onClick={() => {
                      const start = roundUp30(new Date())
                      modals.booking({ spaceId: s.id, start, end: addMin(start, 60) })
                    }}>Book 1h</button>
                  )}
                </div>
              )
            })}
            {!bookable.length && <p className="empty-note">No spaces are open to you right now.</p>}
          </div>
        </div>
      </div>

      {session.isAdmin && <AdminStats all={allQ.data ?? []} spaces={spaces} buildings={buildings} />}
    </section>
  )
}
