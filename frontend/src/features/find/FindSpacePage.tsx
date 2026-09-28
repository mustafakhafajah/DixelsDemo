import { useMemo, useState, type PointerEvent as ReactPointerEvent } from 'react'
import { useAvailability, useBuildings, useFindSpaces, useFloors, useMaintenance, useSpaceTypes } from '../../api/hooks'
import type { ScheduleItem, Space } from '../../api/types'
import { useSession } from '../../app/session'
import { LoadError, Loading, plural } from '../../components/bits'
import { DatePicker, Dropdown, TimePicker } from '../../components/pickers'
import { DEFAULT_MIN_MINUTES, RT_PX_PER_HOUR } from '../../lib/constants'
import { addDays, addMin, ceilStep, dayAt, dayKey, dayName, hm, minLabel, minOfDay, monthName, todayKey } from '../../lib/dateUtils'
import { candidatesDayBounds, computeFree, daySegment, findOverlap, validateWindowLocal, type DaySegment, type MinuteWindow } from '../../lib/laneLayout'
import { useDebouncedValue } from '../../lib/useDebouncedValue'
import { findToday, useFindStore, type FindDuration } from '../../state/findStore'
import { modals } from '../../state/modalStore'
import { itemClass, openItem } from '../bookings/schedule/ScheduleCalendar'

const px = (min: number) => (min / 60) * RT_PX_PER_HOUR

/* A free stretch cut at every whole hour, e.g. 09:30–12:00 -> 09:30–10:00, 10:00–11:00, 11:00–12:00. */
function hourCells(w: MinuteWindow): MinuteWindow[] {
  const out: MinuteWindow[] = []
  for (let s = w.start; s < w.end; s = Math.floor(s / 60) * 60 + 60) out.push({ start: s, end: Math.min(w.end, Math.floor(s / 60) * 60 + 60) })
  return out
}

const CAPACITY_PRESETS = [4, 6, 8, 12]
/* Dragged windows snap to quarter hours, the shortest booking most spaces allow. */
const DRAG_SNAP = 15

interface DragState {
  spaceId: string
  from: number
  to: number
  moved: boolean
}

