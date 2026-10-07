import { RECURRENCE_SAFETY_CAP } from './constants'
import { addDays, dayAt, dayKey, weekStart } from './dateUtils'

export type RepeatFreq = 'none' | 'daily' | 'weekly' | 'monthly'

export interface RecurrenceRule {
  freq: Exclude<RepeatFreq, 'none'>
  interval: number
  /* 0=Sunday … 6=Saturday, matching getDay on the viewer's clock. Empty = start's weekday. */
  byDay: number[]
  end: { mode: 'count'; count: number } | { mode: 'until'; until: Date }
}

export interface Occurrence {
  start: Date
  end: Date
}

/* Port of mock/js/recurrence.js generateOccurrences: always includes the first occurrence. Like Teams, every
 * occurrence keeps the organiser's wall-clock time on the viewer's calendar, so it stays at 09:00 across a
 * clock change rather than drifting by an hour. */
export function generateOccurrences(start: Date, end: Date, rule: RecurrenceRule): { occurrences: Occurrence[]; truncated: boolean } {
  const durMs = end.getTime() - start.getTime()
  const h = start.getHours()
  const m = start.getMinutes()
  const at = (key: string) => dayAt(key, h, m)
  const interval = Math.max(1, Math.floor(rule.interval) || 1)
  const withinEnd = (d: Date) => rule.end.mode !== 'until' || d <= rule.end.until
  const doneByCount = (list: Occurrence[]) => rule.end.mode === 'count' && list.length >= rule.end.count
  const out: Occurrence[] = []
  const push = (s: Date) => out.push({ start: new Date(s), end: new Date(s.getTime() + durMs) })

  if (rule.freq === 'daily') {
    let key = dayKey(start)
    while (out.length < RECURRENCE_SAFETY_CAP) {
      const cur = at(key)
      if (!withinEnd(cur)) break
      push(cur)
      if (doneByCount(out)) break
      key = dayKey(addDays(dayAt(key, 12), interval))
    }
  } else if (rule.freq === 'weekly') {
    const days = (rule.byDay.length ? rule.byDay : [start.getDay()])
      .slice()
      .sort((a, b) => ((a + 6) % 7) - ((b + 6) % 7))
    let weekMonday = dayAt(weekStart(dayKey(start)), 12)
    let first = true
    outer: while (out.length < RECURRENCE_SAFETY_CAP) {
      for (const dow of days) {
        const cand = at(dayKey(addDays(weekMonday, (dow + 6) % 7)))
        if (first && cand < start) continue
        if (!withinEnd(cand)) break outer
        push(cand)
        if (doneByCount(out) || out.length >= RECURRENCE_SAFETY_CAP) break outer
      }
      first = false
      weekMonday = addDays(weekMonday, 7 * interval)
    }
  } else {
    let n = 0
    while (out.length < RECURRENCE_SAFETY_CAP) {
      const first = new Date(start.getFullYear(), start.getMonth() + n * interval, 1)
      const daysInMonth = new Date(first.getFullYear(), first.getMonth() + 1, 0).getDate()
      const target = new Date(first.getFullYear(), first.getMonth(), Math.min(start.getDate(), daysInMonth), h, m)
      if (!withinEnd(target)) break
      push(target)
      if (doneByCount(out)) break
      n++
    }
  }

  return { occurrences: out, truncated: out.length >= RECURRENCE_SAFETY_CAP }
}
