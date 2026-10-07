import { useMemo } from 'react'
import { create } from 'zustand'
import { addDays, dayAt, dayKey, monthBounds, todayKey, weekStart } from '../lib/dateUtils'
import { urlParam, useUrlState } from '../lib/useUrlState'
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

/* The month on screen before switching to Week or Day, so Month comes back to it. Only for this visit;
 * it is not part of a shared link. */
const useSavedMonth = create<Record<string, { from: string; to: string } | null>>(() => ({}))

const MODES = ['month', 'week', 'day'] as const

/* A schedule's filters and period, kept in the address so a copied link opens the same schedule:
 * ?view=week&from=2026-10-05&to=2026-10-11&building=…&floor=…&space=…&user=…
 * Left out: the current month, every building / floor / space, and "Everyone". */
export function useScheduleConfig(id: ScheduleId) {
  const [url, set] = useUrlState({
    mode: urlParam.oneOf('view', MODES, 'month'),
    from: urlParam.day('from', ''),
    to: urlParam.day('to', ''),
    buildingId: urlParam.text('building'),
    floorId: urlParam.text('floor'),
    spaceId: urlParam.text('space', 'all'),
    userId: urlParam.text('user', EVERYONE),
  })
  /* No usable range in the address: the current month / week / day. */
  const range = url.from && url.to && url.to >= url.from ? { from: url.from, to: url.to } : periodFor(url.mode, url.from || todayKey())
  const { mode, buildingId, floorId, spaceId, userId } = url
  /* The same object while nothing changes, so what is worked out from it isn't redone on every render. */
  const cfg = useMemo((): ScheduleConfig => {
    const whole = periodFor(mode, range.from)
    return {
      mode, buildingId, userId, from: range.from, to: range.to,
      floorId: buildingId ? floorId : '',
      spaceId: buildingId && floorId ? spaceId : 'all',
      custom: whole.from !== range.from || whole.to !== range.to,
    }
  }, [mode, buildingId, floorId, spaceId, userId, range.from, range.to])

  /* The period is written only when it isn't today's whole month / week / day. */
  const patch = (p: Partial<Omit<ScheduleConfig, 'custom'>>) => {
    const mode = p.mode ?? cfg.mode
    const from = p.from ?? cfg.from
    const to = p.to ?? cfg.to
    const current = periodFor(mode, todayKey())
    const isCurrent = current.from === from && current.to === to
    set({ ...p, mode, from: isCurrent ? '' : from, to: isCurrent ? '' : to })
  }
  const setPeriod = (anchor: string) => patch(periodFor(cfg.mode, anchor))

  const actions = {
    patch,
    setPeriod,
    setMode: (mode: ScheduleMode) => {
      if (cfg.mode === mode && !cfg.custom) return
      const saved = useSavedMonth.getState()[id] ?? null
      const savedMonth = cfg.mode === 'month' && !cfg.custom ? { from: cfg.from, to: cfg.to } : saved
      useSavedMonth.setState({ [id]: savedMonth })
      if (mode === 'month') {
        patch({ mode, ...(savedMonth ?? monthBounds(cfg.from)) })
        return
      }
      const today = todayKey()
      const anchor = today >= cfg.from && today <= cfg.to ? today : cfg.from
      patch({ mode, ...periodFor(mode, anchor) })
    },
    /* A typed range steps by its own length; a whole period steps to the next month / week / day. */
    shift: (dir: 1 | -1) => {
      if (cfg.custom) {
        const len = daysIn(cfg.from, cfg.to) * dir
        patch({ from: dayKey(addDays(dayAt(cfg.from), len)), to: dayKey(addDays(dayAt(cfg.to), len)) })
      } else if (cfg.mode === 'month') {
        const d = dayAt(cfg.from)
        setPeriod(dayKey(new Date(d.getFullYear(), d.getMonth() + dir, 1)))
      } else {
        setPeriod(dayKey(addDays(dayAt(cfg.from), (cfg.mode === 'week' ? 7 : 1) * dir)))
      }
    },
    /* From / To is used exactly as typed (To never before From). The view follows its length: one day is the
     * Day view, up to a week is one column per day, anything longer is the month grid with the days outside
     * the range greyed. */
    onRangeInput: (from: string, to: string) => {
      const f = from || todayKey()
      const t = to && to >= f ? to : f
      const len = daysIn(f, t)
      patch({ from: f, to: t, mode: len === 1 ? 'day' : len <= 7 ? 'week' : 'month' })
    },
  }
  return [cfg, actions] as const
}
