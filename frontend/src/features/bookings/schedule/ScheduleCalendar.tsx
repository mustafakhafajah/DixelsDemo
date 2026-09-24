import { useEffect, useMemo, useRef } from 'react'
import { lifecycleOf, type ScheduleItem } from '../../../api/types'
import { useSession } from '../../../app/session'
import { DEFAULT_MIN_MINUTES, PX_PER_HOUR, SLOT_MIN } from '../../../lib/constants'
import { addDays, dayAt, dayKey, dayName, hm, minLabel, minOfDay, monthName, pad, todayKey } from '../../../lib/dateUtils'
import { computeFree, daySegment, gridBounds, layoutLanes, type DaySegment } from '../../../lib/laneLayout'
import { modals, type ScheduleId } from '../../../state/modalStore'
import type { ScheduleData } from './useScheduleData'

export function itemClass(i: ScheduleItem, myId: string): string {
  if (i.kind === 'maintenance') return 'cleaning'
  const st = lifecycleOf(i)
  if (st === 'in_progress') return 'inprog'
  if (st === 'ended') return 'ended'
  return i.ownerUserId === myId ? 'mine' : 'other'
}

export const openItem = (i: ScheduleItem) => modals.detail(i.kind === 'booking' ? 'booking' : 'maintenance', i.id)

