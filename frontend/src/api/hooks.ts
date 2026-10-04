import { useMemo } from 'react'
import { keepPreviousData, useMutation, useQuery, useQueryClient } from '@tanstack/react-query'
import { useAuth } from 'react-oidc-context'
import { useApi } from './client'
import {
  toBooking,
  toBusy,
  type BusyWindowDto,
  toMaintenance,
  type Booking,
  type BookingDto,
  type Building,
  type Floor,
  type ListResult,
  type PagedResult,
  type Maintenance,
  type MaintenanceDto,
  type MaintenanceScopeType,
  type Profile,
  type Space,
  type SpaceType,
  type Translation,
  type UserLookup,
  type Window,
} from './types'

const ESTATE_KEYS = [['buildings'], ['floors'], ['spaces'], ['space-types']]

function useEnabled() {
  return !!useAuth().user?.access_token
}

/* ─── Reads ─── */

export function useProfile() {
  const api = useApi()
  return useQuery({
    queryKey: ['profile'],
    queryFn: () => api<Profile>('GET', '/api/app/users/me'),
    enabled: useEnabled(),
    staleTime: 5 * 60_000,
  })
}

/* What the signed-in user may do, as ABP grants it right now. Refetched on focus and every minute,
 * and invalidated (PERMISSION_KEYS) when someone changes grants, so hidden features follow along. */
export const PERMISSION_KEYS = [['permissions']]

interface ApplicationConfiguration {
  auth?: { grantedPolicies?: Record<string, boolean> }
}

export function useGrantedPolicies() {
  const api = useApi()
  return useQuery({
    queryKey: ['permissions'],
    queryFn: async () => (await api<ApplicationConfiguration>('GET', '/api/abp/application-configuration')).auth?.grantedPolicies ?? {},
    enabled: useEnabled(),
    staleTime: 30_000,
    refetchInterval: 60_000,
    refetchOnWindowFocus: true,
  })
}

export function useUsers(enabled = true) {
  const api = useApi()
  const ok = useEnabled()
  return useQuery({
    queryKey: ['users'],
    queryFn: async () => (await api<ListResult<UserLookup>>('GET', '/api/app/user-summaries')).items,
    enabled: ok && enabled,
    staleTime: 5 * 60_000,
  })
}

export function useBuildings() {
  const api = useApi()
  return useQuery({
    queryKey: ['buildings'],
    queryFn: async () => (await api<ListResult<Building>>('GET', '/api/app/buildings')).items,
    enabled: useEnabled(),
  })
}

/* Every floor, or only one building's (pickers ask for the chosen building, so each pick is a new request).
 * Pass enabled=false to wait until a building is chosen. */
export function useFloors(buildingId?: string, enabled = true) {
  const api = useApi()
  const ok = useEnabled()
  return useQuery({
    queryKey: ['floors', { buildingId: buildingId || null }],
    queryFn: async () => (await api<ListResult<Floor>>('GET', '/api/app/floors', undefined, { buildingId: buildingId || undefined })).items,
    enabled: ok && enabled,
  })
}

/* Every space, or only one building's / floor's, for pickers. */
export function useSpaces(filter: { buildingId?: string; floorId?: string } = {}, enabled = true) {
  const api = useApi()
  const ok = useEnabled()
  return useQuery({
    queryKey: ['spaces', 'list', { buildingId: filter.buildingId || null, floorId: filter.floorId || null }],
    queryFn: async () => (await api<ListResult<Space>>('GET', '/api/app/spaces', undefined, {
      buildingId: filter.buildingId || undefined,
      floorId: filter.floorId || undefined,
    })).items,
    enabled: ok && enabled,
  })
}

export interface PageQuery {
  page: number
  pageSize: number
}

/* One page of an estate list (buildings, floors, space types), paged on the server. */
function useEstatePage<T>(key: string, path: string, q: PageQuery, params: Record<string, string | undefined> = {}) {
  const api = useApi()
  return useQuery({
    queryKey: [key, 'page', q],
    queryFn: () => api<PagedResult<T>>('GET', path, undefined, {
      skipCount: String((q.page - 1) * q.pageSize),
      maxResultCount: String(q.pageSize),
      ...params,
    }),
    enabled: useEnabled(),
    /* Keep the current page on screen while the next one loads, instead of flashing "Loading…". */
    placeholderData: keepPreviousData,
  })
}

