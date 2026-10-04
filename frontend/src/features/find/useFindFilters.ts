import { useState } from 'react'
import { create } from 'zustand'
import { addDays, dayAt, dayKey, minLabel, minOfDay, roundUp30, todayKey } from '../../lib/dateUtils'
import { urlParam, useUrlState, type UrlParam } from '../../lib/useUrlState'

export type FindDuration = 30 | 60 | 120 | 'custom'

/* A time of day as "09:30" in the address, minutes after midnight in the page. */
const timeParam = (name: string, fallback: number | null): UrlParam<number | null> => ({
  name, fallback,
  parse: (r) => {
    const m = /^(\d{2}):(\d{2})$/.exec(r[0] ?? '')
    const min = m ? Number(m[1]) * 60 + Number(m[2]) : NaN
    return min >= 0 && min < 1440 ? min : undefined
  },
  format: (v) => (v == null ? [] : [minLabel(v)]),
})

const DURATIONS = ['30', '60', '120', 'custom'] as const
const toDuration = (v: (typeof DURATIONS)[number]): FindDuration => (v === 'custom' ? v : (Number(v) as FindDuration))

/* Counts the Clear filters clicks, so the filter panel can start over (e.g. close a custom capacity box).
 * Only for this visit; it is not part of a shared link. */
const useClears = create<{ count: number; bump: () => void }>((set) => ({ count: 0, bump: () => set((s) => ({ count: s.count + 1 })) }))

function nowFields() {
  const n = roundUp30(new Date())
  return { date: dayKey(n), time: minOfDay(n) }
}

/* The "Find a space" filters, all kept in the address so a copied link opens the same search:
 * ?date=2026-10-04&at=09:30&length=60|custom&until=11:00&free=1&building=…&floor=…&seats=6&type=…&type=…&q=…
 * Date and time left out mean "now" for whoever opens the link. */
export function useFindFilters() {
  /* "Now" as the page opened; a link without a date / time uses the opener's own now. */
  const [now] = useState(nowFields)
  const [v, set] = useUrlState({
    date: urlParam.day('date', now.date),
    time: timeParam('at', now.time),
    length: urlParam.oneOf('length', DURATIONS, '60'),
    customEnd: timeParam('until', null),
    buildingId: urlParam.text('building'),
    floorId: urlParam.text('floor'),
    freeOnly: urlParam.flag('free'),
    minCapacity: urlParam.int('seats', 0, 0, 100_000),
    types: urlParam.list('type'),
    query: urlParam.text('q'),
  })
  const clears = useClears()
  const time = v.time ?? now.time
  const duration = toDuration(v.length)

  const patch = (p: Partial<{
    date: string; time: number; duration: FindDuration; customEnd: number | null; buildingId: string; floorId: string
    freeOnly: boolean; minCapacity: number; types: string[]; query: string
  }>) => {
    const { duration: d, ...rest } = p
    set({ ...rest, ...(d === undefined ? {} : { length: String(d) as (typeof DURATIONS)[number] }) })
  }

  return {
    date: v.date,
    time,
    duration,
    customEnd: v.customEnd,
    buildingId: v.buildingId,
    floorId: v.floorId,
    freeOnly: v.freeOnly,
    minCapacity: v.minCapacity,
    types: v.types,
    query: v.query,
    patch,
    now: () => patch(nowFields()),
    today: () => patch({ date: todayKey() }),
    shiftDay: (dir: 1 | -1) => patch({ date: dayKey(addDays(dayAt(v.date), dir)) }),
    /* Clear filters: every room criterion and Free only go back to "any"; the date, time and length stay. */
    clearCriteria: () => {
      patch({ buildingId: '', floorId: '', minCapacity: 0, types: [], query: '', freeOnly: false })
      clears.bump()
    },
    criteriaVersion: clears.count,
    toggleType: (t: string) => patch({ types: v.types.includes(t) ? v.types.filter((x) => x !== t) : [...v.types, t] }),
  }
}
