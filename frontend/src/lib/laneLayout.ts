import type { Constraints, ScheduleItem } from '../api/types'
import { DEFAULT_CLOSE_MIN, DEFAULT_OPEN_MIN } from './constants'
import { dayAt, dayKey, minOfDay } from './dateUtils'

export interface DaySegment {
  item: ScheduleItem
  s: number
  e: number
  clipStart: boolean
  clipEnd: boolean
  lane: number
  lanes: number
}

export interface MinuteWindow {
  start: number
  end: number
}

/* Clip an item to one day column; an overnight item yields a segment in each day it touches. */
export function daySegment(item: ScheduleItem, key: string): DaySegment | null {
  const d0 = dayAt(key).getTime()
  const d1 = d0 + 86_400_000
  const s = Math.max(item.start.getTime(), d0)
  const e = Math.min(item.end.getTime(), d1)
  if (e <= s) return null
  return {
    item,
    s: Math.round((s - d0) / 60000),
    e: Math.round((e - d0) / 60000),
    clipStart: item.start.getTime() < d0,
    clipEnd: item.end.getTime() > d1,
    lane: 0,
    lanes: 1,
  }
}

/* Greedy lane assignment so overlapping segments sit side by side. */
export function layoutLanes(input: DaySegment[]): DaySegment[] {
  const segs = input.map((g) => ({ ...g })).sort((a, b) => a.s - b.s || a.e - b.e)
  let cluster: DaySegment[] = []
  let clusterEnd = -1
  let laneEnds: number[] = []
  const flush = () => {
    const n = cluster.reduce((m, x) => Math.max(m, x.lane + 1), 1)
    cluster.forEach((x) => (x.lanes = n))
    cluster = []
    laneEnds = []
  }
  segs.forEach((g) => {
    if (cluster.length && g.s >= clusterEnd) flush()
    let lane = laneEnds.findIndex((end) => end <= g.s)
    if (lane === -1) {
      lane = laneEnds.length
      laneEnds.push(g.e)
    } else laneEnds[lane] = g.e
    g.lane = lane
    cluster.push(g)
    clusterEnd = Math.max(clusterEnd, g.e)
  })
  if (cluster.length) flush()
  return segs
}

const itemsOnDay = (items: ScheduleItem[], spaceId: string, key: string) =>
  items.filter((i) => i.spaceId === spaceId && dayKey(i.start) === key)

const endMin = (d: Date) => minOfDay(d) || 1440

function widen(bounds: MinuteWindow, items: ScheduleItem[]): MinuteWindow {
  let { start: open, end: close } = bounds
  items.forEach((i) => {
    open = Math.min(open, Math.floor(minOfDay(i.start) / 60) * 60)
    close = Math.max(close, Math.ceil(endMin(i.end) / 60) * 60)
  })
  return { start: open, end: close }
}

/* One space's resolved hours, widened only by its own items that day. */
export function resourceDayBounds(c: Constraints, spaceId: string, items: ScheduleItem[], key: string): MinuteWindow {
  const w = widen({ start: c.openMinute, end: c.closeMinute }, itemsOnDay(items, spaceId, key))
  const open = Math.max(0, w.start)
  return { start: open, end: Math.min(1440, Math.max(w.end, open + 60)) }
}

/* Several spaces side by side: the widest of their hours, widened by their items that day. */
export function candidatesDayBounds(spaces: { id: string; constraints: Constraints }[], items: ScheduleItem[], key: string): MinuteWindow {
  if (!spaces.length) return { start: DEFAULT_OPEN_MIN, end: DEFAULT_CLOSE_MIN }
  let w: MinuteWindow = { start: 1440, end: 0 }
  spaces.forEach((s) => {
    w = { start: Math.min(w.start, s.constraints.openMinute), end: Math.max(w.end, s.constraints.closeMinute) }
  })
  const ids = new Set(spaces.map((s) => s.id))
  w = widen(w, items.filter((i) => ids.has(i.spaceId) && dayKey(i.start) === key))
  const open = Math.max(0, w.start)
  let close = Math.min(1440, w.end)
  if (close <= open) close = Math.min(1440, open + 60)
  return { start: open, end: close }
}

