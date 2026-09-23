/* UTC-only helpers ported from mock/js/utils.js. The whole app reasons in UTC. */

export const pad = (n: number) => String(n).padStart(2, '0')

export function dayKey(d: Date): string {
  return `${d.getUTCFullYear()}-${pad(d.getUTCMonth() + 1)}-${pad(d.getUTCDate())}`
}

export function dayAt(key: string, h = 0, m = 0): Date {
  const [y, mo, d] = key.split('-').map(Number)
  return new Date(Date.UTC(y, mo - 1, d, h, m, 0, 0))
}

export function fromDateTime(dateStr: string, timeStr: string): Date | null {
  if (!dateStr || !timeStr) return null
  const [h, mi] = timeStr.split(':').map(Number)
  return dayAt(dateStr, h, mi)
}

export const hm = (d: Date) => `${pad(d.getUTCHours())}:${pad(d.getUTCMinutes())}`
export const stamp = (d: Date) => `${dayKey(d)} ${hm(d)}`
export const stampOffset = (d: Date) => `${dayKey(d)} ${hm(d)} +00:00`
export const isoZ = (d: Date) => `${dayKey(d)}T${hm(d)}:00+00:00`
export const minOfDay = (d: Date) => d.getUTCHours() * 60 + d.getUTCMinutes()
export const minLabel = (m: number) => `${pad(Math.floor(m / 60))}:${pad(m % 60)}`

export function addDays(d: Date, n: number): Date {
  const x = new Date(d)
  x.setUTCDate(x.getUTCDate() + n)
  return x
}

export const addMin = (d: Date, n: number) => new Date(d.getTime() + n * 60000)

const DAY_NAMES = ['Sunday', 'Monday', 'Tuesday', 'Wednesday', 'Thursday', 'Friday', 'Saturday']
const MONTH_NAMES = ['January', 'February', 'March', 'April', 'May', 'June', 'July', 'August', 'September', 'October', 'November', 'December']
export const dayName = (d: Date) => DAY_NAMES[d.getUTCDay()]
export const monthName = (d: Date) => MONTH_NAMES[d.getUTCMonth()]

export function durationLabel(mins: number): string {
  const h = Math.floor(mins / 60)
  const m = Math.round(mins % 60)
  return (h ? `${h}h` : '') + (m ? `${h ? ' ' : ''}${m}m` : h ? '' : '0m')
}

export function roundUp30(d: Date): Date {
  const x = new Date(d)
  x.setUTCSeconds(0, 0)
  const m = x.getUTCMinutes()
  x.setUTCMinutes(m < 30 ? 30 : 60)
  return x
}

export const overlaps = (aS: Date, aE: Date, bS: Date, bE: Date) => aS < bE && bS < aE

export function relative(d: Date): string {
  const diff = d.getTime() - Date.now()
  const m = Math.round(Math.abs(diff) / 60000)
  const txt = m < 60 ? `${m} min` : m < 1440 ? `${Math.round(m / 60)} h` : `${Math.round(m / 1440)} d`
  return diff >= 0 ? `in ${txt}` : `${txt} ago`
}

/* Monday-first week start for a day key. */
export function weekStart(key: string): string {
  const d = dayAt(key)
  return dayKey(addDays(d, -((d.getUTCDay() + 6) % 7)))
}

export function monthBounds(key: string): { from: string; to: string } {
  const d = dayAt(key)
  const y = d.getUTCFullYear()
  const mo = d.getUTCMonth()
  return { from: `${y}-${pad(mo + 1)}-01`, to: dayKey(new Date(Date.UTC(y, mo + 1, 0))) }
}

export const todayKey = () => dayKey(new Date())

/* API instants arrive as ISO strings; ABP may omit the Z for UTC kinds, so force UTC. */
export function parseUtc(s: string): Date {
  return new Date(/[zZ]|[+-]\d\d:?\d\d$/.test(s) ? s : `${s}Z`)
}
