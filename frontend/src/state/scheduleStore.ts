import { create } from 'zustand'
import { addDays, dayAt, dayKey, monthBounds, todayKey, weekStart } from '../lib/dateUtils'
import type { ScheduleId } from './modalStore'

export type ScheduleMode = 'month' | 'week' | 'day'

export interface ScheduleConfig {
  /* 'all' = every space the user has booked, otherwise one space id. */
  spaceId: string
  /* Whose schedule 'my' shows; null = the signed-in user. */
  userId: string | null
  from: string
  to: string
  mode: ScheduleMode
  savedMonth: { from: string; to: string } | null
}

function initial(spaceId: string): ScheduleConfig {
  const { from, to } = monthBounds(todayKey())
  return { spaceId, userId: null, from, to, mode: 'month', savedMonth: null }
}

function periodFor(mode: ScheduleMode, anchor: string): { from: string; to: string } {
  if (mode === 'week') {
    const f = weekStart(anchor)
    return { from: f, to: dayKey(addDays(dayAt(f), 6)) }
  }
  if (mode === 'day') return { from: anchor, to: anchor }
  return monthBounds(anchor)
}

interface ScheduleState {
  configs: Record<ScheduleId, ScheduleConfig>
  patch: (id: ScheduleId, p: Partial<ScheduleConfig>) => void
  setMode: (id: ScheduleId, mode: ScheduleMode) => void
  setPeriod: (id: ScheduleId, anchor: string) => void
  shift: (id: ScheduleId, dir: 1 | -1) => void
  onRangeInput: (id: ScheduleId, from: string, to: string) => void
}

export const useScheduleStore = create<ScheduleState>((set, get) => ({
  configs: { my: initial('all') },
  patch: (id, p) => set({ configs: { ...get().configs, [id]: { ...get().configs[id], ...p } } }),
  setPeriod: (id, anchor) => get().patch(id, periodFor(get().configs[id].mode, anchor)),
  setMode: (id, mode) => {
    const cfg = get().configs[id]
    if (cfg.mode === mode) return
    const savedMonth = cfg.mode === 'month' ? { from: cfg.from, to: cfg.to } : cfg.savedMonth
    if (mode === 'month') {
      const s = savedMonth ?? monthBounds(cfg.from)
      get().patch(id, { mode, savedMonth, from: s.from, to: s.to })
      return
    }
    const today = todayKey()
    const anchor = today >= cfg.from && today <= cfg.to ? today : cfg.from
    get().patch(id, { mode, savedMonth, ...periodFor(mode, anchor) })
  },
  shift: (id, dir) => {
    const cfg = get().configs[id]
    if (cfg.mode === 'month') {
      const d = dayAt(cfg.from)
      get().setPeriod(id, dayKey(new Date(Date.UTC(d.getUTCFullYear(), d.getUTCMonth() + dir, 1))))
    } else {
      get().setPeriod(id, dayKey(addDays(dayAt(cfg.from), (cfg.mode === 'week' ? 7 : 1) * dir)))
    }
  },
  /* Every mode is a fixed-length period: re-anchor on whichever field the user touched. */
  onRangeInput: (id, from, to) => {
    const cfg = get().configs[id]
    const f = from || todayKey()
    get().setPeriod(id, f === cfg.from && to && to !== cfg.to ? to : f)
  },
}))