/* Grid bounds for a time grid: union of the shown spaces' hours, widened by rendered segments. */
export function gridBounds(segsBy: Record<string, DaySegment[]>, constraints: Constraints[]): MinuteWindow {
  let open = DEFAULT_OPEN_MIN
  let close = DEFAULT_CLOSE_MIN
  if (constraints.length) {
    open = Math.min(...constraints.map((c) => c.openMinute))
    close = Math.max(...constraints.map((c) => c.closeMinute))
  }
  Object.values(segsBy).forEach((segs) => segs.forEach((g) => {
    open = Math.min(open, Math.floor(g.s / 60) * 60)
    close = Math.max(close, Math.ceil(g.e / 60) * 60)
  }))
  open = Math.max(0, open)
  close = Math.min(1440, close)
  if (close <= open) close = Math.min(1440, open + 60)
  return { start: open, end: close }
}

/* Free windows for one space on one day inside [open, close] (mock: computeFree). */
export function computeFree(c: Constraints, spaceId: string, items: ScheduleItem[], key: string, open: number, close: number): MinuteWindow[] {
  if (c.holidays.includes(key)) return []
  const effOpen = Math.max(open, c.openMinute)
  const effClose = Math.min(close, c.closeMinute)
  if (effClose <= effOpen) return []
  const busy = itemsOnDay(items, spaceId, key)
    .map((i) => ({ s: minOfDay(i.start), e: endMin(i.end) }))
    .sort((a, b) => a.s - b.s)
  const out: MinuteWindow[] = []
  let cursor = effOpen
  busy.forEach(({ s, e }) => {
    if (s > cursor) out.push({ start: cursor, end: Math.min(s, effClose) })
    cursor = Math.max(cursor, e)
  })
  if (cursor < effClose) out.push({ start: cursor, end: effClose })
  return out.filter((w) => w.end > w.start)
}

/* Client-side mirror of the server's window validation, for live form feedback only. */
export function validateWindowLocal(c: Constraints | null, spaceName: string, start: Date | null, end: Date | null, allowPast = false):
  { code: string; message: string } | null {
  if (!start || !end) return { code: 'validation.missing_field', message: 'Start and end are both required.' }
  if (end <= start) return { code: 'validation.end_before_start', message: 'End must be after start.' }
  if (!allowPast && start < new Date()) return { code: 'validation.start_in_past', message: 'Start must not be in the past.' }
  if (!c) return null
  const key = dayKey(start)
  if (c.holidays.includes(key))
    return { code: 'validation.holiday_closed', message: `${spaceName}'s building is closed for a holiday on ${key}.` }
  const sMin = minOfDay(start)
  const eMin = endMin(end)
  if (sMin < c.openMinute || eMin > c.closeMinute)
    return {
      code: 'validation.outside_hours',
      message: `${spaceName} can only be booked between ${String(c.openMinute / 60).padStart(2, '0')}:00 and ${String(c.closeMinute / 60).padStart(2, '0')}:00.`,
    }
  const mins = (end.getTime() - start.getTime()) / 60000
  if (mins < c.minBookingMinutes)
    return { code: 'validation.duration_below_min', message: `Minimum booking length here is ${c.minBookingMinutes} minutes.` }
  if (mins > c.maxBookingHours * 60)
    return { code: 'validation.duration_above_max', message: `Maximum booking length here is ${c.maxBookingHours} hours.` }
  return null
}

export function findOverlap<T extends ScheduleItem>(items: T[], spaceId: string, start: Date, end: Date, excludeId?: string): T | undefined {
  return items.find((i) => i.spaceId === spaceId && i.id !== excludeId && i.start < end && start < i.end)
}
