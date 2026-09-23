import { create } from 'zustand'
import type { SpaceType } from '../api/types'
import { addDays, dayAt, dayKey, minOfDay, roundUp30, todayKey } from '../lib/dateUtils'

export type FindDuration = 30 | 60 | 120 | 'custom'

export interface FindFilters {
  date: string
  time: number
  duration: FindDuration
  customEnd: number | null
  buildingId: string
  floorName: string
  minCapacity: number
  types: SpaceType[]
  query: string
}

interface FindState extends FindFilters {
  patch: (p: Partial<FindFilters>) => void
  now: () => void
  shiftDay: (dir: 1 | -1) => void
  toggleType: (t: SpaceType) => void
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
  floorName: '',
  minCapacity: 0,
  types: [],
  query: '',
  patch: (p) => set(p),
  now: () => set(nowFields()),
  shiftDay: (dir) => set({ date: dayKey(addDays(dayAt(get().date), dir)) }),
  toggleType: (t) => {
    const types = get().types
    set({ types: types.includes(t) ? types.filter((x) => x !== t) : [...types, t] })
  },
}))

export const findToday = () => useFindStore.getState().patch({ date: todayKey() })
