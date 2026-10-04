import { useMemo, useState, type PointerEvent as ReactPointerEvent } from 'react'
import { useTranslation } from 'react-i18next'
import { useAvailability, useBuildings, useFindSpaces, useFloors, useMaintenance, useSpaceTypes } from '../../api/hooks'
import type { ScheduleItem, Space } from '../../api/types'
import { useSession } from '../../app/session'
import { P } from '../../auth/permissions'
import { ErrorLine, LoadError, Loading } from '../../components/bits'
import { DatePicker, Dropdown, TimePicker } from '../../components/pickers'
import { DEFAULT_MIN_MINUTES, RT_PX_PER_HOUR } from '../../lib/constants'
import { addDays, addMin, ceilStep, dayAt, dayKey, durationLabel, formatDate, hm, minLabel, minOfDay, todayKey } from '../../lib/dateUtils'
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
  const { t } = useTranslation()
  const f = useFindStore()
  /* "Custom…" shows a number box; a saved value that isn't a preset reopens in custom mode. */
  const [customCapacity, setCustomCapacity] = useState(() => f.minCapacity > 0 && !CAPACITY_PRESETS.includes(f.minCapacity))
  const buildings = useBuildings().data ?? []
  /* Floor stays locked until a building is picked, then the server is asked for that building's floors. */
  const floors = (useFloors(f.buildingId, !!f.buildingId).data ?? []).filter((x) => x.isBookable)
  const spaceTypes = useSpaceTypes().data ?? []
  /* Only types some space actually has, as id → name. */
  const types = spaceTypes.filter((t) => t.spaceCount > 0).map((t) => [t.id, t.name] as const)
    .sort((a, b) => a[1].localeCompare(b[1]))
  const endMin = (((f.customEnd ?? f.time + 60) % 1440) + 1440) % 1440
  const setDur = (d: FindDuration) => f.patch({ duration: d, customEnd: d === 'custom' && f.customEnd == null ? f.time + 60 : f.customEnd })

  return (
    <aside className="card find-filters">
      <div>
        <h3>{t('find.when')}</h3>
        {/* Stacked, one per row, so neither field is squeezed in the narrow filter column. */}
        <div style={{ display: 'flex', flexDirection: 'column', gap: 8 }}>
          <DatePicker value={f.date} aria-label={t('common.date')} onChange={(v) => f.patch({ date: v || todayKey() })} />
          <TimePicker value={minLabel(f.time)} aria-label={t('common.startTime')}
            onChange={(v) => { const [h, m] = v.split(':').map(Number); f.patch({ time: h * 60 + m }) }} />
        </div>
        <div className="dur-row" style={{ marginTop: 10 }}>
          <button type="button" className="dur-chip" onClick={f.now}>{t('common.now')}</button>
          {([30, 60, 120, 'custom'] as FindDuration[]).map((d) => (
            <button key={d} type="button" className={`dur-chip${f.duration === d ? ' active' : ''}`} onClick={() => setDur(d)}>
              {d === 'custom' ? t('find.custom') : d === 30 ? t('find.minutes', { count: 30 }) : durationLabel(d)}
            </button>
          ))}
        </div>
        {f.duration === 'custom' && (
          <div style={{ marginTop: 8 }}>
            <label className="lbl" htmlFor="fv-end">{t('common.end')}</label>
            <TimePicker id="fv-end" aria-label={t('common.endTime')} value={minLabel(endMin)}
              onChange={(v) => { const [h, m] = v.split(':').map(Number); f.patch({ customEnd: h * 60 + m }) }} />
            {/* Until it is fixed, the search uses the shortest booking length from the start. */}
            <ErrorLine id="fv-end-error" error={endMin <= f.time
              ? { code: 'validation.end_before_start', message: t('find.endAfterStart', { start: minLabel(f.time) }) } : undefined} />
          </div>
        )}
        <label htmlFor="fv-free-only" style={{ display: 'flex', gap: 9, alignItems: 'flex-start', cursor: 'pointer', marginTop: 12 }}>
          <input type="checkbox" id="fv-free-only" checked={f.freeOnly} onChange={(e) => f.patch({ freeOnly: e.target.checked })} style={{ marginTop: 3 }} />
          <span>
            <span style={{ fontWeight: 600 }}>{t('find.freeOnly')}</span>
            <span style={{ display: 'block', fontSize: 12, color: 'var(--slate)' }}>{t('find.freeOnlyHint')}</span>
          </span>
        </label>
      </div>
      <div>
        <h3>{t('find.criteria')}</h3>
        <div style={{ display: 'flex', flexDirection: 'column', gap: 10 }}>
          <div>
            <label className="lbl" htmlFor="fv-building">{t('common.building')}</label>
            <Dropdown id="fv-building" value={f.buildingId} onChange={(v) => f.patch({ buildingId: v, floorId: '' })}
              options={[{ value: '', label: t('common.allBuildings') }, ...buildings.map((b) => ({ value: b.id, label: b.name }))]} />
          </div>
          <div>
            <label className="lbl" htmlFor="fv-floor">{t('common.floor')}</label>
            <Dropdown id="fv-floor" value={f.floorId} onChange={(v) => f.patch({ floorId: v })}
              disabled={!f.buildingId} placeholder={t('common.chooseBuildingFirst')}
              options={f.buildingId ? [{ value: '', label: t('common.allFloors') }, ...floors.map((x) => ({ value: x.id, label: t('common.floorName', { name: x.name }) }))] : []} />
          </div>
          <div>
            <label className="lbl" htmlFor="fv-capacity">{t('common.capacity')}</label>
            <Dropdown id="fv-capacity" value={customCapacity ? 'custom' : String(f.minCapacity)}
              onChange={(v) => {
                if (v === 'custom') { setCustomCapacity(true); return }
                setCustomCapacity(false)
                f.patch({ minCapacity: Number(v) })
              }}
              options={[
                { value: '0', label: t('find.anyCapacity') },
                ...CAPACITY_PRESETS.map((n) => ({ value: String(n), label: t('find.atLeast', { count: n }) })),
                { value: 'custom', label: t('find.customCapacity') },
              ]} />
            {customCapacity && (
              <div style={{ display: 'flex', alignItems: 'center', gap: 8, marginTop: 6 }}>
                <input id="fv-capacity-custom" type="number" className="inp mono" min={1} placeholder={t('find.seatsPlaceholder')} autoFocus
                  aria-label={t('find.minSeats')} value={f.minCapacity || ''}
                  onChange={(e) => f.patch({ minCapacity: Math.max(0, Math.floor(Number(e.target.value) || 0)) })} />
                <span style={{ fontSize: 12, color: 'var(--slate)', whiteSpace: 'nowrap' }}>{t('find.seatsOrMore', { count: f.minCapacity })}</span>
              </div>
            )}
          </div>
          <div>
            <label className="lbl">{t('find.spaceType')}</label>
            <div style={{ display: 'flex', flexWrap: 'wrap', gap: 6 }}>
              {types.map(([id, name]) => (
                <button key={id} type="button" className={`type-pill${f.types.includes(id) ? ' active' : ''}`} onClick={() => f.toggleType(id)}>
                  <bdi>{name}</bdi>
                </button>
              ))}
            </div>
          </div>
          <div>
            <label className="lbl" htmlFor="fv-query">{t('find.search')}</label>
            <input id="fv-query" className="inp" dir="auto" placeholder={t('find.searchPlaceholder')} value={f.query} onChange={(e) => f.patch({ query: e.target.value })} />
          </div>
        </div>
      </div>
    </aside>
  )
}

