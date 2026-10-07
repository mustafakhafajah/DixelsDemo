import i18n from 'i18next'
import { parseUtc } from '../lib/dateUtils'

export type MaintenanceScopeType = 'Space' | 'Floor' | 'Building'
export type Lifecycle = 'scheduled' | 'in_progress' | 'ended' | 'cancelled'

/* A record's words in one language. English ("en") is always there; the others are optional extras.
 * note is only used by spaces. */
export interface Translation {
  language: string
  name: string
  note?: string | null
}

/* An admin-managed kind of space ("Meeting room", "Desk", ...). */
export interface SpaceType {
  id: string
  name: string
  /* name is in the reader's language, or English. translations has every language, English first, for the edit form. */
  translations: Translation[]
  spaceCount: number
}

export interface Building {
  id: string
  name: string
  translations: Translation[]
  timeZone: string
  /* Ticked = bookable. Unticking blocks every floor and space in it. */
  isBookable: boolean
  openHour: number
  closeHour: number
  minBookingMinutes: number
  maxBookingHours: number
  /* Closed dates and weekly closed days (0 = Sunday), both in the building's own time zone. */
  holidays: string[]
  closedWeekdays: number[]
  floorCount: number
  spaceCount: number
}

export interface Floor {
  id: string
  buildingId: string
  buildingName: string
  name: string
  translations: Translation[]
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
  closedWeekdays: number[]
  /* The building's time zone: holidays and closed weekdays are days there. */
  timeZone: string
}

export interface Space {
  id: string
  name: string
  translations: Translation[]
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
  /* Confirmed bookings on it that have not ended yet. */
  upcomingBookingCount: number
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
  creationTime: string
  lastModificationTime: string | null
  /* Portal users invited. */
  attendees: BookingAttendee[]
  /* Outside guests' addresses: only for the owner and admins, and only until the booking is over. */
  guests: string[]
  guestCount: number
}

export interface BookingAttendee {
  userId: string
  name: string
  email: string | null
}

/* Someone to invite (GET /api/app/booking-people). */
export interface BookingPerson {
  id: string
  name: string
  email: string | null
}

/* One person on the invite list as the form sends it: a picked user, or anyone by email. */
export type AttendeeInput = { userId: string } | { email: string }

export interface Booking extends Omit<BookingDto, 'startUtc' | 'endUtc' | 'lifecycle'> {
  kind: 'booking'
  start: Date
  end: Date
  /* Someone else's booking as an employee sees it: only when and where. It can't be opened. */
  busy?: true
}

/* What the server tells a non-admin about other people's bookings (GET /api/app/busy-windows). */
export interface BusyWindowDto {
  spaceId: string
  startUtc: string
  endUtc: string
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

/* The signed-in user as ABP's application-configuration reports it (ICurrentUser). */
export interface CurrentUser {
  isAuthenticated: boolean
  id: string | null
  userName: string | null
  name: string | null
  surName: string | null
  email: string | null
  /* Role names from the identity database, e.g. ["admin"]. */
  roles: string[]
}

/* The parts of one ABP user (GET /api/identity/users) the person filter shows. */
export interface UserLookup {
  id: string
  userName: string
  name: string | null
  surname: string | null
}

/* "Name Surname", falling back to the user name - the same rule the server uses for owner names. */
export const displayName = (name: string | null, surname: string | null, userName: string | null) =>
  [name, surname].filter(Boolean).join(' ') || userName || ''

export interface ListResult<T> {
  items: T[]
}

export interface PagedResult<T> extends ListResult<T> {
  totalCount: number
}

export interface Window {
  startUtc: string
  endUtc: string
}

/* A busy window shaped like a booking, so free-time and overlap checks treat it the same way. */
export const toBusy = (d: BusyWindowDto): Booking => ({
  kind: 'booking', busy: true, id: `busy:${d.spaceId}:${d.startUtc}`, spaceId: d.spaceId, spaceName: '',
  ownerUserId: '', ownerName: i18n.t('schedule.busy'), status: 'Confirmed', version: 0, seriesId: null,
  attendees: [], guests: [], guestCount: 0,
  creationTime: d.startUtc, lastModificationTime: null, start: parseUtc(d.startUtc), end: parseUtc(d.endUtc),
})

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