export const useBuildingsPage = (q: PageQuery) => useEstatePage<Building>('buildings', '/api/app/buildings', q)

export const useFloorsPage = (q: PageQuery & { buildingId?: string }) =>
  useEstatePage<Floor>('floors', '/api/app/floors', q, { buildingId: q.buildingId || undefined })

export const useSpaceTypesPage = (q: PageQuery) => useEstatePage<SpaceType>('space-types', '/api/app/space-types', q)

export interface FindSpacesQuery {
  buildingId?: string
  floorId?: string
  minCapacity?: number
  typeIds: string[]
  name?: string
  /* "Free only": the window the spaces must be free for. */
  freeFrom?: Date
  freeTo?: Date
}

/* "Find a space": the server filters and returns only bookable spaces, so the page never loads the whole estate.
 * The key starts with 'spaces', so saving a space refreshes these results too. */
export function useFindSpaces(q: FindSpacesQuery) {
  const api = useApi()
  return useQuery({
    queryKey: ['spaces', 'find', { ...q, freeFrom: q.freeFrom?.getTime(), freeTo: q.freeTo?.getTime() }],
    queryFn: async () => (await api<ListResult<Space>>('GET', '/api/app/spaces', undefined, {
      bookable: true,
      buildingId: q.buildingId,
      floorId: q.floorId,
      minCapacity: q.minCapacity || undefined,
      typeIds: q.typeIds,
      name: q.name,
      freeFromUtc: q.freeFrom?.toISOString(),
      freeToUtc: q.freeTo?.toISOString(),
    })).items,
    enabled: useEnabled(),
    /* Keep the current rooms on screen while a changed filter loads, instead of flashing "Loading…". */
    placeholderData: keepPreviousData,
  })
}

export function useSpaceTypes() {
  const api = useApi()
  return useQuery({
    queryKey: ['space-types'],
    queryFn: async () => (await api<ListResult<SpaceType>>('GET', '/api/app/space-types')).items,
    enabled: useEnabled(),
  })
}

export interface SpaceRegistryQuery {
  page: number
  pageSize: number
  buildingId?: string
  floorId?: string
  name?: string
  typeId?: string
}

/* Server-side paged and filtered, so the registry stays fast with thousands of spaces. */
export function useSpaceRegistry(q: SpaceRegistryQuery) {
  const api = useApi()
  return useQuery({
    queryKey: ['spaces', 'registry', q],
    queryFn: () => api<PagedResult<Space>>('GET', '/api/app/spaces', undefined, {
      skipCount: (q.page - 1) * q.pageSize,
      maxResultCount: q.pageSize,
      buildingId: q.buildingId,
      floorId: q.floorId,
      name: q.name,
      typeIds: q.typeId ? [q.typeId] : undefined,
    }),
    enabled: useEnabled(),
    /* Keep showing the current page while the next one loads, instead of flashing "Loading…". */
    placeholderData: keepPreviousData,
  })
}

export interface RangeFilter {
  from?: Date
  to?: Date
  spaceId?: string
  buildingId?: string
  floorId?: string
  ownerUserId?: string
  includeCancelled?: boolean
}

function rangeQuery(f: RangeFilter) {
  return {
    fromUtc: f.from?.toISOString(),
    toUtc: f.to?.toISOString(),
    spaceId: f.spaceId,
    buildingId: f.buildingId,
    floorId: f.floorId,
    ownerUserId: f.ownerUserId,
    includeCancelled: f.includeCancelled || undefined,
  }
}

const filterKey = (f: RangeFilter) => ({
  from: f.from?.getTime(), to: f.to?.getTime(), spaceId: f.spaceId, buildingId: f.buildingId, floorId: f.floorId,
  ownerUserId: f.ownerUserId,
  includeCancelled: !!f.includeCancelled,
})

/* keepPrevious: a changed filter keeps the current items on screen until the new ones arrive (the Schedule);
 * off by default, so a form never judges a new time against the old one's bookings. */
