import { create } from 'zustand'
import { addDays, dayAt, dayKey, monthBounds, todayKey, weekStart } from '../lib/dateUtils'
import type { ScheduleId } from './modalStore'

export type ScheduleMode = 'month' | 'week' | 'day'

/* The admins' "Everyone" choice in the User filter. */
export const EVERYONE = 'everyone'

export interface ScheduleConfig {
  /* '' = every building, otherwise only bookings in that building (and its closed days shown grey). */
  buildingId: string
  /* '' = every floor of that building, otherwise one floor id (needs a building). */
  floorId: string
  /* 'all' = every space, otherwise one space id. */
  spaceId: string
  /* Admins: whose bookings, EVERYONE (the default) or one user's id. Employees always see their own. */
  userId: string
  from: string
  to: string
  mode: ScheduleMode
  /* true when From / To is a range the admin typed, not a whole month / week / day. */
  custom: boolean
  savedMonth: { from: string; to: string } | null
}

function initial(spaceId: string): ScheduleConfig {
  const { from, to } = monthBounds(todayKey())
  return { buildingId: '', floorId: '', spaceId, userId: EVERYONE, from, to, mode: 'month', custom: false, savedMonth: null }
}

function periodFor(mode: ScheduleMode, anchor: string): { from: string; to: string } {
  if (mode === 'week') {
    const f = weekStart(anchor)
    return { from: f, to: dayKey(addDays(dayAt(f), 6)) }
  }
  if (mode === 'day') return { from: anchor, to: anchor }
  return monthBounds(anchor)
}

/* Days from `from` to `to`, both included. */
const daysIn = (from: string, to: string) => Math.round((dayAt(to).getTime() - dayAt(from).getTime()) / 86_400_000) + 1

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
  setPeriod: (id, anchor) => get().patch(id, { ...periodFor(get().configs[id].mode, anchor), custom: false }),
  setMode: (id, mode) => {
    const cfg = get().configs[id]
    if (cfg.mode === mode && !cfg.custom) return
    const savedMonth = cfg.mode === 'month' && !cfg.custom ? { from: cfg.from, to: cfg.to } : cfg.savedMonth
    if (mode === 'month') {
      const s = savedMonth ?? monthBounds(cfg.from)
      get().patch(id, { mode, savedMonth, from: s.from, to: s.to, custom: false })
      return
    }
    const today = todayKey()
    const anchor = today >= cfg.from && today <= cfg.to ? today : cfg.from
    get().patch(id, { mode, savedMonth, ...periodFor(mode, anchor), custom: false })
  },
  /* A typed range steps by its own length; a whole period steps to the next month / week / day. */
  shift: (id, dir) => {
    const cfg = get().configs[id]
    if (cfg.custom) {
      const len = daysIn(cfg.from, cfg.to) * dir
      get().patch(id, { from: dayKey(addDays(dayAt(cfg.from), len)), to: dayKey(addDays(dayAt(cfg.to), len)) })
    } else if (cfg.mode === 'month') {
      const d = dayAt(cfg.from)
      get().setPeriod(id, dayKey(new Date(Date.UTC(d.getUTCFullYear(), d.getUTCMonth() + dir, 1))))
    } else {
      get().setPeriod(id, dayKey(addDays(dayAt(cfg.from), (cfg.mode === 'week' ? 7 : 1) * dir)))
    }
  },
  /* From / To is used exactly as typed (To never before From). The view follows its length: one day is the
   * Day view, up to a week is one column per day, anything longer is the month grid with the days outside
   * the range greyed. */
  onRangeInput: (id, from, to) => {
    const f = from || todayKey()
    const t = to && to >= f ? to : f
    const len = daysIn(f, t)
    get().patch(id, { from: f, to: t, mode: len === 1 ? 'day' : len <= 7 ? 'week' : 'month', custom: true })
  },
}))