export function FindSpacePage() {
  const { t, i18n } = useTranslation()
  const f = useFindStore()
  const session = useSession()
  const { userId } = session
  /* Without permission to book, the timeline is read-only: no free cells to click and no drag. */
  const canBook = session.can(P.Bookings.Create)
  const durMin = f.duration === 'custom' ? Math.max(DEFAULT_MIN_MINUTES, (f.customEnd ?? f.time + 60) - f.time) : f.duration
  const winStart = useMemo(() => dayAt(f.date, 0, f.time), [f.date, f.time])
  const winEnd = useMemo(() => addMin(winStart, durMin), [winStart, durMin])

  /* Every filter is sent to the server; typing in the search box waits a moment before asking. With "Free only"
   * the chosen time and length go too, and the server leaves out rooms that are taken or closed then. */
  const name = useDebouncedValue(f.query.trim())
  const buildingId = f.buildingId || undefined
  const floorId = (f.buildingId && f.floorId) || undefined
  const findQ = useFindSpaces({
    buildingId,
    floorId,
    minCapacity: f.minCapacity,
    typeIds: f.types,
    name: name || undefined,
    freeFrom: f.freeOnly ? winStart : undefined,
    freeTo: f.freeOnly ? winEnd : undefined,
  })
  /* The day's bookings and blocked time, for the chosen building and floor only. */
  const dayFrom = useMemo(() => dayAt(f.date), [f.date])
  const dayTo = useMemo(() => addDays(dayAt(f.date), 1), [f.date])
  const bookingsQ = useAvailability({ from: dayFrom, to: dayTo, buildingId, floorId }, session)
  const maintQ = useMaintenance({ from: dayFrom, to: dayTo, buildingId, floorId })

  const items: ScheduleItem[] = useMemo(() => [...(bookingsQ.data ?? []), ...(maintQ.data ?? [])], [bookingsQ.data, maintQ.data])

  const candidates = findQ.data ?? []
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
  if (failed) body = <LoadError what={t('load.rooms')} error={failed.error} onRetry={() => { findQ.refetch(); bookingsQ.refetch(); maintQ.refetch() }} />
  else if (!findQ.data) body = <Loading />
  else if (!candidates.length) {
    body = (
      <div className="sched-empty">
        <p>{f.freeOnly ? t('find.noFreeRooms') : t('find.noRooms')}</p>
        <p>{f.freeOnly ? t('find.noFreeRoomsHint') : t('find.noRoomsHint')}</p>
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
    const band = wE > wS ? <div className="rt-band" style={{ insetInlineStart: px(wS - open), width: px(wE - wS) }} /> : null

    /* Drag on a room's row to pick a window: minutes snap to DRAG_SNAP and stay inside the visible day.
     * Time runs along the reading direction, so in right-to-left languages it is measured from the right edge. */
    const rtl = i18n.dir() === 'rtl'
    const minuteAt = (e: ReactPointerEvent<HTMLDivElement>) => {
      const rect = e.currentTarget.getBoundingClientRect()
      const raw = open + ((rtl ? rect.right - e.clientX : e.clientX - rect.left) / RT_PX_PER_HOUR) * 60
      return Math.min(close, Math.max(open, Math.round(raw / DRAG_SNAP) * DRAG_SNAP))
    }
    startDrag = (e, s) => {
      if (!canBook || e.button !== 0 || (e.target as HTMLElement).closest('.rt-block')) return
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
    const nowLine = isToday && nowMin >= open && nowMin <= close ? <div className="rt-now" style={{ insetInlineStart: px(nowMin - open) }} /> : null

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
            {ticks.map((m) => <div key={m} className="rt-tick" style={{ insetInlineStart: px(m - open) }}>{minLabel(m)}</div>)}
            {band}{nowLine}
          </div>
        </div>
        {keys.map((k) => {
          const [bld, fl] = k.split('||')
          const rooms = groups.get(k)!
          return (
            <div key={k}>
              <div className="rt-group-head">
                <bdi>{t('common.floorIn', { building: bld, floor: fl })}</bdi> <span className="tag">{t('find.roomsFree', { rooms: t('count.room', { count: rooms.length }), count: rooms.filter(isFree).length })}</span>
              </div>
              {rooms.map((s) => {
                const segs = items.filter((i) => i.spaceId === s.id).map((i) => daySegment(i, f.date))
                  .filter((g): g is DaySegment => !!g).sort((a, b) => a.s - b.s)
                /* Today's free time starts now: the past is never offered as free. */
                const from = isToday ? Math.max(open, ceilStep(nowMin)) : open
                const free = computeFree(s.constraints, s.id, items, f.date, from, close)
                  .filter((w) => w.end - w.start >= s.constraints.minBookingMinutes)
                const cells = !canBook ? [] : free.flatMap((reg) => hourCells(reg)
                  .filter((c) => reg.end - c.start >= s.constraints.minBookingMinutes)
                  .map((c) => ({ ...c, reg })))
                return (
                  <div key={s.id} className="rt-row">
                    <div className="rt-roominfo">
                      <div className="rt-roomname"><bdi>{s.name}</bdi></div>
                      <div className="rt-roommeta"><bdi>{s.typeName}</bdi>{s.capacity ? ` · ${t('find.seats', { count: s.capacity })}` : ''}</div>
                    </div>
                    <div className="rt-track" style={{ width: trackW }}
                      onPointerDown={(e) => startDrag(e, s)} onPointerMove={(e) => moveDrag(e, s)}
                      onPointerUp={() => endDrag(s)} onPointerCancel={() => setDrag(null)}>
                      {band}{nowLine}
                      {drag?.spaceId === s.id && drag.moved && (
                        <div className="rt-drag" style={{ insetInlineStart: px(Math.min(drag.from, drag.to) - open), width: px(Math.abs(drag.to - drag.from)) }}>
                          <span>{minLabel(Math.min(drag.from, drag.to))}–{minLabel(Math.max(drag.from, drag.to))}</span>
                        </div>
                      )}
                      {cells.map((c) => (
                        <div key={c.start} className="rt-free" style={{ insetInlineStart: px(c.start - open) + 1, width: px(c.end - c.start) - 2 }}
                          onClick={() => cellPrefill(s, c, c.reg)} title={t('find.bookCell', { name: s.name, from: minLabel(c.start), to: minLabel(c.end) })} />
                      ))}
                      {segs.map((g) => {
                        const it = g.item
                        const who = it.kind === 'maintenance' ? it.note || t('schedule.blocked') : it.busy ? t('schedule.busy') : it.ownerUserId === userId ? t('common.you') : it.ownerName
                        return (
                          <div key={it.id} className={`tg-block rt-block ${itemClass(it, userId)}`} onClick={() => openItem(it)}
                            style={{ insetInlineStart: px(g.s - open), width: Math.max(30, px(g.e - g.s) - 2) }}
                            title={`${hm(it.start)}–${hm(it.end)} UTC · ${who}`}>
                            <b>{g.clipStart ? '↥' : ''}{hm(it.start)}{g.clipEnd ? ' ↧' : ''}</b>
                            <span><bdi>{who}</bdi></span>
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
          {/* Today at the start, the date with its arrows in the middle (the empty third column keeps it centred). */}
          <div className="find-header">
            <button type="button" className="btn btn-sm find-today" onClick={findToday}>{t('common.today')}</button>
            <div className="find-date-nav">
              <button type="button" className="iconbtn" onClick={() => f.shiftDay(-1)} aria-label={t('find.previousDay')} title={t('find.previousDay')}>‹</button>
              <span className="mono period-label" style={{ minWidth: 190 }}>
                {formatDate(d, { weekday: 'short', day: 'numeric', month: 'short', year: 'numeric' })}
              </span>
              <button type="button" className="iconbtn" onClick={() => f.shiftDay(1)} aria-label={t('find.nextDay')} title={t('find.nextDay')}>›</button>
            </div>
            <span />
          </div>
          <p className="find-stats">
            {candidates.length ? t('find.stats', { free: freeCount, rooms: t('count.room', { count: candidates.length }), from: hm(winStart), to: hm(winEnd) }) : ''}
          </p>
          {body}
        </section>
      </div>
    </section>
  )
}
