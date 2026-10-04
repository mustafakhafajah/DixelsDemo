import { create } from 'zustand'
import { addDays, dayAt, dayKey, minOfDay, roundUp30, todayKey } from '../lib/dateUtils'

export type FindDuration = 30 | 60 | 120 | 'custom'

export interface FindFilters {
  date: string
  time: number
  duration: FindDuration
  customEnd: number | null
  buildingId: string
  floorId: string
  /* "Free only": list only the rooms free for the chosen time and duration (asked of the server). */
  freeOnly: boolean
  minCapacity: number
  /* Space type ids. */
  types: string[]
  query: string
}

interface FindState extends FindFilters {
  patch: (p: Partial<FindFilters>) => void
  now: () => void
  shiftDay: (dir: 1 | -1) => void
  toggleType: (typeId: string) => void
  /* Clear filters: every room criterion and Free only go back to "any"; the date, time and length stay.
   * criteriaVersion counts the clears, so the filter panel can start over (e.g. close a custom capacity box). */
  clearCriteria: () => void
  criteriaVersion: number
}

function nowFields() {
  const n = roundUp30(new Date())
  return { date: dayKey(n), time: minOfDay(n) }
}

export const useFindStore = create<FindState>((set, get) => ({
  ...nowFields(),
  duration: 60,
  customEnd: null,
  buildingId: '',
  floorId: '',
  freeOnly: false,
  minCapacity: 0,
  types: [],
  query: '',
  patch: (p) => set(p),
  now: () => set(nowFields()),
  shiftDay: (dir) => set({ date: dayKey(addDays(dayAt(get().date), dir)) }),
  criteriaVersion: 0,
  clearCriteria: () => set({ buildingId: '', floorId: '', minCapacity: 0, types: [], query: '', freeOnly: false, criteriaVersion: get().criteriaVersion + 1 }),
  toggleType: (t) => {
    const types = get().types
    set({ types: types.includes(t) ? types.filter((x) => x !== t) : [...types, t] })
  },
}))

export const findToday = () => useFindStore.getState().patch({ date: todayKey() })
