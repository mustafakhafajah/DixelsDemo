import { useMemo } from 'react'
import { useBookings, useBuildings, useFloors, useMaintenance, useSpaces } from '../../api/hooks'
import type { ScheduleItem, Space } from '../../api/types'
import { useSession } from '../../app/session'
import { Loading, plural } from '../../components/bits'
import { DEFAULT_MIN_MINUTES, RT_PX_PER_HOUR } from '../../lib/constants'
import { addDays, addMin, dayAt, dayKey, dayName, hm, minLabel, minOfDay, monthName, todayKey } from '../../lib/dateUtils'
import { candidatesDayBounds, computeFree, daySegment, findOverlap, validateWindowLocal, type DaySegment, type MinuteWindow } from '../../lib/laneLayout'
import { findToday, useFindStore, type FindDuration } from '../../state/findStore'
import { modals } from '../../state/modalStore'
import { itemClass, openItem } from '../bookings/schedule/ScheduleCalendar'

const px = (min: number) => (min / 60) * RT_PX_PER_HOUR

function FindFilters({ spaces }: { spaces: Space[] }) {
  const f = useFindStore()
  const buildings = useBuildings().data ?? []
  const floors = useFloors().data ?? []
  const floorNames = [...new Set(floors.filter((x) => x.isBookable && (!f.buildingId || x.buildingId === f.buildingId)).map((x) => x.name))]
    .sort((a, b) => a.localeCompare(b, undefined, { numeric: true }))
  /* Only types some space actually has, as id → name. */
  const types = [...new Map(spaces.map((s) => [s.typeId, s.typeName])).entries()].sort((a, b) => a[1].localeCompare(b[1]))
  const endMin = (((f.customEnd ?? f.time + 60) % 1440) + 1440) % 1440
  const setDur = (d: FindDuration) => f.patch({ duration: d, customEnd: d === 'custom' && f.customEnd == null ? f.time + 60 : f.customEnd })

  return (
    <aside className="card find-filters">
      <div>
        <h3>When</h3>
        {/* Stacked, one per row, so neither field is squeezed in the narrow filter column. */}
        <div style={{ display: 'flex', flexDirection: 'column', gap: 8 }}>
          <input type="date" className="inp mono" value={f.date} aria-label="Date"
            onChange={(e) => f.patch({ date: e.target.value || todayKey() })} />
          <input type="time" className="inp mono" value={minLabel(f.time)} aria-label="Start time"
            onChange={(e) => { const [h, m] = (e.target.value || '09:00').split(':').map(Number); f.patch({ time: h * 60 + m }) }} />
        </div>
        <div className="dur-row" style={{ marginTop: 10 }}>
          <button type="button" className="dur-chip" onClick={f.now}>Now</button>
          {([30, 60, 120, 'custom'] as FindDuration[]).map((d) => (
            <button key={d} type="button" className={`dur-chip${f.duration === d ? ' active' : ''}`} onClick={() => setDur(d)}>
              {d === 30 ? '30 min' : d === 60 ? '1h' : d === 120 ? '2h' : 'Custom'}
            </button>
          ))}
        </div>
        {f.duration === 'custom' && (
          <div style={{ marginTop: 8 }}>
            <label className="lbl" htmlFor="fv-end">End</label>
            <input type="time" id="fv-end" className="inp mono" value={minLabel(endMin)}
              onChange={(e) => { const [h, m] = (e.target.value || '10:00').split(':').map(Number); f.patch({ customEnd: h * 60 + m }) }} />
          </div>
        )}
      </div>
      <div>
        <h3>Room criteria</h3>
        <div style={{ display: 'flex', flexDirection: 'column', gap: 10 }}>
          <div>
            <label className="lbl" htmlFor="fv-building">Building</label>
            <select id="fv-building" className="inp" value={f.buildingId} onChange={(e) => f.patch({ buildingId: e.target.value, floorName: '' })}>
              <option value="">All buildings</option>
              {buildings.map((b) => <option key={b.id} value={b.id}>{b.name}</option>)}
            </select>
          </div>
          <div>
            <label className="lbl" htmlFor="fv-floor">Floor</label>
            <select id="fv-floor" className="inp" value={floorNames.includes(f.floorName) ? f.floorName : ''} onChange={(e) => f.patch({ floorName: e.target.value })}>
              <option value="">All floors</option>
              {floorNames.map((n) => <option key={n} value={n}>{n}</option>)}
            </select>
          </div>
          <div>
            <label className="lbl" htmlFor="fv-capacity">Capacity</label>
            <select id="fv-capacity" className="inp" value={f.minCapacity} onChange={(e) => f.patch({ minCapacity: Number(e.target.value) })}>
              <option value={0}>Any capacity</option>
              {[4, 6, 8, 12].map((n) => <option key={n} value={n}>{n}+</option>)}
            </select>
          </div>
          <div>
            <label className="lbl">Space type</label>
            <div style={{ display: 'flex', flexWrap: 'wrap', gap: 6 }}>
              {types.map(([id, name]) => (
                <button key={id} type="button" className={`type-pill${f.types.includes(id) ? ' active' : ''}`} onClick={() => f.toggleType(id)}>
                  {name}
                </button>
              ))}
            </div>
          </div>
          <div>
            <label className="lbl" htmlFor="fv-query">Search rooms</label>
            <input id="fv-query" className="inp" placeholder="Room name…" value={f.query} onChange={(e) => f.patch({ query: e.target.value })} />
          </div>
        </div>
      </div>
    </aside>
  )
}

