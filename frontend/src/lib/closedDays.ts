import i18n from 'i18next'
import { intlLocale } from '../i18n/languages'
import { dayAt, dayKey, minLabel, minOfDay, pad, weekdayName } from './dateUtils'
import type { MinuteWindow } from './laneLayout'

/* A building's closed days (holiday dates and weekly closed days). They are days in the building's own time
 * zone, not the viewer's; mirrors the server's BuildingCalendar. A Building and a space's Constraints both fit. */
export interface ClosedRules {
  holidays: string[]
  closedWeekdays: number[]
  timeZone: string
}

/* Opening hours are minutes of the building's local day too (a space's Constraints fit). */
export interface OpeningRules extends ClosedRules {
  openMinute: number
  closeMinute: number
}

/* Weekday toggles, Monday first; the numbers are JS getDay() (0 = Sunday), as the server stores them.
 * Their names come from weekdayName(), in the chosen language. */
export const WEEKDAYS = [1, 2, 3, 4, 5, 6, 0]

export function isValidTimeZone(tz: string): boolean {
  try {
    new Intl.DateTimeFormat('en-CA', { timeZone: tz })
    return true
  } catch {
    return false
  }
}

const formatters = new Map<string, Intl.DateTimeFormat>()
function formatter(tz: string): Intl.DateTimeFormat {
  let f = formatters.get(tz)
  if (!f) {
    /* An unknown zone counts as UTC, as on the server. */
    f = new Intl.DateTimeFormat('en-CA', {
      timeZone: isValidTimeZone(tz) ? tz : 'UTC', year: 'numeric', month: '2-digit', day: '2-digit', hour: '2-digit', minute: '2-digit', hourCycle: 'h23',
    })
    formatters.set(tz, f)
  }
  return f
}

/* Date keys on a building's calendar are plain calendar days, so they step and get their weekday in pure UTC
 * arithmetic: the viewer's own clock changes must not move them. */
export function calKeyAddDays(key: string, n: number): string {
  const [y, mo, d] = key.split('-').map(Number)
  const x = new Date(Date.UTC(y, mo - 1, d + n))
  return `${x.getUTCFullYear()}-${pad(x.getUTCMonth() + 1)}-${pad(x.getUTCDate())}`
}

/* The weekday of a calendar date, as getDay() numbers it (0 = Sunday). */
export function calWeekday(key: string): number {
  const [y, mo, d] = key.split('-').map(Number)
  return new Date(Date.UTC(y, mo - 1, d)).getUTCDay()
}

/* The browser's own time zone: the clock every time in the app is shown on. */
export const viewerZone = () => Intl.DateTimeFormat().resolvedOptions().timeZone

const localParts = (d: Date, tz: string) => Object.fromEntries(formatter(tz || 'UTC').formatToParts(d).map((x) => [x.type, x.value]))

/* The date ("YYYY-MM-DD") at this instant in the given time zone. */
export function localDayKey(d: Date, tz: string): string {
  const p = localParts(d, tz)
  return `${p.year}-${p.month}-${p.day}`
}

/* Minutes since midnight at this instant in the given time zone. */
export function localMinute(d: Date, tz: string): number {
  const p = localParts(d, tz)
  return Number(p.hour) * 60 + Number(p.minute)
}

/* "HH:MM" at this instant on the zone's clock. */
export const localLabel = (d: Date, tz: string) => minLabel(localMinute(d, tz))

/* The city part of a zone, for "14:00 Warsaw": Europe/Warsaw -> Warsaw, America/New_York -> New York. */
export function zoneCity(tz: string): string {
  const id = tz && isValidTimeZone(tz) ? tz : 'UTC'
  return id.split('/').pop()!.replace(/_/g, ' ')
}

/* The instant a wall-clock time (local day + minute, 1440 = midnight after) happens in the zone. A second step
 * corrects the offset on the day the clocks change. */
export function zonedInstant(key: string, minute: number, tz: string): Date {
  const [y, mo, d] = key.split('-').map(Number)
  const wall = Date.UTC(y, mo - 1, d, 0, minute)
  const offsetAt = (t: number) => {
    const p = localParts(new Date(t), tz)
    return Date.UTC(+p.year, +p.month - 1, +p.day, +p.hour, +p.minute) - t
  }
  const first = wall - offsetAt(wall)
  return new Date(wall - offsetAt(first))
}

/* Does the zone's clock read the same as the viewer's at this instant? Then showing its time as well adds nothing. */
export const sameClockAsViewer = (tz: string, at: Date) => localDayKey(at, tz) === dayKey(at) && localMinute(at, tz) === minOfDay(at)

/* The zone's own offset from UTC at this instant (whatever the viewer's is), for labels: "UTC+2", "UTC−3", "UTC+5:30". */
export function offsetLabel(tz: string, at: Date): string {
  const t = Math.floor(at.getTime() / 60000) * 60000
  const p = localParts(new Date(t), tz)
  const diff = Math.round((Date.UTC(+p.year, +p.month - 1, +p.day, +p.hour, +p.minute) - t) / 60000)
  const abs = Math.abs(diff)
  return `UTC${diff < 0 ? '−' : '+'}${Math.floor(abs / 60)}${abs % 60 ? `:${pad(abs % 60)}` : ''}`
}

