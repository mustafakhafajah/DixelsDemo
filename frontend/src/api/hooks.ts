import { useMutation, useQuery, useQueryClient } from '@tanstack/react-query'
import { useAuth } from 'react-oidc-context'
import { useApi } from './client'
import {
  toBooking,
  toMaintenance,
  type Booking,
  type BookingDto,
  type Building,
  type EstateStatus,
  type Floor,
  type ListResult,
  type Maintenance,
  type MaintenanceDto,
  type MaintenanceScopeType,
  type Profile,
  type Space,
  type SpaceType,
  type UserLookup,
  type Window,
} from './types'

const ESTATE_KEYS = [['buildings'], ['floors'], ['spaces']]

function useEnabled() {
  return !!useAuth().user?.access_token
}

/* ─── Reads ─── */

export function useProfile() {
  const api = useApi()
  return useQuery({
    queryKey: ['profile'],
    queryFn: () => api<Profile>('GET', '/api/app/profile-lookup/current'),
    enabled: useEnabled(),
    staleTime: 5 * 60_000,
  })
}

export function useUsers(enabled = true) {
  const api = useApi()
  const ok = useEnabled()
  return useQuery({
    queryKey: ['users'],
    queryFn: async () => (await api<ListResult<UserLookup>>('GET', '/api/app/profile-lookup/users')).items,
    enabled: ok && enabled,
    staleTime: 5 * 60_000,
  })
}

export function useBuildings() {
  const api = useApi()
  return useQuery({
    queryKey: ['buildings'],
    queryFn: async () => (await api<ListResult<Building>>('GET', '/api/app/building')).items,
    enabled: useEnabled(),
  })
}

export function useFloors() {
  const api = useApi()
  return useQuery({
    queryKey: ['floors'],
    queryFn: async () => (await api<ListResult<Floor>>('GET', '/api/app/floor')).items,
    enabled: useEnabled(),
  })
}

export function useSpaces() {
  const api = useApi()
  return useQuery({
    queryKey: ['spaces'],
    queryFn: async () => (await api<ListResult<Space>>('GET', '/api/app/space')).items,
    enabled: useEnabled(),
  })
}

export interface RangeFilter {
  from?: Date
  to?: Date
  spaceId?: string
  ownerUserId?: string
  includeCancelled?: boolean
}

function rangeQuery(f: RangeFilter) {
  return {
    FromUtc: f.from?.toISOString(),
    ToUtc: f.to?.toISOString(),
    SpaceId: f.spaceId,
    OwnerUserId: f.ownerUserId,
    IncludeCancelled: f.includeCancelled || undefined,
  }
}

const filterKey = (f: RangeFilter) => ({
  from: f.from?.getTime(), to: f.to?.getTime(), spaceId: f.spaceId, ownerUserId: f.ownerUserId,
  includeCancelled: !!f.includeCancelled,
})

export function useBookings(filter: RangeFilter, enabled = true) {
  const api = useApi()
  const ok = useEnabled()
  return useQuery({
    queryKey: ['bookings', filterKey(filter)],
    queryFn: async (): Promise<Booking[]> =>
      (await api<ListResult<BookingDto>>('GET', '/api/app/booking', undefined, rangeQuery(filter))).items.map(toBooking),
    enabled: ok && enabled,
    refetchInterval: 60_000,
  })
}

export function useMaintenance(filter: RangeFilter, enabled = true) {
  const api = useApi()
  const ok = useEnabled()
  return useQuery({
    queryKey: ['maintenance', filterKey(filter)],
    queryFn: async (): Promise<Maintenance[]> =>
      (await api<ListResult<MaintenanceDto>>('GET', '/api/app/maintenance-window', undefined, rangeQuery(filter))).items.map(toMaintenance),
    enabled: ok && enabled,
    refetchInterval: 60_000,
  })
}

export function useBooking(id: string | null) {
  const api = useApi()
  const ok = useEnabled()
  return useQuery({
    queryKey: ['booking', id],
    queryFn: async () => toBooking(await api<BookingDto>('GET', `/api/app/booking/${id}`)),
    enabled: ok && !!id,
  })
}

export function useMaintenanceWindow(id: string | null) {
  const api = useApi()
  const ok = useEnabled()
  return useQuery({
    queryKey: ['maintenance-window', id],
    queryFn: async () => toMaintenance(await api<MaintenanceDto>('GET', `/api/app/maintenance-window/${id}`)),
    enabled: ok && !!id,
  })
}

/* ─── Mutations ─── */

function useInvalidate() {
  const qc = useQueryClient()
  return (keys: string[][]) => Promise.all(keys.map((k) => qc.invalidateQueries({ queryKey: k })))
}

