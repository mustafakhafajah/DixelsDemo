import { RECURRENCE_SAFETY_CAP } from './constants'
import { addDays, dayAt, dayKey, weekStart } from './dateUtils'

export type RepeatFreq = 'none' | 'daily' | 'weekly' | 'monthly'

export interface RecurrenceRule {
  freq: Exclude<RepeatFreq, 'none'>
  interval: number
  /* 0=Sunday … 6=Saturday, matching getUTCDay. Empty = start's weekday. */
  byDay: number[]
  end: { mode: 'count'; count: number } | { mode: 'until'; until: Date }
}

export interface Occurrence {
  start: Date
  end: Date
}

/* Port of mock/js/recurrence.js generateOccurrences: always includes the first occurrence. */
export function generateOccurrences(start: Date, end: Date, rule: RecurrenceRule): { occurrences: Occurrence[]; truncated: boolean } {
  const durMs = end.getTime() - start.getTime()
  const interval = Math.max(1, Math.floor(rule.interval) || 1)
  const withinEnd = (d: Date) => rule.end.mode !== 'until' || d <= rule.end.until
  const doneByCount = (list: Occurrence[]) => rule.end.mode === 'count' && list.length >= rule.end.count
  const out: Occurrence[] = []
  const push = (s: Date) => out.push({ start: new Date(s), end: new Date(s.getTime() + durMs) })

  if (rule.freq === 'daily') {
    let cur = new Date(start)
    while (out.length < RECURRENCE_SAFETY_CAP) {
      if (!withinEnd(cur)) break
      push(cur)
      if (doneByCount(out)) break
      cur = addDays(cur, interval)
    }
  } else if (rule.freq === 'weekly') {
    const days = (rule.byDay.length ? rule.byDay : [start.getUTCDay()])
      .slice()
      .sort((a, b) => ((a + 6) % 7) - ((b + 6) % 7))
    const startMidnight = dayAt(dayKey(start))
    let weekMonday = dayAt(weekStart(dayKey(start)))
    let first = true
    outer: while (out.length < RECURRENCE_SAFETY_CAP) {
      for (const dow of days) {
        const dayBase = addDays(weekMonday, (dow + 6) % 7)
        const cand = new Date(dayBase.getTime() + (start.getTime() - startMidnight.getTime()))
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
      const target = new Date(Date.UTC(start.getUTCFullYear(), start.getUTCMonth() + n * interval, 1,
        start.getUTCHours(), start.getUTCMinutes(), start.getUTCSeconds()))
      const daysInMonth = new Date(Date.UTC(target.getUTCFullYear(), target.getUTCMonth() + 1, 0)).getUTCDate()
      target.setUTCDate(Math.min(start.getUTCDate(), daysInMonth))
      if (!withinEnd(target)) break
      push(target)
      if (doneByCount(out)) break
      n++
    }
  }

  return { occurrences: out, truncated: out.length >= RECURRENCE_SAFETY_CAP }
}