/* "08:00–09:00 Warsaw time" for a window on a building's clock, or null when that clock reads as the viewer's.
 * Shown next to the viewer's own times the app works in. */
export const localRange = (start: Date, end: Date, tz: string) => sameClockAsViewer(tz, start) ? null
  : i18n.t('time.localRange', { from: localLabel(start, tz), to: localLabel(end, tz), zone: zoneCity(tz) })

/* Does the window fit the opening hours on the building's own clock? It must start at or after opening and end by
 * closing on the same local day; ending exactly at local midnight counts as the day before. Mirrors the server's
 * BuildingCalendar.IsWithinHours. */
export function isWithinHours(r: OpeningRules, start: Date, end: Date): boolean {
  let endDay = localDayKey(end, r.timeZone)
  let endMin = localMinute(end, r.timeZone)
  if (endMin === 0 && end > start) {
    endDay = calKeyAddDays(endDay, -1)
    endMin = 1440
  }
  return endDay === localDayKey(start, r.timeZone) && localMinute(start, r.timeZone) >= r.openMinute && endMin <= r.closeMinute
}

/* Is this date (in the building's own calendar) a holiday or a weekly closed day? */
export function isClosedDay(r: ClosedRules, key: string): boolean {
  return r.holidays.includes(key) || r.closedWeekdays.includes(calWeekday(key))
}

export const isClosedAt = (r: ClosedRules, instant: Date) => isClosedDay(r, localDayKey(instant, r.timeZone))

/* Why a window can't be booked, e.g. "on Fridays" or "for a holiday on 2026-12-25", or null. Every local day
 * the window touches counts: its start day and the day of its last minute. */
export function closedReason(r: ClosedRules, start: Date, end: Date): string | null {
  if (!r.holidays.length && !r.closedWeekdays.length) return null
  const first = localDayKey(start, r.timeZone)
  const last = localDayKey(new Date(Math.max(start.getTime(), end.getTime() - 1)), r.timeZone)
  for (let k = first; k <= last; k = calKeyAddDays(k, 1)) {
    if (r.holidays.includes(k)) return i18n.t('closed.onHoliday', { date: k })
    const wd = calWeekday(k)
    if (r.closedWeekdays.includes(wd)) return i18n.t('closed.onWeekday', { day: weekdayName(wd) })
  }
  return null
}

/* The parts of the viewer's day (minutes 0-1440) that are open: not on a closed day of the building, and inside
 * the opening hours on the building's clock. Offsets are whole quarter hours and hours are whole hours, so checking
 * every 15 minutes finds each building midnight, opening and closing exactly. The answer depends on the viewer's
 * zone too, so that is part of the cache key. */
const openCache = new Map<string, MinuteWindow[]>()
export function openParts(r: OpeningRules, key: string): MinuteWindow[] {
  const allHours = r.openMinute <= 0 && r.closeMinute >= 1440
  if (!r.holidays.length && !r.closedWeekdays.length && allHours) return [{ start: 0, end: 1440 }]
  const cacheKey = `${viewerZone()}|${r.timeZone}|${r.holidays.join(',')}|${r.closedWeekdays.join(',')}|${r.openMinute}-${r.closeMinute}|${key}`
  const hit = openCache.get(cacheKey)
  if (hit) return hit
  const out: MinuteWindow[] = []
  for (let m = 0; m < 1440; m += 15) {
    const at = dayAt(key, 0, m)
    if (isClosedAt(r, at)) continue
    const local = localMinute(at, r.timeZone)
    if (local < r.openMinute || local >= r.closeMinute) continue
    const last = out[out.length - 1]
    if (last && last.end === m) last.end = m + 15
    else out.push({ start: m, end: m + 15 })
  }
  openCache.set(cacheKey, out)
  return out
}

/* The stretch of the viewer's day (minutes) the opening hours cover, closed days aside: what a time grid shows for it. */
export function hoursSpan(r: OpeningRules, key: string): MinuteWindow {
  const parts = openParts({ ...r, holidays: [], closedWeekdays: [] }, key)
  return parts.length ? { start: parts[0].start, end: parts[parts.length - 1].end } : { start: r.openMinute, end: r.closeMinute }
}

/* Keeps only the open parts of each window. */
export function onlyOpen(r: OpeningRules, key: string, windows: MinuteWindow[]): MinuteWindow[] {
  const open = openParts(r, key)
  return windows.flatMap((w) => open
    .map((o) => ({ start: Math.max(w.start, o.start), end: Math.min(w.end, o.end) }))
    .filter((x) => x.end > x.start))
}

/* "Closed Fri, Sat" for a building's list line; empty when it has no weekly closed days. */
export function closedWeekdaysLabel(days: number[]): string {
  const names = WEEKDAYS.filter((d) => days.includes(d)).map((d) => weekdayName(d, 'short'))
  return names.length ? i18n.t('closed.weekdays', { days: new Intl.ListFormat(intlLocale(), { style: 'short', type: 'unit' }).format(names) }) : ''
}