const BOOKING_KEYS = [['bookings'], ['booking']]

export interface CreateBookingInput {
  spaceId: string
  startUtc: string
  endUtc: string
  parking: boolean
  idempotencyKey?: string
}

export function useCreateBooking() {
  const api = useApi()
  const invalidate = useInvalidate()
  return useMutation({
    mutationFn: (input: CreateBookingInput) => api<BookingDto>('POST', '/api/app/booking', input).then(toBooking),
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
    mutationFn: (input: { spaceId: string; occurrences: Window[]; parking: boolean }) =>
      api<SeriesResult>('POST', '/api/app/booking/series', input),
    onSuccess: () => invalidate(BOOKING_KEYS),
  })
}

export function useRescheduleBooking() {
  const api = useApi()
  const invalidate = useInvalidate()
  return useMutation({
    mutationFn: ({ id, ...body }: { id: string; startUtc: string; endUtc: string; expectedVersion: number }) =>
      api<BookingDto>('POST', `/api/app/booking/${id}/reschedule`, body).then(toBooking),
    onSettled: () => invalidate(BOOKING_KEYS),
  })
}

export function useCancelBooking() {
  const api = useApi()
  const invalidate = useInvalidate()
  return useMutation({
    mutationFn: (id: string) => api<BookingDto>('POST', `/api/app/booking/${id}/cancel`).then(toBooking),
    onSettled: () => invalidate(BOOKING_KEYS),
  })
}

export function useCancelBookingSeries() {
  const api = useApi()
  const invalidate = useInvalidate()
  return useMutation({
    mutationFn: (id: string) =>
      api<{ seriesId: string | null; cancelledCount: number }>('POST', `/api/app/booking/${id}/cancel-series-from`),
    onSettled: () => invalidate(BOOKING_KEYS),
  })
}

export function useEndBookingEarly() {
  const api = useApi()
  const invalidate = useInvalidate()
  return useMutation({
    mutationFn: (id: string) => api<BookingDto>('POST', `/api/app/booking/${id}/end-early`).then(toBooking),
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
        'POST', '/api/app/maintenance-window/preview-affected-bookings', input),
    enabled: ok && !!input && input.occurrences.length > 0,
  })
}

export function useScheduleMaintenance() {
  const api = useApi()
  const invalidate = useInvalidate()
  return useMutation({
    mutationFn: (input: MaintenanceScopeInput & { note?: string }) =>
      api<{ seriesId: string | null; created: number; affectedBookingsCount: number }>(
        'POST', '/api/app/maintenance-window/schedule', input),
    onSuccess: () => invalidate([['maintenance'], ['maintenance-window']]),
  })
}

export function useCancelMaintenance() {
  const api = useApi()
  const invalidate = useInvalidate()
  return useMutation({
    mutationFn: (id: string) => api<MaintenanceDto>('POST', `/api/app/maintenance-window/${id}/cancel`).then(toMaintenance),
    onSettled: () => invalidate([['maintenance'], ['maintenance-window']]),
  })
}

export interface BuildingInput {
  name: string
  timeZone: string
  status: EstateStatus
  openHour: number
  closeHour: number
  minBookingMinutes: number
  maxBookingHours: number
  holidays: string[]
}

export interface FloorInput {
  buildingId: string
  name: string
  status: EstateStatus
  openHourOverride: number | null
  closeHourOverride: number | null
  minBookingMinutesOverride: number | null
  maxBookingHoursOverride: number | null
}

export interface SpaceInput {
  name: string
  type: SpaceType
  status: EstateStatus
  buildingId: string
  floorId: string
  timeZone: string
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
    id ? api<Building>('PUT', `/api/app/building/${id}`, body) : api<Building>('POST', '/api/app/building', body))

export const useSaveFloor = () =>
  useEstateMutation<{ id?: string; body: FloorInput }>((api, { id, body }) =>
    id ? api<Floor>('PUT', `/api/app/floor/${id}`, body) : api<Floor>('POST', '/api/app/floor', body))

export const useSaveSpace = () =>
  useEstateMutation<{ id?: string; body: SpaceInput }>((api, { id, body }) =>
    id ? api<Space>('PUT', `/api/app/space/${id}`, body) : api<Space>('POST', '/api/app/space', body))

export const useSetStatus = () =>
  useEstateMutation<{ kind: 'building' | 'floor' | 'space'; id: string; status: EstateStatus }>((api, { kind, id, status }) =>
    api('POST', `/api/app/${kind}/${id}/set-status`, { status }))
