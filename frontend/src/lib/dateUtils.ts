import i18n from 'i18next'
import { intlLocale } from '../i18n/languages'

/* Date helpers ported from mock/js/utils.js. Like Teams or Outlook, the app shows and picks every time on the
 * viewer's own clock (the browser's time zone); the server still stores and sends UTC instants. */

export const pad = (n: number) => String(n).padStart(2, '0')

export function dayKey(d: Date): string {
  return `${d.getFullYear()}-${pad(d.getMonth() + 1)}-${pad(d.getDate())}`
}

/* The instant a wall-clock time happens on the viewer's day; minutes past 59 (or 1440 = midnight after) roll over. */
export function dayAt(key: string, h = 0, m = 0): Date {
  const [y, mo, d] = key.split('-').map(Number)
  return new Date(y, mo - 1, d, h, m, 0, 0)
}

export function fromDateTime(dateStr: string, timeStr: string): Date | null {
  if (!dateStr || !timeStr) return null
  const [h, mi] = timeStr.split(':').map(Number)
  return dayAt(dateStr, h, mi)
}

export const hm = (d: Date) => `${pad(d.getHours())}:${pad(d.getMinutes())}`
export const stamp = (d: Date) => `${dayKey(d)} ${hm(d)}`

/* The viewer's offset from UTC at this instant, e.g. "+03:00" (getTimezoneOffset counts the other way round). */
export function offsetOf(d: Date): string {
  const off = -d.getTimezoneOffset()
  const abs = Math.abs(off)
  return `${off < 0 ? '-' : '+'}${pad(Math.floor(abs / 60))}:${pad(abs % 60)}`
}
export const stampOffset = (d: Date) => `${dayKey(d)} ${hm(d)} ${offsetOf(d)}`
export const minOfDay = (d: Date) => d.getHours() * 60 + d.getMinutes()
export const minLabel = (m: number) => `${pad(Math.floor(m / 60))}:${pad(m % 60)}`

/* The same wall-clock time n days later, even across a clock change (so not always n × 24 hours). */
export function addDays(d: Date, n: number): Date {
  const x = new Date(d)
  x.setDate(x.getDate() + n)
  return x
}

export const addMin = (d: Date, n: number) => new Date(d.getTime() + n * 60000)

/* Dates in words, in the chosen language (Intl decides the order, names and digits), on the viewer's own clock
 * like everything else in the app. One formatter per language and set of options. */
const formatters = new Map<string, Intl.DateTimeFormat>()
function dateFormat(opts: Intl.DateTimeFormatOptions): Intl.DateTimeFormat {
  const locale = intlLocale()
  const key = `${locale}|${JSON.stringify(opts)}`
  let f = formatters.get(key)
  if (!f) {
    f = new Intl.DateTimeFormat(locale, opts)
    formatters.set(key, f)
  }
  return f
}

export const formatDate = (d: Date, opts: Intl.DateTimeFormatOptions) => dateFormat(opts).format(d)
export const formatDateRange = (a: Date, b: Date, opts: Intl.DateTimeFormatOptions) => dateFormat(opts).formatRange(a, b)

/* A weekday's name from its getDay() number (0 = Sunday); 1 January 2023 was a Sunday. Noon keeps it clear of
 * any clock change at midnight. */
export const weekdayName = (day: number, width: 'long' | 'short' = 'long') =>
  formatDate(new Date(2023, 0, 1 + day, 12), { weekday: width })

/* "1h 30m", in the chosen language's short units. */
export function durationLabel(mins: number): string {
  const h = Math.floor(mins / 60)
  const m = Math.round(mins % 60)
  return [h ? i18n.t('time.hoursShort', { count: h }) : '', m || !h ? i18n.t('time.minutesShort', { count: m }) : '']
    .filter(Boolean).join(' ')
}

export function roundUp30(d: Date): Date {
  const x = new Date(d)
  x.setSeconds(0, 0)
  const m = x.getMinutes()
  x.setMinutes(m < 30 ? 30 : 60)
  return x
}

export const overlaps = (aS: Date, aE: Date, bS: Date, bE: Date) => aS < bE && bS < aE

export function relative(d: Date): string {
  const m = Math.round((d.getTime() - Date.now()) / 60000)
  const f = new Intl.RelativeTimeFormat(intlLocale(), { style: 'short' })
  if (Math.abs(m) < 60) return f.format(m, 'minute')
  return Math.abs(m) < 1440 ? f.format(Math.round(m / 60), 'hour') : f.format(Math.round(m / 1440), 'day')
}

/* Monday-first week start for a day key. */
export function weekStart(key: string): string {
  const d = dayAt(key)
  return dayKey(addDays(d, -((d.getDay() + 6) % 7)))
}

export function monthBounds(key: string): { from: string; to: string } {
  const d = dayAt(key)
  const y = d.getFullYear()
  const mo = d.getMonth()
  return { from: `${y}-${pad(mo + 1)}-01`, to: dayKey(new Date(y, mo + 1, 0)) }
}

export const todayKey = () => dayKey(new Date())

/* API instants arrive as ISO strings; ABP may omit the Z for UTC kinds, so force UTC. */
export function parseUtc(s: string): Date {
  return new Date(/[zZ]|[+-]\d\d:?\d\d$/.test(s) ? s : `${s}Z`)
}

/* The next whole step (default 15 min) at or after this minute of the day, e.g. 10:07 -> 10:15. */
export const ceilStep = (min: number, step = 15) => Math.ceil(min / step) * step

/* The earliest start a form may offer: now, rounded up to the next 15 minutes (may be tomorrow). */
export function earliestStart(now = new Date()): Date {
  const d = new Date(now)
  d.setSeconds(0, 0)
  return addMin(d, ceilStep(d.getMinutes()) - d.getMinutes())
}

/* Moves a start/end pair on a day so the start is not before `earliest`, keeping its length.
 * Returns the (possibly unchanged) times as "HH:MM"; the end also stays after the start. */
export function keepWindowAhead(day: string, startTime: string, endTime: string, earliest: Date, fallbackMin = 60) {
  const s = fromDateTime(day, startTime)
  const e = fromDateTime(day, endTime)
  if (!s || !e) return { startTime, endTime }
  const length = e > s ? e.getTime() - s.getTime() : fallbackMin * 60000
  const start = s < earliest ? earliest : s
  const end = new Date(start.getTime() + length)
  /* The end cannot spill into the next day on a single-day form: stop at 23:59 worth of steps. */
  return { startTime: hm(start), endTime: dayKey(end) === day ? hm(end) : '23:45' }
}
