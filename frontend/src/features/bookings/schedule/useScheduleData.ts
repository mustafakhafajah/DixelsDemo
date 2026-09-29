import { useMemo } from 'react'
import { useBookings, useBuildings, useMaintenance, useSpaces } from '../../../api/hooks'
import type { ScheduleItem, Space } from '../../../api/types'
import { useSession } from '../../../app/session'
import type { ClosedRules } from '../../../lib/closedDays'
import { addDays, dayAt } from '../../../lib/dateUtils'
import type { ScheduleId } from '../../../state/modalStore'
import { useScheduleStore } from '../../../state/scheduleStore'

/* Items a schedule instance shows (mock: scopeBookings): one user's bookings, plus blocked time on those spaces. */
export function useScheduleData(id: ScheduleId) {
  const cfg = useScheduleStore((s) => s.configs[id])
  const session = useSession()
  const spacesQ = useSpaces()
  const buildingsQ = useBuildings()
  const from = useMemo(() => dayAt(cfg.from), [cfg.from])
  const to = useMemo(() => addDays(dayAt(cfg.to), 1), [cfg.to])
  /* Employees have no space picker: they always see all of their own bookings. */
  const multiSpace = !session.isAdmin || cfg.spaceId === 'all'
  const singleSpaceId = multiSpace ? '' : cfg.spaceId
  const ownerId = cfg.userId || session.userId

  const ready = !!session.userId
  const scoped = useBookings({ from, to, ownerUserId: ownerId, spaceId: singleSpaceId || undefined }, ready)
  /* Everyone's bookings on the single space, so free windows and slot prefill see the whole picture. */
  const spaceAll = useBookings({ from, to, spaceId: singleSpaceId }, ready && !!singleSpaceId)
  const maint = useMaintenance({ from, to }, ready)

  return useMemo(() => {
    const spaces = spacesQ.data ?? []
    const spaceById = new Map(spaces.map((s) => [s.id, s]))
    /* A chosen building (and floor) keeps only what happens there. */
    const inScope = (spaceId: string) => {
      const s = spaceById.get(spaceId)
      return (!cfg.buildingId || s?.buildingId === cfg.buildingId) && (!cfg.floorId || s?.floorId === cfg.floorId)
    }
    const mine = (scoped.data ?? []).filter((b) => inScope(b.spaceId))
    const spaceIds = multiSpace ? [...new Set(mine.map((b) => b.spaceId))] : [cfg.spaceId]
    const cleaning = (maint.data ?? []).filter((m) => spaceIds.includes(m.spaceId))
    const items: ScheduleItem[] = [...mine, ...cleaning].sort((a, b) => a.start.getTime() - b.start.getTime())
    const busyOnSpace: ScheduleItem[] = singleSpaceId ? [...(spaceAll.data ?? []), ...cleaning] : items
    const shownSpaces = spaceIds.map((sid) => spaces.find((s) => s.id === sid)).filter(Boolean) as Space[]
    const single = singleSpaceId ? spaces.find((s) => s.id === singleSpaceId) ?? null : null
    const building = cfg.buildingId ? buildingsQ.data?.find((b) => b.id === cfg.buildingId) ?? null : null
    /* Whose closed days to grey out: the chosen building's, or the one chosen space's building. With every
     * building shown there is no single answer, so nothing is greyed. */
    const closed: ClosedRules | null = building ?? single?.constraints ?? null
    return {
      cfg,
      items,
      busyOnSpace,
      shownSpaces,
      single,
      multiSpace,
      closed,
      loading: scoped.isLoading || maint.isLoading,
      /* The first failed request, if any, and a way to ask again. */
      error: [spacesQ, scoped, spaceAll, maint].find((q) => q.isError)?.error ?? null,
      retry: () => { spacesQ.refetch(); scoped.refetch(); spaceAll.refetch(); maint.refetch() },
      spaces,
    }
  }, [spacesQ.data, buildingsQ.data, scoped.data, spaceAll.data, maint.data, cfg, multiSpace, singleSpaceId, scoped.isLoading, maint.isLoading, spacesQ, scoped, spaceAll, maint])
}

export type ScheduleData = ReturnType<typeof useScheduleData>