export function useBookings(filter: RangeFilter, enabled = true, keepPrevious = false) {
  const api = useApi()
  const ok = useEnabled()
  return useQuery({
    queryKey: ['bookings', filterKey(filter)],
    queryFn: async (): Promise<Booking[]> =>
      (await api<ListResult<BookingDto>>('GET', '/api/app/bookings', undefined, rangeQuery(filter))).items.map(toBooking),
    enabled: ok && enabled,
    refetchInterval: 60_000,
    placeholderData: keepPrevious ? keepPreviousData : undefined,
  })
}

/* Other people's bookings as bare busy windows (no id, owner or name). */
export function useBusy(filter: RangeFilter, enabled = true) {
  const api = useApi()
  const ok = useEnabled()
  return useQuery({
    queryKey: ['bookings', 'busy', filterKey(filter)],
    queryFn: async (): Promise<Booking[]> =>
      (await api<ListResult<BusyWindowDto>>('GET', '/api/app/busy-windows', undefined,
        {
          fromUtc: filter.from?.toISOString(), toUtc: filter.to?.toISOString(), spaceId: filter.spaceId,
          buildingId: filter.buildingId, floorId: filter.floorId,
        })).items.map(toBusy),
    enabled: ok && enabled,
    refetchInterval: 60_000,
  })
}

/* Everything that makes a time taken. Admins get every booking in full; everyone else gets their own
 * bookings plus grey "Busy" windows for the rest, because the server never sends them anyone else's. */
export function useAvailability(filter: RangeFilter, who: { isAdmin: boolean; userId: string }, enabled = true) {
  const all = useBookings(filter, enabled && who.isAdmin)
  const own = useBookings({ ...filter, ownerUserId: who.userId }, enabled && !who.isAdmin && !!who.userId)
  const busy = useBusy(filter, enabled && !who.isAdmin)
  const data = useMemo(() => (own.data && busy.data ? [...own.data, ...busy.data] : undefined), [own.data, busy.data])
  if (who.isAdmin) return all
  const failed = own.isError ? own : busy.isError ? busy : null
  return {
    data,
    isError: !!failed,
    error: failed?.error ?? null,
    isLoading: own.isLoading || busy.isLoading,
    refetch: () => Promise.all([own.refetch(), busy.refetch()]),
  }
}

export function useMaintenance(filter: RangeFilter, enabled = true, keepPrevious = false) {
  const api = useApi()
  const ok = useEnabled()
  return useQuery({
    queryKey: ['maintenance', filterKey(filter)],
    queryFn: async (): Promise<Maintenance[]> =>
      (await api<ListResult<MaintenanceDto>>('GET', '/api/app/maintenance-windows', undefined, rangeQuery(filter))).items.map(toMaintenance),
    enabled: ok && enabled,
    refetchInterval: 60_000,
    placeholderData: keepPrevious ? keepPreviousData : undefined,
  })
}

export function useBooking(id: string | null) {
  const api = useApi()
  const ok = useEnabled()
  return useQuery({
    queryKey: ['booking', id],
    queryFn: async () => toBooking(await api<BookingDto>('GET', `/api/app/bookings/${id}`)),
    enabled: ok && !!id,
  })
}

export function useMaintenanceWindow(id: string | null) {
  const api = useApi()
  const ok = useEnabled()
  return useQuery({
    queryKey: ['maintenance-window', id],
    queryFn: async () => toMaintenance(await api<MaintenanceDto>('GET', `/api/app/maintenance-windows/${id}`)),
    enabled: ok && !!id,
  })
}

/* ─── Mutations ─── */

export function useInvalidate() {
  const qc = useQueryClient()
  return (keys: string[][]) => Promise.all(keys.map((k) => qc.invalidateQueries({ queryKey: k })))
}

/* The space registry shows upcoming-booking counts, so booking changes refresh it too. */
const BOOKING_KEYS = [['bookings'], ['booking'], ['spaces', 'registry']]

export interface CreateBookingInput {
  spaceId: string
  startUtc: string
  endUtc: string
  idempotencyKey?: string
}

export function useCreateBooking() {
  const api = useApi()
  const invalidate = useInvalidate()
  return useMutation({
    mutationFn: (input: CreateBookingInput) => api<BookingDto>('POST', '/api/app/bookings', input).then(toBooking),
    onSuccess: () => invalidate(BOOKING_KEYS),
  })
}

