import { useMemo } from 'react'
import { useBookings, useBuildings, useMaintenance, useSpaces } from '../../../api/hooks'
import type { ScheduleItem, Space } from '../../../api/types'
import { useSession } from '../../../app/session'
import { P } from '../../../auth/permissions'
import type { ClosedRules } from '../../../lib/closedDays'
import { addDays, dayAt } from '../../../lib/dateUtils'
import type { ScheduleId } from '../../../state/modalStore'
import { EVERYONE, useScheduleConfig } from '../../../state/scheduleStore'

/* Items a schedule instance shows (mock: scopeBookings). With Bookings.ViewAll: everyone's bookings (or one person's)
 * plus the blocked time in the chosen building / floor / space. Without it: your own bookings plus blocked time on those spaces. */
export function useScheduleData(id: ScheduleId) {
  const [cfg] = useScheduleConfig(id)
  const session = useSession()
  const spacesQ = useSpaces()
  const buildingsQ = useBuildings()
  const from = useMemo(() => dayAt(cfg.from), [cfg.from])
  const to = useMemo(() => addDays(dayAt(cfg.to), 1), [cfg.to])
  /* Without Bookings.ViewAll there is no space picker: you always see all of your own bookings. */
  const seesAll = session.can(P.Bookings.ViewAll)
  const multiSpace = !seesAll || cfg.spaceId === 'all'
  const singleSpaceId = multiSpace ? '' : cfg.spaceId
  /* With ViewAll: no owner filter for "Everyone", otherwise the chosen person. Without it the server only ever
   * sends your own, and the request says so too. */
  const everyone = seesAll && cfg.userId === EVERYONE
  const ownerId = !seesAll ? session.userId : everyone ? undefined : cfg.userId

  const ready = !!session.userId
  /* Every filter is sent to the server, so each pick is a new request; the current items stay on screen
   * until the new ones arrive. */
  const buildingId = cfg.buildingId || undefined
  const floorId = cfg.floorId || undefined
  const spaceId = singleSpaceId || undefined
  const scoped = useBookings({ from, to, ownerUserId: ownerId, spaceId, buildingId, floorId }, ready, true)
  /* Everyone's bookings on the single space, so free windows and slot prefill see the whole picture. */
  const spaceAll = useBookings({ from, to, spaceId: singleSpaceId }, ready && !!singleSpaceId && !everyone, true)
  const maint = useMaintenance({ from, to, spaceId, buildingId, floorId }, ready, true)

  return useMemo(() => {
    const spaces = spacesQ.data ?? []
    const mine = scoped.data ?? []
    /* With ViewAll, all the blocked time the server sent for the filters; "My Schedule" shows it only on the
     * spaces you booked. */
    const bookedSpaces = new Set(mine.map((b) => b.spaceId))
    const cleaning = seesAll ? maint.data ?? [] : (maint.data ?? []).filter((m) => bookedSpaces.has(m.spaceId))
    const spaceIds = multiSpace ? [...new Set([...bookedSpaces, ...cleaning.map((m) => m.spaceId)])] : [cfg.spaceId]
    const items: ScheduleItem[] = [...mine, ...cleaning].sort((a, b) => a.start.getTime() - b.start.getTime())
    const busyOnSpace: ScheduleItem[] = singleSpaceId ? [...(everyone ? mine : spaceAll.data ?? []), ...cleaning] : items
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
      everyone,
      closed,
      loading: scoped.isLoading || maint.isLoading || spaceAll.isLoading,
      /* The first failed request, if any, and a way to ask again. */
      error: [spacesQ, scoped, spaceAll, maint].find((q) => q.isError)?.error ?? null,
      retry: () => { spacesQ.refetch(); scoped.refetch(); spaceAll.refetch(); maint.refetch() },
      spaces,
    }
  }, [spacesQ, buildingsQ, scoped, spaceAll, maint, cfg, multiSpace, everyone, singleSpaceId, seesAll])
}

export type ScheduleData = ReturnType<typeof useScheduleData>
