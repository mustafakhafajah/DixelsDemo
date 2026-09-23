import { useMemo } from 'react'
import { useBookings, useMaintenance, useSpaces } from '../../../api/hooks'
import type { ScheduleItem, Space } from '../../../api/types'
import { useSession } from '../../../app/session'
import { addDays, dayAt } from '../../../lib/dateUtils'
import type { ScheduleId } from '../../../state/modalStore'
import { useScheduleStore } from '../../../state/scheduleStore'

/* Items a schedule instance shows (mock: scopeBookings). 'my' = one user's bookings (+ cleaning
 * on those spaces); 'adm' = everything on one chosen space. */
export function useScheduleData(id: ScheduleId) {
  const cfg = useScheduleStore((s) => s.configs[id])
  const session = useSession()
  const spacesQ = useSpaces()
  const from = useMemo(() => dayAt(cfg.from), [cfg.from])
  const to = useMemo(() => addDays(dayAt(cfg.to), 1), [cfg.to])
  const multiSpace = id === 'my' && cfg.spaceId === 'all'
  const singleSpaceId = multiSpace ? '' : cfg.spaceId
  const ownerId = id === 'my' ? cfg.userId || session.userId : undefined

  const ready = !!session.userId && (id === 'my' || !!cfg.spaceId)
  const scoped = useBookings({ from, to, ownerUserId: ownerId, spaceId: singleSpaceId || undefined }, ready)
  /* Everyone's bookings on the single space, so free windows and slot prefill see the whole picture. */
  const spaceAll = useBookings({ from, to, spaceId: singleSpaceId }, ready && !!singleSpaceId && id === 'my')
  const maint = useMaintenance({ from, to }, ready)

  return useMemo(() => {
    const spaces = spacesQ.data ?? []
    const mine = scoped.data ?? []
    const spaceIds = id === 'my'
      ? multiSpace ? [...new Set(mine.map((b) => b.spaceId))] : [cfg.spaceId]
      : cfg.spaceId ? [cfg.spaceId] : []
    const cleaning = (maint.data ?? []).filter((m) => spaceIds.includes(m.spaceId))
    const items: ScheduleItem[] = [...mine, ...cleaning].sort((a, b) => a.start.getTime() - b.start.getTime())
    const busyOnSpace: ScheduleItem[] = singleSpaceId
      ? [...(id === 'my' ? spaceAll.data ?? [] : mine), ...cleaning]
      : items
    const shownSpaces = spaceIds.map((sid) => spaces.find((s) => s.id === sid)).filter(Boolean) as Space[]
    const single = singleSpaceId ? spaces.find((s) => s.id === singleSpaceId) ?? null : null
    return {
      cfg,
      items,
      busyOnSpace,
      shownSpaces,
      single,
      multiSpace,
      needsPick: id === 'adm' && !cfg.spaceId,
      loading: scoped.isLoading || maint.isLoading,
      spaces,
    }
  }, [spacesQ.data, scoped.data, spaceAll.data, maint.data, cfg, id, multiSpace, singleSpaceId, scoped.isLoading, maint.isLoading])
}

export type ScheduleData = ReturnType<typeof useScheduleData>
