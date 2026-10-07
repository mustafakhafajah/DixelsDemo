import { useEffect, useMemo, useRef, useState } from 'react'
import { useTranslation } from 'react-i18next'
import type { ScheduleItem } from '../../../api/types'
import { useSession } from '../../../app/session'
import { P } from '../../../auth/permissions'
import { isClosedAt, isClosedDay, localLabel, localRange, sameClockAsViewer, viewerZone, zoneCity } from '../../../lib/closedDays'
import { DEFAULT_MIN_MINUTES, PX_PER_HOUR, SLOT_MIN } from '../../../lib/constants'
import { WEEKDAYS } from '../../../lib/closedDays'
import { addDays, dayAt, dayKey, formatDate, hm, minLabel, minOfDay, pad, todayKey, weekdayName } from '../../../lib/dateUtils'
import { computeFree, daySegment, gridBounds, layoutLanes, type DaySegment } from '../../../lib/laneLayout'
import { pressable } from '../../../lib/pressable'
import { modals, type ScheduleId } from '../../../state/modalStore'
import { itemClass, openItem, opens } from './scheduleItems'
import type { ScheduleData } from './useScheduleData'

/* Each item's building time zone, by space (a space always has its building's zone). */
function useZones(data: ScheduleData) {
  return useMemo(() => new Map(data.spaces.map((s) => [s.id, s.timeZone])), [data.spaces])
}

function MonthView({ id, data }: { id: ScheduleId; data: ScheduleData }) {
  const { t } = useTranslation()
  const { cfg, items, multiSpace, everyone, closed } = data
  const { userId } = useSession()
  const zones = useZones(data)
  const byDay = useMemo(() => {
    const m: Record<string, ScheduleItem[]> = {}
    items.forEach((i) => (m[dayKey(i.start)] ??= []).push(i))
    return m
  }, [items])

  const from = dayAt(cfg.from)
  const to = dayAt(cfg.to)
  const months: Date[] = []
  for (let c = new Date(from.getFullYear(), from.getMonth(), 1); c <= to;
    c = new Date(c.getFullYear(), c.getMonth() + 1, 1)) months.push(c)
  const today = todayKey()

  const label = (i: ScheduleItem) => i.kind === 'maintenance'
    ? multiSpace ? `${i.note || t('schedule.blocked')} · ${i.spaceName}` : i.note || t('schedule.blocked')
    : multiSpace
      /* With everyone's bookings shown, each one says whose it is too. */
      ? everyone ? `${i.ownerUserId === userId ? t('common.you') : i.ownerName} · ${i.spaceName}` : i.spaceName
      : i.ownerUserId === userId ? t('common.you') : i.ownerName

  /* A building on another clock also shows its own time: " · 08:00 Warsaw" on the chip, the full window in its tooltip. */
  const tzOf = (i: ScheduleItem) => zones.get(i.spaceId) ?? viewerZone()
  const localStart = (i: ScheduleItem) => sameClockAsViewer(tzOf(i), i.start) ? null
    : <i className="tg-local"> · {localLabel(i.start, tzOf(i))} {zoneCity(tzOf(i))}</i>
  const localSuffix = (i: ScheduleItem) => { const l = localRange(i.start, i.end, tzOf(i)); return l ? ` (${l})` : '' }

  return (
    <div className="month-wrap">
      {months.map((m) => {
        const y = m.getFullYear()
        const mo = m.getMonth()
        const daysInMonth = new Date(y, mo + 1, 0).getDate()
        const firstDow = (new Date(y, mo, 1).getDay() + 6) % 7
        const trail = (7 - ((firstDow + daysInMonth) % 7)) % 7
        return (
          <div key={m.getTime()} className="month-block">
            <div className="month-head">{formatDate(m, { month: 'long', year: 'numeric' })}</div>
            <div className="month-grid">
              {WEEKDAYS.map((d) => <div key={d} className="wk-label">{weekdayName(d, 'short')}</div>)}
              {Array.from({ length: firstDow }, (_, i) => <div key={`lead${i}`} className="day-cell out" />)}
              {Array.from({ length: daysInMonth }, (_, i) => {
                const key = `${y}-${pad(mo + 1)}-${pad(i + 1)}`
                const inRange = key >= cfg.from && key <= cfg.to
                const shut = inRange && !!closed && isClosedDay(closed, key)
                const list = byDay[key] ?? []
                return (
                  /* A closed day (holiday or weekly closed day) does nothing when clicked; bookings made on it
                   * before it was closed still open their detail. */
                  <div key={key} className={`day-cell${inRange ? '' : ' out'}${shut ? ' closed' : ''}${key === today ? ' today' : ''}`}
                    onClick={inRange && !shut ? () => modals.day(key, id) : undefined}
                    {...(inRange && !shut ? pressable(() => modals.day(key, id)) : {})}
                    title={shut ? t('schedule.buildingClosedDay') : undefined}>
                    <span className="day-num">{i + 1}</span>
                    {shut && <span className="closed-label">{t('schedule.closed')}</span>}
                    {list.slice(0, 2).map((it) => (
                      <div key={it.id} className={`chip ${itemClass(it, userId)}`}
                        onClick={shut ? (e) => { e.stopPropagation(); openItem(it) } : undefined}
                        {...(shut && opens(it) ? pressable(() => openItem(it)) : {})}
                        title={`${hm(it.start)}–${hm(it.end)}${localSuffix(it)} · ${label(it)}`}>
                        {hm(it.start)}{localStart(it)} <bdi>{label(it)}</bdi>
                      </div>
                    ))}
                    {list.length > 2 && <div className="more-link">{t('schedule.more', { count: list.length - 2 })}</div>}
                  </div>
                )
              })}
              {Array.from({ length: trail }, (_, i) => <div key={`trail${i}`} className="day-cell out" />)}
            </div>
          </div>
        )
      })}
    </div>
  )
}