export function FindSpacePage() {
  const f = useFindStore()
  const { userId } = useSession()
  const spacesQ = useSpaces()
  const dayFrom = useMemo(() => dayAt(f.date), [f.date])
  const dayTo = useMemo(() => addDays(dayAt(f.date), 1), [f.date])
  const bookingsQ = useBookings({ from: dayFrom, to: dayTo })
  const maintQ = useMaintenance({ from: dayFrom, to: dayTo })
  const spaces = useMemo(() => spacesQ.data ?? [], [spacesQ.data])

  const items: ScheduleItem[] = useMemo(() => [...(bookingsQ.data ?? []), ...(maintQ.data ?? [])], [bookingsQ.data, maintQ.data])

  const q = f.query.trim().toLowerCase()
  const candidates = spaces
    .filter((s) => s.canCurrentUserBook)
    .filter((s) => !f.buildingId || s.buildingId === f.buildingId)
    .filter((s) => !f.floorName || s.floorName === f.floorName)
    .filter((s) => !f.minCapacity || s.capacity >= f.minCapacity)
    .filter((s) => !f.types.length || f.types.includes(s.typeId))
    .filter((s) => !q || s.name.toLowerCase().includes(q))

  const durMin = f.duration === 'custom' ? Math.max(DEFAULT_MIN_MINUTES, (f.customEnd ?? f.time + 60) - f.time) : f.duration
  const winStart = dayAt(f.date, 0, f.time)
  const winEnd = addMin(winStart, durMin)
  const isFree = (s: Space) => !findOverlap(items, s.id, winStart, winEnd) &&
    !validateWindowLocal(s.constraints, s.name, winStart, winEnd, true)
  const freeCount = candidates.filter(isFree).length
  const d = dayAt(f.date)

  const regionPrefill = (s: Space, region: MinuteWindow) => {
    const wS = minOfDay(winStart)
    const wE = Math.min(1440, wS + durMin)
    const fits = wE > wS && wS >= region.start && wE <= region.end
    const start = fits ? wS : region.start
    const end = fits ? wE : Math.min(region.end, start + Math.max(60, s.constraints.minBookingMinutes))
    modals.booking({ spaceId: s.id, start: dayAt(f.date, 0, start), end: dayAt(f.date, 0, end) })
  }

  let body
  if (!spacesQ.data) body = <Loading />
  else if (!candidates.length) {
    body = (
      <div className="sched-empty">
        <p>No rooms match these filters</p>
        <p>Widen the room criteria, or pick a different building or floor.</p>
      </div>
    )
  } else {
    const { start: open, end: close } = candidatesDayBounds(candidates, items, f.date)
    const trackW = px(close - open)
    const ticks: number[] = []
    for (let m = open; m < close; m += 60) ticks.push(m)
    const now = new Date()
    const nowMin = minOfDay(now)
    const isToday = f.date === dayKey(now)
    const wS = Math.max(open, minOfDay(winStart))
    const wE = Math.min(close, wS + durMin)
    const band = wE > wS ? <div className="rt-band" style={{ left: px(wS - open), width: px(wE - wS) }} /> : null
    const nowLine = isToday && nowMin >= open && nowMin <= close ? <div className="rt-now" style={{ left: px(nowMin - open) }} /> : null

    const groups = new Map<string, Space[]>()
    candidates.forEach((s) => {
      const k = `${s.buildingName}||${s.floorName}`
      groups.set(k, [...(groups.get(k) ?? []), s])
    })
    const keys = [...groups.keys()].sort((a, b) => a.localeCompare(b, undefined, { numeric: true }))

    body = (
      <div className="rt-wrap">
        <div className="rt-header">
          <div className="rt-roominfo" style={{ border: 'none', background: 'none', position: 'static' }} />
          <div className="rt-ruler" style={{ width: trackW }}>
            {ticks.map((m) => <div key={m} className="rt-tick" style={{ left: px(m - open) }}>{minLabel(m)}</div>)}
            {band}{nowLine}
          </div>
        </div>
        {keys.map((k) => {
          const [bld, fl] = k.split('||')
          const rooms = groups.get(k)!
          return (
            <div key={k}>
              <div className="rt-group-head">
                {bld} · Floor {fl} <span className="tag">{plural(rooms.length, 'room')} · {rooms.filter(isFree).length} free</span>
              </div>
              {rooms.map((s) => {
                const segs = items.filter((i) => i.spaceId === s.id).map((i) => daySegment(i, f.date))
                  .filter((g): g is DaySegment => !!g).sort((a, b) => a.s - b.s)
                const free = computeFree(s.constraints, s.id, items, f.date, open, close)
                  .filter((w) => w.end - w.start >= s.constraints.minBookingMinutes)
                return (
                  <div key={s.id} className="rt-row">
                    <div className="rt-roominfo">
                      <div className="rt-roomname">{s.name}</div>
                      <div className="rt-roommeta">{s.typeName}{s.capacity ? ` · ${s.capacity} seats` : ''}</div>
                    </div>
                    <div className="rt-track" style={{ width: trackW }}>
                      {band}{nowLine}
                      {free.map((reg) => (
                        <div key={reg.start} className="rt-free" style={{ left: px(reg.start - open), width: px(reg.end - reg.start) }}
                          onClick={() => regionPrefill(s, reg)} title={`Book ${s.name} ${minLabel(reg.start)}–${minLabel(reg.end)}`} />
                      ))}
                      {segs.map((g) => {
                        const it = g.item
                        const who = it.kind === 'maintenance' ? it.note || 'Blocked' : it.ownerUserId === userId ? 'You' : it.ownerName
                        return (
                          <div key={it.id} className={`tg-block rt-block ${itemClass(it, userId)}`} onClick={() => openItem(it)}
                            style={{ left: px(g.s - open), width: Math.max(30, px(g.e - g.s) - 2) }}
                            title={`${hm(it.start)}–${hm(it.end)} UTC · ${who}`}>
                            <b>{g.clipStart ? '↥' : ''}{hm(it.start)}{g.clipEnd ? ' ↧' : ''}</b>
                            <span>{who}</span>
                          </div>
                        )
                      })}
                    </div>
                  </div>
                )
              })}
            </div>
          )
        })}
      </div>
    )
  }

  return (
    <section>
      <div className="find-layout">
        <FindFilters spaces={spaces} />
        <section className="card" style={{ overflow: 'hidden' }}>
          <div className="find-header">
            <div style={{ display: 'flex', alignItems: 'center', gap: 10 }}>
              <button type="button" className="btn btn-sm" onClick={findToday}>Today</button>
              <button type="button" className="iconbtn" onClick={() => f.shiftDay(-1)} aria-label="Previous day">‹</button>
              <span className="mono period-label" style={{ minWidth: 190 }}>
                {dayName(d).slice(0, 3)}, {monthName(d).slice(0, 3)} {d.getUTCDate()}, {d.getUTCFullYear()}
              </span>
              <button type="button" className="iconbtn" onClick={() => f.shiftDay(1)} aria-label="Next day">›</button>
            </div>
          </div>
          <p className="find-stats">
            {candidates.length ? `${freeCount} of ${plural(candidates.length, 'room')} free ${hm(winStart)}–${hm(winEnd)} · grouped by building and floor.` : ''}
          </p>
          {body}
        </section>
      </div>
    </section>
  )
}
