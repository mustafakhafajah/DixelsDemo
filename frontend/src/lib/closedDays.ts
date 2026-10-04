import i18n from 'i18next'
import { intlLocale } from '../i18n/languages'
import { dayAt, dayKey, weekdayName } from './dateUtils'
import type { MinuteWindow } from './laneLayout'

/* A building's closed days (holiday dates and weekly closed days). They are days in the building's own time
 * zone, not UTC; mirrors the server's BuildingCalendar. A Building and a space's Constraints both fit. */
export interface ClosedRules {
  holidays: string[]
  closedWeekdays: number[]
  timeZone: string
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
    f = new Intl.DateTimeFormat('en-CA', { timeZone: isValidTimeZone(tz) ? tz : 'UTC', year: 'numeric', month: '2-digit', day: '2-digit' })
    formatters.set(tz, f)
  }
  return f
}

/* The date ("YYYY-MM-DD") at this instant in the given time zone. */
export function localDayKey(d: Date, tz: string): string {
  const p = Object.fromEntries(formatter(tz || 'UTC').formatToParts(d).map((x) => [x.type, x.value]))
  return `${p.year}-${p.month}-${p.day}`
}

/* Is this date (in the building's own calendar) a holiday or a weekly closed day? */
export function isClosedDay(r: ClosedRules, key: string): boolean {
  return r.holidays.includes(key) || r.closedWeekdays.includes(dayAt(key).getUTCDay())
}

export const isClosedAt = (r: ClosedRules, instant: Date) => isClosedDay(r, localDayKey(instant, r.timeZone))

/* Why a window can't be booked, e.g. "on Fridays" or "for a holiday on 2026-12-25", or null. Every local day
 * the window touches counts: its start day and the day of its last minute. */
export function closedReason(r: ClosedRules, start: Date, end: Date): string | null {
  if (!r.holidays.length && !r.closedWeekdays.length) return null
  const first = localDayKey(start, r.timeZone)
  const last = localDayKey(new Date(Math.max(start.getTime(), end.getTime() - 1)), r.timeZone)
  for (let k = first; k <= last; k = dayKey(new Date(dayAt(k).getTime() + 86400000))) {
    if (r.holidays.includes(k)) return i18n.t('closed.onHoliday', { date: k })
    const wd = dayAt(k).getUTCDay()
    if (r.closedWeekdays.includes(wd)) return i18n.t('closed.onWeekday', { day: weekdayName(wd) })
  }
  return null
}

/* The parts of a UTC day (minutes 0-1440) that are open, i.e. not on a closed local day. Offsets are whole
 * quarter hours, so checking every 15 minutes finds the local midnight exactly. */
const openCache = new Map<string, MinuteWindow[]>()
export function openParts(r: ClosedRules, key: string): MinuteWindow[] {
  if (!r.holidays.length && !r.closedWeekdays.length) return [{ start: 0, end: 1440 }]
  const cacheKey = `${r.timeZone}|${r.holidays.join(',')}|${r.closedWeekdays.join(',')}|${key}`
  const hit = openCache.get(cacheKey)
  if (hit) return hit
  const out: MinuteWindow[] = []
  for (let m = 0; m < 1440; m += 15) {
    if (isClosedAt(r, dayAt(key, 0, m))) continue
    const last = out[out.length - 1]
    if (last && last.end === m) last.end = m + 15
    else out.push({ start: m, end: m + 15 })
  }
  openCache.set(cacheKey, out)
  return out
}

/* Keeps only the open parts of each window. */
export function withoutClosed(r: ClosedRules, key: string, windows: MinuteWindow[]): MinuteWindow[] {
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