export interface SeriesResult {
  seriesId: string | null
  created: BookingDto[]
  skipped: { startUtc: string; endUtc: string; errorCode: string; errorMessage: string }[]
}

export function useCreateBookingSeries() {
  const api = useApi()
  const invalidate = useInvalidate()
  return useMutation({
    mutationFn: (input: { spaceId: string; occurrences: Window[] }) =>
      api<SeriesResult>('POST', '/api/app/booking-series', input),
    onSuccess: () => invalidate(BOOKING_KEYS),
  })
}

export function useRescheduleBooking() {
  const api = useApi()
  const invalidate = useInvalidate()
  return useMutation({
    mutationFn: ({ id, ...body }: { id: string; startUtc: string; endUtc: string; expectedVersion: number }) =>
      api<BookingDto>('PATCH', `/api/app/bookings/${id}`, body).then(toBooking),
    onSettled: () => invalidate(BOOKING_KEYS),
  })
}

export function useCancelBooking() {
  const api = useApi()
  const invalidate = useInvalidate()
  return useMutation({
    mutationFn: (id: string) => api<BookingDto>('PATCH', `/api/app/bookings/${id}`, { lifecycle: 'cancelled' }).then(toBooking),
    onSettled: () => invalidate(BOOKING_KEYS),
  })
}

/* Cancels this booking and every later one in its series; a booking outside a series is cancelled on its own. */
export function useCancelBookingSeries() {
  const api = useApi()
  const invalidate = useInvalidate()
  return useMutation({
    mutationFn: async (b: Pick<Booking, 'id' | 'seriesId' | 'start'>): Promise<{ seriesId: string | null; cancelledCount: number }> => {
      if (!b.seriesId) {
        await api<BookingDto>('PATCH', `/api/app/bookings/${b.id}`, { lifecycle: 'cancelled' })
        return { seriesId: null, cancelledCount: 1 }
      }
      return api('PATCH', `/api/app/booking-series/${b.seriesId}`, { lifecycle: 'cancelled', fromUtc: b.start.toISOString() })
    },
    onSettled: () => invalidate(BOOKING_KEYS),
  })
}

export function useEndBookingEarly() {
  const api = useApi()
  const invalidate = useInvalidate()
  return useMutation({
    mutationFn: (id: string) => api<BookingDto>('PATCH', `/api/app/bookings/${id}`, { lifecycle: 'ended' }).then(toBooking),
    onSettled: () => invalidate(BOOKING_KEYS),
  })
}

export interface MaintenanceScopeInput {
  scopeType: MaintenanceScopeType
  scopeId: string
  occurrences: Window[]
}

export function usePreviewMaintenance(input: MaintenanceScopeInput | null) {
  const api = useApi()
  const ok = useEnabled()
  return useQuery({
    queryKey: ['maintenance-preview', input],
    queryFn: () =>
      api<{ spaceCount: number; totalAffected: number; perOccurrence: { startUtc: string; endUtc: string; affectedCount: number }[] }>(
        'POST', '/api/app/maintenance-windows/previews', input),
    enabled: ok && !!input && input.occurrences.length > 0,
  })
}

export function useScheduleMaintenance() {
  const api = useApi()
  const invalidate = useInvalidate()
  return useMutation({
    mutationFn: (input: MaintenanceScopeInput & { note?: string; cancelAffectedBookings?: boolean }) =>
      api<{ seriesId: string | null; created: number; affectedBookingsCount: number; cancelledBookingsCount: number }>(
        'POST', '/api/app/maintenance-windows', input),
    /* Blocking can cancel bookings, so booking views refresh too. */
    onSuccess: () => invalidate([['maintenance'], ['maintenance-window'], ...BOOKING_KEYS]),
  })
}

export function useCancelMaintenance() {
  const api = useApi()
  const invalidate = useInvalidate()
  return useMutation({
    mutationFn: (id: string) => api<MaintenanceDto>('PATCH', `/api/app/maintenance-windows/${id}`, { lifecycle: 'cancelled' }).then(toMaintenance),
    onSettled: () => invalidate([['maintenance'], ['maintenance-window']]),
  })
}

export interface BuildingInput {
  name: string
  translations: Translation[]
  timeZone: string
  isBookable: boolean
  openHour: number
  closeHour: number
  minBookingMinutes: number
  maxBookingHours: number
  holidays: string[]
  closedWeekdays: number[]
}

