import { parseUtc } from '../lib/dateUtils'

export type MaintenanceScopeType = 'Space' | 'Floor' | 'Building'
export type Lifecycle = 'scheduled' | 'in_progress' | 'ended' | 'cancelled'

/* An admin-managed kind of space ("Meeting room", "Desk", ...). */
export interface SpaceType {
  id: string
  name: string
  spaceCount: number
}

export interface Building {
  id: string
  name: string
  timeZone: string
  /* Ticked = bookable. Unticking blocks every floor and space in it. */
  isBookable: boolean
  openHour: number
  closeHour: number
  minBookingMinutes: number
  maxBookingHours: number
  holidays: string[]
  floorCount: number
  spaceCount: number
}

export interface Floor {
  id: string
  buildingId: string
  buildingName: string
  name: string
  isBookable: boolean
  openHourOverride: number | null
  closeHourOverride: number | null
  minBookingMinutesOverride: number | null
  maxBookingHoursOverride: number | null
  spaceCount: number
}

export interface Constraints {
  openMinute: number
  closeMinute: number
  minBookingMinutes: number
  maxBookingHours: number
  holidays: string[]
}

export interface Space {
  id: string
  name: string
  typeId: string
  typeName: string
  /* Its own tick; it can still be blocked by its floor or building (see notBookableReason). */
  isBookable: boolean
  buildingId: string
  buildingName: string
  floorId: string
  floorName: string
  /* Always the building's time zone. */
  timeZone: string
  capacity: number
  note: string | null
  openHourOverride: number | null
  closeHourOverride: number | null
  minBookingMinutesOverride: number | null
  maxBookingHoursOverride: number | null
  constraints: Constraints
  canCurrentUserBook: boolean
  /* Why it cannot be booked right now, e.g. "HQ North is not bookable ..."; null when it can. */
  notBookableReason: string | null
}

export interface BookingDto {
  id: string
  spaceId: string
  spaceName: string
  ownerUserId: string
  ownerName: string
  startUtc: string
  endUtc: string
  status: 'Confirmed' | 'Cancelled'
  lifecycle: Lifecycle
  version: number
  seriesId: string | null
  parking: boolean
  creationTime: string
  lastModificationTime: string | null
}

export interface Booking extends Omit<BookingDto, 'startUtc' | 'endUtc' | 'lifecycle'> {
  kind: 'booking'
  start: Date
  end: Date
}

export interface MaintenanceDto {
  id: string
  spaceId: string
  spaceName: string
  startUtc: string
  endUtc: string
  note: string | null
  seriesId: string | null
  scopeType: MaintenanceScopeType
  scopeId: string
  scopeLabel: string
  status: 'Active' | 'Cancelled'
  lifecycle: Lifecycle
  creationTime: string
  creatorId: string | null
}

export interface Maintenance extends Omit<MaintenanceDto, 'startUtc' | 'endUtc' | 'lifecycle'> {
  kind: 'maintenance'
  start: Date
  end: Date
}

export type ScheduleItem = Booking | Maintenance

export interface Profile {
  id: string
  name: string
  email: string | null
  isAdmin: boolean
}

export interface UserLookup {
  id: string
  name: string
  isAdmin: boolean
}

export interface ListResult<T> {
  items: T[]
}

export interface PagedResult<T> extends ListResult<T> {
  totalCount: number
}

/* One page of the admin space registry (GET /api/app/space/paged-list). */
export interface SpaceRegistryPage extends PagedResult<Space> {
  upcomingBookingCounts: Record<string, number>
}

export interface Window {
  startUtc: string
  endUtc: string
}

export const toBooking = (d: BookingDto): Booking => {
  const { startUtc, endUtc, lifecycle: _lifecycle, ...rest } = d
  return { ...rest, kind: 'booking', start: parseUtc(startUtc), end: parseUtc(endUtc) }
}

export const toMaintenance = (d: MaintenanceDto): Maintenance => {
  const { startUtc, endUtc, lifecycle: _lifecycle, ...rest } = d
  return { ...rest, kind: 'maintenance', start: parseUtc(startUtc), end: parseUtc(endUtc) }
}

/* Lifecycle is derived live (the server value goes stale as time passes), as in the mock. */
export function lifecycleOf(item: ScheduleItem, now = Date.now()): Lifecycle {
  const cancelled = item.kind === 'booking' ? item.status === 'Cancelled' : item.status === 'Cancelled'
  if (cancelled) return 'cancelled'
  if (item.end.getTime() <= now) return 'ended'
  if (item.start.getTime() <= now) return 'in_progress'
  return 'scheduled'
}