function MonthView({ id, data }: { id: ScheduleId; data: ScheduleData }) {
  const { cfg, items, multiSpace } = data
  const { userId } = useSession()
  const byDay = useMemo(() => {
    const m: Record<string, ScheduleItem[]> = {}
    items.forEach((i) => (m[dayKey(i.start)] ??= []).push(i))
    return m
  }, [items])

  const from = dayAt(cfg.from)
  const to = dayAt(cfg.to)
  const months: Date[] = []
  for (let c = new Date(Date.UTC(from.getUTCFullYear(), from.getUTCMonth(), 1)); c <= to;
    c = new Date(Date.UTC(c.getUTCFullYear(), c.getUTCMonth() + 1, 1))) months.push(c)
  const today = todayKey()

  const label = (i: ScheduleItem) => i.kind === 'maintenance'
    ? multiSpace ? `${i.note || 'Blocked'} · ${i.spaceName}` : i.note || 'Blocked'
    : multiSpace ? i.spaceName : i.ownerUserId === userId ? 'You' : i.ownerName

  return (
    <div className="month-wrap">
      {months.map((m) => {
        const y = m.getUTCFullYear()
        const mo = m.getUTCMonth()
        const daysInMonth = new Date(Date.UTC(y, mo + 1, 0)).getUTCDate()
        const firstDow = (new Date(Date.UTC(y, mo, 1)).getUTCDay() + 6) % 7
        const trail = (7 - ((firstDow + daysInMonth) % 7)) % 7
        return (
          <div key={m.getTime()} className="month-block">
            <div className="month-head">{monthName(m)} {y}</div>
            <div className="month-grid">
              {['Mon', 'Tue', 'Wed', 'Thu', 'Fri', 'Sat', 'Sun'].map((d) => <div key={d} className="wk-label">{d}</div>)}
              {Array.from({ length: firstDow }, (_, i) => <div key={`lead${i}`} className="day-cell out" />)}
              {Array.from({ length: daysInMonth }, (_, i) => {
                const key = `${y}-${pad(mo + 1)}-${pad(i + 1)}`
                const inRange = key >= cfg.from && key <= cfg.to
                const list = byDay[key] ?? []
                return (
                  <div key={key} className={`day-cell${inRange ? '' : ' out'}${key === today ? ' today' : ''}`}
                    onClick={inRange ? () => modals.day(key, id) : undefined}>
                    <span className="day-num">{i + 1}</span>
                    {list.slice(0, 2).map((it) => (
                      <div key={it.id} className={`chip ${itemClass(it, userId)}`}>{hm(it.start)} {label(it)}</div>
                    ))}
                    {list.length > 2 && <div className="more-link">+{list.length - 2} more</div>}
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

function TimeGridView({ id, data }: { id: ScheduleId; data: ScheduleData }) {
  const { cfg, items, multiSpace, single, shownSpaces, busyOnSpace } = data
  const { userId } = useSession()
  const scroller = useRef<HTMLDivElement>(null)
  const pph = cfg.mode === 'day' ? PX_PER_HOUR.day : PX_PER_HOUR.week

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
  const bookable = multiSpace || !!single?.canCurrentUserBook

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

  const axis = Array.from({ length: Math.ceil((close - open) / 60) }, (_, i) => open + i * 60)
  const slotMins = Array.from({ length: Math.ceil((close - open) / SLOT_MIN) }, (_, i) => open + i * SLOT_MIN)
  const track = cfg.mode === 'day' ? 'minmax(320px,1fr)' : 'minmax(126px,1fr)'

  return (
    <div className="tg-wrap">
      <div className="tg-scroll" ref={scroller}>
        <div className="tg-grid" style={{ gridTemplateColumns: `56px repeat(${days.length},${track})` }}>
          <div className="tg-corner" />
          {days.map((k) => {
            const d = dayAt(k)
            return (
              <button key={k} type="button" className={`tg-colhead${k === today ? ' today' : ''}`} title="Open the full day"
                onClick={() => modals.day(k, id)}>
                <small>{dayName(d).slice(0, 3).toUpperCase()}</small>{d.getUTCDate()} {monthName(d).slice(0, 3)}
              </button>
            )
          })}
          <div className="tg-axis" style={{ height: bodyH }}>
            {axis.map((m) => <div key={m} className="tg-hour" style={{ height: pph }}>{minLabel(m)}</div>)}
          </div>
          {days.map((k) => {
            const segs = segsBy[k]
            const dow = dayAt(k).getUTCDay()
            const slots = slotMins.map((m) => {
              const busy = segs.some((g) => g.s < m + SLOT_MIN && m < g.e)
              const inert = !bookable || busy || dayAt(k, 0, m) < now
              return (
                <div key={m} className={`tg-slot${m % 60 === 60 - SLOT_MIN ? ' hard' : ''}${inert ? ' inert' : ''}`}
                  style={{ height: pph / 2 }}
                  onClick={inert ? undefined : () => slotPrefill(k, m)}
                  title={inert ? undefined : `Book from ${minLabel(m)}`} />
              )
            })
            return (
              <div key={k} className={`tg-col${dow === 0 || dow === 6 ? ' weekend' : ''}`} style={{ height: bodyH }}>
                {slots}
                {segs.map((g) => {
                  const it = g.item
                  const who = it.kind === 'maintenance' ? it.note || 'Blocked' : it.ownerName
                  const label = it.kind === 'maintenance'
                    ? multiSpace ? `${who} · ${it.spaceName}` : who
                    : multiSpace ? it.spaceName : it.ownerUserId === userId ? 'You' : who
                  const w = 100 / g.lanes
                  const h = Math.max(17, ((g.e - g.s) / 60) * pph - 1)
                  return (
                    <div key={`${it.id}-${k}`} className={`tg-block ${itemClass(it, userId)}${h < 30 ? ' compact' : ''}`}
                      onClick={() => openItem(it)}
                      style={{ top: ((g.s - open) / 60) * pph, height: h, left: `calc(${w * g.lane}% + 2px)`, width: `calc(${w}% - 4px)` }}
                      title={`${hm(it.start)}–${hm(it.end)} UTC · ${it.spaceName} · ${who}`}>
                      <b>{g.clipStart ? '↥ ' : ''}{hm(it.start)}–{hm(it.end)}{g.clipEnd ? ' ↧' : ''}</b>
                      <span>{label}</span>
                    </div>
                  )
                })}
                {k === today && nowMin >= open && nowMin <= close && (
                  <div className="tg-now" style={{ top: ((nowMin - open) / 60) * pph }} title="Now" />
                )}
              </div>
            )
          })}
        </div>
      </div>
      <p className="tg-hint">
        {bookable ? 'Click an empty slot to book it' : 'This space cannot be booked, so slots are inert'} · click a block for its detail · click a date for the whole day. All times UTC.
      </p>
    </div>
  )
}

export function ScheduleCalendar({ id, data }: { id: ScheduleId; data: ScheduleData }) {
  return data.cfg.mode === 'month' ? <MonthView id={id} data={data} /> : <TimeGridView id={id} data={data} />
}