export interface FloorInput {
  buildingId: string
  name: string
  translations: Translation[]
  isBookable: boolean
  openHourOverride: number | null
  closeHourOverride: number | null
  minBookingMinutesOverride: number | null
  maxBookingHoursOverride: number | null
}

export interface SpaceInput {
  name: string
  translations: Translation[]
  typeId: string
  isBookable: boolean
  buildingId: string
  floorId: string
  capacity: number
  note: string
  openHourOverride: number | null
  closeHourOverride: number | null
  minBookingMinutesOverride: number | null
  maxBookingHoursOverride: number | null
}

function useEstateMutation<TInput>(fn: (api: ReturnType<typeof useApi>, input: TInput) => Promise<unknown>) {
  const api = useApi()
  const invalidate = useInvalidate()
  return useMutation({
    mutationFn: (input: TInput) => fn(api, input),
    onSuccess: () => invalidate(ESTATE_KEYS),
  })
}

export const useSaveBuilding = () =>
  useEstateMutation<{ id?: string; body: BuildingInput }>((api, { id, body }) =>
    id ? api<Building>('PUT', `/api/app/buildings/${id}`, body) : api<Building>('POST', '/api/app/buildings', body))

export const useSaveFloor = () =>
  useEstateMutation<{ id?: string; body: FloorInput }>((api, { id, body }) =>
    id ? api<Floor>('PUT', `/api/app/floors/${id}`, body) : api<Floor>('POST', '/api/app/floors', body))

export const useSaveSpace = () =>
  useEstateMutation<{ id?: string; body: SpaceInput }>((api, { id, body }) =>
    id ? api<Space>('PUT', `/api/app/spaces/${id}`, body) : api<Space>('POST', '/api/app/spaces', body))

export type EstateKind = 'building' | 'floor' | 'space'

/* The collection each estate kind lives in, e.g. 'building' -> /api/app/buildings. */
const ESTATE_PATH: Record<EstateKind, string> = { building: 'buildings', floor: 'floors', space: 'spaces' }

export const useSetBookable = () =>
  useEstateMutation<{ kind: EstateKind; id: string; isBookable: boolean }>((api, { kind, id, isBookable }) =>
    api('PATCH', `/api/app/${ESTATE_PATH[kind]}/${id}`, { isBookable }))

export interface EstateScope {
  scopeType: MaintenanceScopeType
  scopeId: string
}

/* A scope's upcoming bookings, e.g. /api/app/floors/{id}/upcoming-bookings. */
const SCOPE_PATH: Record<MaintenanceScopeType, string> = { Building: 'buildings', Floor: 'floors', Space: 'spaces' }
const upcomingPath = (s: EstateScope) => `/api/app/${SCOPE_PATH[s.scopeType]}/${s.scopeId}/upcoming-bookings`

/* Bookings in a space, floor or building that have not started yet (admin only). */
export function useUpcomingCount(scope: EstateScope | null) {
  const api = useApi()
  const ok = useEnabled()
  return useQuery({
    queryKey: ['upcoming-count', scope],
    queryFn: () => api<number>('GET', `${upcomingPath(scope!)}/count`),
    enabled: ok && !!scope,
  })
}

/* Explicit admin action: cancel exactly those not-yet-started bookings. */
export function useCancelUpcoming() {
  const api = useApi()
  const invalidate = useInvalidate()
  return useMutation({
    mutationFn: (scope: EstateScope) => api<{ cancelledCount: number }>('PATCH', upcomingPath(scope), { lifecycle: 'cancelled' }),
    onSettled: () => invalidate([...BOOKING_KEYS, ['upcoming-count']]),
  })
}

export const useSaveSpaceType = () =>
  useEstateMutation<{ id?: string; name: string; translations: Translation[] }>((api, { id, ...body }) =>
    id ? api<SpaceType>('PUT', `/api/app/space-types/${id}`, body) : api<SpaceType>('POST', '/api/app/space-types', body))

/* Only allowed when no space uses the type; the server answers space_type.in_use otherwise. */
export const useDeleteSpaceType = () =>
  useEstateMutation<string>((api, id) => api('DELETE', `/api/app/space-types/${id}`))