interface SlotDrag { key: string; a: number; b: number }

function TimeGridView({ id, data }: { id: ScheduleId; data: ScheduleData }) {
  const { t } = useTranslation()
  const { cfg, items, multiSpace, everyone, single, shownSpaces, busyOnSpace, closed } = data
  const { userId, can } = useSession()
  const canBook = can(P.Bookings.Create)
  const zones = useZones(data)
  const scroller = useRef<HTMLDivElement>(null)
  const pph = cfg.mode === 'day' ? PX_PER_HOUR.day : PX_PER_HOUR.week
  /* Mouse drag over empty slots: from the slot pressed (a) to the slot under the pointer (b), in one day. */
  const [drag, setDrag] = useState<SlotDrag | null>(null)
  const dragNow = useRef<SlotDrag | null>(null) // the same drag, readable from the window listeners
  const moveDrag = (d: SlotDrag | null) => { dragNow.current = d; setDrag(d) }
  const dragCol = useRef<HTMLDivElement | null>(null)
  const pressedWithMouse = useRef(false)

  const days = useMemo(() => {
    const out: string[] = []
    for (let d = dayAt(cfg.from); dayKey(d) <= cfg.to; d = addDays(d, 1)) out.push(dayKey(d))
    return out
  }, [cfg.from, cfg.to])

  const segsBy = useMemo(() => {
    const m: Record<string, DaySegment[]> = {}
    days.forEach((k) => (m[k] = layoutLanes(items.map((i) => daySegment(i, k)).filter((g): g is DaySegment => !!g))))
    return m
  }, [days, items])

  const { start: open, end: close } = gridBounds(segsBy, shownSpaces.map((s) => s.constraints))
  const bodyH = ((close - open) / 60) * pph
  const now = new Date()
  const today = todayKey()
  const nowMin = minOfDay(now)
  /* Without permission to book, every slot is inert: no click-to-book and no drag. */
  const bookable = canBook && (multiSpace || !!single?.canCurrentUserBook)

  useEffect(() => {
    if (scroller.current && days.includes(today) && nowMin > open && nowMin < close)
      scroller.current.scrollTop = Math.max(0, ((nowMin - open) / 60) * pph - pph)
  }, [days, today, nowMin, open, close, pph])

  const slotPrefill = (key: string, min: number) => {
    let endMin = min + 60
    if (single) {
      const win = computeFree(single.constraints, single.id, busyOnSpace, key, 0, 1440).find((w) => w.start <= min && w.end > min)
      if (win) endMin = Math.min(endMin, win.end)
    }
    const minM = single?.constraints.minBookingMinutes ?? DEFAULT_MIN_MINUTES
    if (endMin - min < minM) endMin = min + minM
    endMin = Math.min(1440, endMin)
    modals.booking({ spaceId: single?.id, start: dayAt(key, 0, min), end: dayAt(key, 0, endMin) })
  }

  /* A slot can be booked when the space is bookable, the slot is free, not in the past and the building is open. */
  const inertAt = (key: string, min: number) =>
    !bookable || segsBy[key].some((g) => g.s < min + SLOT_MIN && min < g.e) || dayAt(key, 0, min) < now
    || (!!closed && (isClosedDay(closed, key) || isClosedAt(closed, dayAt(key, 0, min))))

  /* The drag only grows over free slots: it stops before the first booked, past or closed slot. */
  const reach = (key: string, from: number, to: number) => {
    const step = to >= from ? SLOT_MIN : -SLOT_MIN
    let m = from
    while (m !== to && m + step >= open && m + step < close && !inertAt(key, m + step)) m += step
    return m
  }

  const finishDrag = (d: SlotDrag) => {
    const lo = Math.min(d.a, d.b)
    const hi = Math.max(d.a, d.b) + SLOT_MIN
    if (hi - lo === SLOT_MIN) return slotPrefill(d.key, lo) // a plain click keeps the usual one-hour booking
    const minM = single?.constraints.minBookingMinutes ?? DEFAULT_MIN_MINUTES
    modals.booking({ spaceId: single?.id, start: dayAt(d.key, 0, lo), end: dayAt(d.key, 0, Math.min(1440, Math.max(hi, lo + minM))) })
  }

  /* The window listeners are added once per drag, so they read the latest helpers through this ref. */
  const live = useRef({ reach, finishDrag })
  useEffect(() => { live.current = { reach, finishDrag } })
  const dragging = drag !== null
  useEffect(() => {
    if (!dragging) return
    const move = (e: PointerEvent) => {
      const col = dragCol.current
      const d = dragNow.current
      if (!col || !d) return
      const target = open + Math.floor((e.clientY - col.getBoundingClientRect().top) / (pph / 2)) * SLOT_MIN
      const b = live.current.reach(d.key, d.a, Math.max(open, Math.min(close - SLOT_MIN, target)))
      if (b !== d.b) moveDrag({ ...d, b })
    }
    const up = () => {
      const d = dragNow.current
      moveDrag(null)
      if (d) live.current.finishDrag(d)
    }
    const esc = (e: KeyboardEvent) => { if (e.key === 'Escape') moveDrag(null) }
    window.addEventListener('pointermove', move)
    window.addEventListener('pointerup', up)
    window.addEventListener('keydown', esc)
    return () => {
      window.removeEventListener('pointermove', move)
      window.removeEventListener('pointerup', up)
      window.removeEventListener('keydown', esc)
    }
  }, [dragging, open, close, pph])

  const axis = Array.from({ length: Math.ceil((close - open) / 60) }, (_, i) => open + i * 60)
  /* When every space shown is in one building zone whose clock reads differently from the viewer's, a second hour
   * column gives that zone's hours (read on the first day shown), headed by its city. */
  const shownZones = [...new Set(shownSpaces.map((s) => s.timeZone))]
  const axisZone = shownZones.length === 1 && days.length && !sameClockAsViewer(shownZones[0], dayAt(days[0], 12)) ? shownZones[0] : null
  const tzOf = (i: ScheduleItem) => zones.get(i.spaceId) ?? viewerZone()
  const slotMins = Array.from({ length: Math.ceil((close - open) / SLOT_MIN) }, (_, i) => open + i * SLOT_MIN)
  const track = cfg.mode === 'day' ? 'minmax(220px,1fr)' : 'minmax(126px,1fr)'

  return (
    <div className={`tg-wrap${dragging ? ' dragging' : ''}`}>
      <div className="tg-scroll" ref={scroller}>
        <div className="tg-grid" style={{ gridTemplateColumns: `56px repeat(${days.length},${track})` }}>
          <div className="tg-corner">{axisZone && <span className="tg-corner-zones"><small>{zoneCity(axisZone)}</small></span>}</div>
          {days.map((k) => {
            const d = dayAt(k)
            const shut = !!closed && isClosedDay(closed, k)
            return (
              <button key={k} type="button" className={`tg-colhead${k === today ? ' today' : ''}${shut ? ' closed' : ''}`}
                title={shut ? t('schedule.buildingClosedDay') : t('schedule.openDay')} disabled={shut}
                onClick={() => modals.day(k, id)}>
                <small>{formatDate(d, { weekday: 'short' }).toUpperCase()}</small>{formatDate(d, { day: 'numeric', month: 'short' })}
              </button>
            )
          })}
          <div className="tg-axis" style={{ height: bodyH }}>
            {axis.map((m) => (
              <div key={m} className="tg-hour" style={{ height: pph }}>
                {minLabel(m)}{axisZone && <small className="tg-hour-local">{localLabel(dayAt(days[0], 0, m), axisZone)}</small>}
              </div>
            ))}
          </div>
          {days.map((k) => {
            const segs = segsBy[k]
            const dow = dayAt(k).getDay()
            const shut = !!closed && isClosedDay(closed, k)
            const slots = slotMins.map((m) => {
              const inert = inertAt(k, m)
              return (
                <div key={m} className={`tg-slot${m % 60 === 60 - SLOT_MIN ? ' hard' : ''}${inert ? ' inert' : ''}`}
                  style={{ height: pph / 2 }}
                  onPointerDown={inert ? undefined : (e) => {
                    /* Mouse: press, drag and release to choose the time. Touch keeps tap-to-book so the grid still scrolls. */
                    pressedWithMouse.current = e.pointerType === 'mouse'
                    if (!pressedWithMouse.current || e.button !== 0) return
                    e.preventDefault()
                    dragCol.current = e.currentTarget.parentElement as HTMLDivElement
                    moveDrag({ key: k, a: m, b: m })
                  }}
                  onClick={inert ? undefined : () => { if (!pressedWithMouse.current) slotPrefill(k, m) }}
                  {...(inert ? {} : { ...pressable(() => slotPrefill(k, m)), 'aria-label': `${formatDate(dayAt(k), { weekday: 'short', day: 'numeric', month: 'short' })} · ${t('schedule.slotTitle', { time: minLabel(m) })}` })}
                  title={inert || dragging ? undefined : t('schedule.slotTitle', { time: minLabel(m) })} />
              )
            })
            const sel = drag?.key === k ? { lo: Math.min(drag.a, drag.b), hi: Math.max(drag.a, drag.b) + SLOT_MIN } : null
            return (
              <div key={k} className={`tg-col${dow === 0 || dow === 6 ? ' weekend' : ''}${shut ? ' closed' : ''}`} style={{ height: bodyH }}>
                {slots}
                {sel && (
                  <div className="tg-select" style={{ top: ((sel.lo - open) / 60) * pph, height: ((sel.hi - sel.lo) / 60) * pph }}>
                    {minLabel(sel.lo)}–{minLabel(sel.hi)}{axisZone && <> · {localRange(dayAt(k, 0, sel.lo), dayAt(k, 0, sel.hi), axisZone)}</>}
                  </div>
                )}
                {segs.map((g) => {
                  const it = g.item
                  const who = it.kind === 'maintenance' ? it.note || t('schedule.blocked') : it.ownerName
                  const label = it.kind === 'maintenance'
                    ? multiSpace ? `${who} · ${it.spaceName}` : who
                    : multiSpace
                      ? everyone ? `${it.ownerUserId === userId ? t('common.you') : who} · ${it.spaceName}` : it.spaceName
                      : it.ownerUserId === userId ? t('common.you') : who
                  const local = localRange(it.start, it.end, tzOf(it))
                  const w = 100 / g.lanes
                  const h = Math.max(17, ((g.e - g.s) / 60) * pph - 1)
                  return (
                    <div key={`${it.id}-${k}`} className={`tg-block ${itemClass(it, userId)}${h < 30 ? ' compact' : ''}`}
                      onClick={() => openItem(it)} {...(opens(it) ? pressable(() => openItem(it)) : {})}
                      style={{ top: ((g.s - open) / 60) * pph, height: h, insetInlineStart: `calc(${w * g.lane}% + 2px)`, width: `calc(${w}% - 4px)` }}
                      title={`${hm(it.start)}–${hm(it.end)}${local ? ` (${local})` : ''} · ${it.spaceName} · ${who}`}>
                      <b>{g.clipStart ? '↥ ' : ''}{hm(it.start)}–{hm(it.end)}{g.clipEnd ? ' ↧' : ''}</b>
                      {local && h >= 30 && <span className="tg-local">{local}</span>}
                      <span><bdi>{label}</bdi></span>
                    </div>
                  )
                })}
                {k === today && nowMin >= open && nowMin <= close && (
                  <div className="tg-now" style={{ top: ((nowMin - open) / 60) * pph }} title={t('common.now')} />
                )}
              </div>
            )
          })}
        </div>
      </div>
      <p className="tg-hint">
        {bookable ? t('schedule.hint.bookable') : canBook ? t('schedule.hint.notBookable') : t('schedule.hint.noPermission')} · {t('schedule.hint.rest')}
      </p>
    </div>
  )
}

export function ScheduleCalendar({ id, data }: { id: ScheduleId; data: ScheduleData }) {
  return data.cfg.mode === 'month' ? <MonthView id={id} data={data} /> : <TimeGridView id={id} data={data} />
}