function FindFilters() {
  const f = useFindStore()
  /* "Custom…" shows a number box; a saved value that isn't a preset reopens in custom mode. */
  const [customCapacity, setCustomCapacity] = useState(() => f.minCapacity > 0 && !CAPACITY_PRESETS.includes(f.minCapacity))
  const buildings = useBuildings().data ?? []
  const floors = useFloors().data ?? []
  const spaceTypes = useSpaceTypes().data ?? []
  /* Floor stays locked until a building is picked, then lists only that building's floors. */
  const floorNames = [...new Set(floors.filter((x) => x.isBookable && !!f.buildingId && x.buildingId === f.buildingId).map((x) => x.name))]
    .sort((a, b) => a.localeCompare(b, undefined, { numeric: true }))
  /* Only types some space actually has, as id → name. */
  const types = spaceTypes.filter((t) => t.spaceCount > 0).map((t) => [t.id, t.name] as const)
    .sort((a, b) => a[1].localeCompare(b[1]))
  const endMin = (((f.customEnd ?? f.time + 60) % 1440) + 1440) % 1440
  const setDur = (d: FindDuration) => f.patch({ duration: d, customEnd: d === 'custom' && f.customEnd == null ? f.time + 60 : f.customEnd })

  return (
    <aside className="card find-filters">
      <div>
        <h3>When</h3>
        {/* Stacked, one per row, so neither field is squeezed in the narrow filter column. */}
        <div style={{ display: 'flex', flexDirection: 'column', gap: 8 }}>
          <DatePicker value={f.date} aria-label="Date" onChange={(v) => f.patch({ date: v || todayKey() })} />
          <TimePicker value={minLabel(f.time)} aria-label="Start time"
            onChange={(v) => { const [h, m] = v.split(':').map(Number); f.patch({ time: h * 60 + m }) }} />
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
            <TimePicker id="fv-end" aria-label="End time" value={minLabel(endMin)}
              onChange={(v) => { const [h, m] = v.split(':').map(Number); f.patch({ customEnd: h * 60 + m }) }} />
          </div>
        )}
      </div>
      <div>
        <h3>Room criteria</h3>
        <div style={{ display: 'flex', flexDirection: 'column', gap: 10 }}>
          <div>
            <label className="lbl" htmlFor="fv-building">Building</label>
            <Dropdown id="fv-building" value={f.buildingId} onChange={(v) => f.patch({ buildingId: v, floorName: '' })}
              options={[{ value: '', label: 'All buildings' }, ...buildings.map((b) => ({ value: b.id, label: b.name }))]} />
          </div>
          <div>
            <label className="lbl" htmlFor="fv-floor">Floor</label>
            <Dropdown id="fv-floor" value={floorNames.includes(f.floorName) ? f.floorName : ''} onChange={(v) => f.patch({ floorName: v })}
              disabled={!f.buildingId} placeholder="Choose a building first"
              options={f.buildingId ? [{ value: '', label: 'All floors' }, ...floorNames.map((n) => ({ value: n, label: `Floor ${n}` }))] : []} />
          </div>
          <div>
            <label className="lbl" htmlFor="fv-capacity">Capacity</label>
            <Dropdown id="fv-capacity" value={customCapacity ? 'custom' : String(f.minCapacity)}
              onChange={(v) => {
                if (v === 'custom') { setCustomCapacity(true); return }
                setCustomCapacity(false)
                f.patch({ minCapacity: Number(v) })
              }}
              options={[
                { value: '0', label: 'Any capacity' },
                ...CAPACITY_PRESETS.map((n) => ({ value: String(n), label: `${n}+` })),
                { value: 'custom', label: 'Custom…' },
              ]} />
            {customCapacity && (
              <div style={{ display: 'flex', alignItems: 'center', gap: 8, marginTop: 6 }}>
                <input id="fv-capacity-custom" type="number" className="inp mono" min={1} placeholder="e.g. 10" autoFocus
                  aria-label="Minimum seats" value={f.minCapacity || ''}
                  onChange={(e) => f.patch({ minCapacity: Math.max(0, Math.floor(Number(e.target.value) || 0)) })} />
                <span style={{ fontSize: 12, color: 'var(--slate)', whiteSpace: 'nowrap' }}>seats or more</span>
              </div>
            )}
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
  const session = useSession()
  const { userId } = session
  /* The room criteria are filtered on the server; typing in the search box waits a moment before asking. */
  const name = useDebouncedValue(f.query.trim())
  const findQ = useFindSpaces({
    buildingId: f.buildingId || undefined,
    floorName: (f.buildingId && f.floorName) || undefined,
    minCapacity: f.minCapacity,
    typeIds: f.types,
    name: name || undefined,
  })
  const dayFrom = useMemo(() => dayAt(f.date), [f.date])
  const dayTo = useMemo(() => addDays(dayAt(f.date), 1), [f.date])
  const bookingsQ = useAvailability({ from: dayFrom, to: dayTo }, session)
  const maintQ = useMaintenance({ from: dayFrom, to: dayTo })

  const items: ScheduleItem[] = useMemo(() => [...(bookingsQ.data ?? []), ...(maintQ.data ?? [])], [bookingsQ.data, maintQ.data])

  const candidates = findQ.data ?? []

  const durMin = f.duration === 'custom' ? Math.max(DEFAULT_MIN_MINUTES, (f.customEnd ?? f.time + 60) - f.time) : f.duration
  const winStart = dayAt(f.date, 0, f.time)
  const winEnd = addMin(winStart, durMin)
  const isFree = (s: Space) => !findOverlap(items, s.id, winStart, winEnd) &&
    !validateWindowLocal(s.constraints, s.name, winStart, winEnd, true)
  const freeCount = candidates.filter(isFree).length
  const d = dayAt(f.date)

  /* Clicking an hour books that hour; when the space's minimum is longer, the booking runs on
   * into the next hours of the same free stretch. */
  const cellPrefill = (s: Space, cell: MinuteWindow, region: MinuteWindow) => {
    const end = Math.min(region.end, Math.max(cell.end, cell.start + s.constraints.minBookingMinutes))
    modals.booking({ spaceId: s.id, start: dayAt(f.date, 0, cell.start), end: dayAt(f.date, 0, end) })
  }

  const [drag, setDrag] = useState<DragState | null>(null)
  /* Set below, where the visible day (open/close) is known; rows only exist once it is. */
  let startDrag = (_e: ReactPointerEvent<HTMLDivElement>, _s: Space) => {}
  let moveDrag = (_e: ReactPointerEvent<HTMLDivElement>, _s: Space) => {}
  let endDrag = (_s: Space) => {}

  let body
  const failed = [findQ, bookingsQ, maintQ].find((q) => q.isError)
  if (failed) body = <LoadError what="the rooms" error={failed.error} onRetry={() => { findQ.refetch(); bookingsQ.refetch(); maintQ.refetch() }} />
  else if (!findQ.data) body = <Loading />
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

    /* Drag on a room's row to pick a window: minutes snap to DRAG_SNAP and stay inside the visible day. */
    const minuteAt = (e: ReactPointerEvent<HTMLDivElement>) => {
      const rect = e.currentTarget.getBoundingClientRect()
      const raw = open + ((e.clientX - rect.left) / RT_PX_PER_HOUR) * 60
      return Math.min(close, Math.max(open, Math.round(raw / DRAG_SNAP) * DRAG_SNAP))
    }
    startDrag = (e, s) => {
      if (e.button !== 0 || (e.target as HTMLElement).closest('.rt-block')) return
      const m = minuteAt(e)
      setDrag({ spaceId: s.id, from: m, to: m, moved: false })
    }
    moveDrag = (e, s) => {
      if (drag?.spaceId !== s.id) return
      const m = minuteAt(e)
      if (m === drag.to) return
      /* Capture only once it's really a drag, so a plain click still reaches the free slot underneath. */
      if (!drag.moved) e.currentTarget.setPointerCapture(e.pointerId)
      setDrag({ ...drag, to: m, moved: true })
    }
    endDrag = (s) => {
      if (drag?.spaceId !== s.id) return
      const from = Math.min(drag.from, drag.to)
      const to = Math.max(drag.from, drag.to)
      setDrag(null)
      if (!drag.moved || to - from < DRAG_SNAP) return
      /* The picked window becomes the search window too, so the band and "free" counts match what's being booked. */
      f.patch({ time: from, duration: 'custom', customEnd: to })
      modals.booking({ spaceId: s.id, start: dayAt(f.date, 0, from), end: dayAt(f.date, 0, to) })
    }
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
                /* Today's free time starts now: the past is never offered as free. */
                const from = isToday ? Math.max(open, ceilStep(nowMin)) : open
                const free = computeFree(s.constraints, s.id, items, f.date, from, close)
                  .filter((w) => w.end - w.start >= s.constraints.minBookingMinutes)
                const cells = free.flatMap((reg) => hourCells(reg)
                  .filter((c) => reg.end - c.start >= s.constraints.minBookingMinutes)
                  .map((c) => ({ ...c, reg })))
                return (
                  <div key={s.id} className="rt-row">
                    <div className="rt-roominfo">
                      <div className="rt-roomname">{s.name}</div>
                      <div className="rt-roommeta">{s.typeName}{s.capacity ? ` · ${s.capacity} seats` : ''}</div>
                    </div>
                    <div className="rt-track" style={{ width: trackW }}
                      onPointerDown={(e) => startDrag(e, s)} onPointerMove={(e) => moveDrag(e, s)}
                      onPointerUp={() => endDrag(s)} onPointerCancel={() => setDrag(null)}>
                      {band}{nowLine}
                      {drag?.spaceId === s.id && drag.moved && (
                        <div className="rt-drag" style={{ left: px(Math.min(drag.from, drag.to) - open), width: px(Math.abs(drag.to - drag.from)) }}>
                          <span>{minLabel(Math.min(drag.from, drag.to))}–{minLabel(Math.max(drag.from, drag.to))}</span>
                        </div>
                      )}
                      {cells.map((c) => (
                        <div key={c.start} className="rt-free" style={{ left: px(c.start - open) + 1, width: px(c.end - c.start) - 2 }}
                          onClick={() => cellPrefill(s, c, c.reg)} title={`Book ${s.name} ${minLabel(c.start)}–${minLabel(c.end)}`} />
                      ))}
                      {segs.map((g) => {
                        const it = g.item
                        const who = it.kind === 'maintenance' ? it.note || 'Blocked' : it.busy ? 'Busy' : it.ownerUserId === userId ? 'You' : it.ownerName
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
        <FindFilters />
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
