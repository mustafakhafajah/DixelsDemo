import { describe, expect, it } from 'vitest'
import { hoursSpan, isUtcLike, isWithinHours, localLabel, localRange, offsetLabel, openParts, zoneCity, zonedInstant, type OpeningRules } from './closedDays'

/* Opening hours are on the building's own clock, as on the server (BuildingCalendar.IsWithinHours). */

const rules = (timeZone: string, openHour: number, closeHour: number): OpeningRules =>
  ({ timeZone, openMinute: openHour * 60, closeMinute: closeHour * 60, holidays: [], closedWeekdays: [] })

const utc = (iso: string) => new Date(`${iso}:00Z`)

describe('isWithinHours', () => {
  it('reads the hours in the building time zone', () => {
    /* Riyadh is UTC+3: 08:00-18:00 there is 05:00-15:00 UTC. */
    const riyadh = rules('Asia/Riyadh', 8, 18)
    expect(isWithinHours(riyadh, utc('2026-11-02T05:00'), utc('2026-11-02T06:00'))).toBe(true)
    expect(isWithinHours(riyadh, utc('2026-11-02T16:00'), utc('2026-11-02T17:00'))).toBe(false)
  })

  it('follows daylight saving', () => {
    /* London clocks go forward on 29 March 2026. */
    const london = rules('Europe/London', 8, 18)
    expect(isWithinHours(london, utc('2026-03-27T07:00'), utc('2026-03-27T08:00'))).toBe(false)
    expect(isWithinHours(london, utc('2026-03-30T07:00'), utc('2026-03-30T08:00'))).toBe(true)
  })

  it('may end at local midnight but not run past it', () => {
    const tokyo = rules('Asia/Tokyo', 0, 24)
    expect(isWithinHours(tokyo, utc('2026-11-02T14:00'), utc('2026-11-02T15:00'))).toBe(true)
    expect(isWithinHours(tokyo, utc('2026-11-02T14:00'), utc('2026-11-02T16:00'))).toBe(false)
  })
})

describe('openParts', () => {
  it('shifts the open hours onto the UTC day', () => {
    expect(openParts(rules('Asia/Riyadh', 8, 18), '2026-11-02')).toEqual([{ start: 300, end: 900 }])
    expect(hoursSpan(rules('UTC', 8, 20), '2026-11-02')).toEqual({ start: 480, end: 1200 })
  })
})

/* The booking form picks in UTC and shows the building's clock beside it (BookingModal). */
describe('both clocks', () => {
  it('shows a UTC time on the building clock, with the zone named by its city', () => {
    expect(localLabel(utc('2026-10-07T12:00'), 'Europe/Warsaw')).toBe('14:00')
    expect(zoneCity('Europe/Warsaw')).toBe('Warsaw')
    expect(zoneCity('America/New_York')).toBe('New York')
    expect(zoneCity('Mars/Base')).toBe('UTC')
  })

  it('adds nothing for a building whose clock reads as UTC', () => {
    expect(isUtcLike('UTC', utc('2026-10-07T12:00'))).toBe(true)
    expect(isUtcLike('Europe/London', utc('2026-12-01T12:00'))).toBe(true)
    expect(isUtcLike('Europe/Warsaw', utc('2026-10-07T12:00'))).toBe(false)
  })

  it('turns local opening times into UTC, across the clock change', () => {
    /* Warsaw is UTC+2 until 25 October 2026, then UTC+1. */
    expect(zonedInstant('2026-10-07', 8 * 60, 'Europe/Warsaw')).toEqual(utc('2026-10-07T06:00'))
    expect(zonedInstant('2026-10-07', 20 * 60, 'Europe/Warsaw')).toEqual(utc('2026-10-07T18:00'))
    expect(zonedInstant('2026-11-02', 8 * 60, 'Europe/Warsaw')).toEqual(utc('2026-11-02T07:00'))
  })

  it('offers only the UTC times inside a Warsaw building opening hours', () => {
    /* Open 08:00-20:00 in Warsaw on 7 October = 06:00-18:00 UTC: an end of 19:00 UTC is not offered. */
    expect(openParts(rules('Europe/Warsaw', 8, 20), '2026-10-07')).toEqual([{ start: 6 * 60, end: 18 * 60 }])
  })
})

/* The timeline and schedule show a building's own clock beside UTC (FindSpacePage, ScheduleCalendar). */
describe('building clock labels', () => {
  it('gives the offset from UTC, including half hours and the clock change', () => {
    expect(offsetLabel('Europe/Warsaw', utc('2026-10-07T12:00'))).toBe('UTC+2')
    expect(offsetLabel('Europe/Warsaw', utc('2026-11-02T12:00'))).toBe('UTC+1')
    expect(offsetLabel('Asia/Kolkata', utc('2026-10-07T12:00'))).toBe('UTC+5:30')
    expect(offsetLabel('America/New_York', utc('2026-12-01T02:00'))).toBe('UTC−5')
  })

  it('has a local window only for a building whose clock differs from UTC', () => {
    expect(localRange(utc('2026-10-07T06:00'), utc('2026-10-07T07:00'), 'UTC')).toBeNull()
    expect(localRange(utc('2026-10-07T06:00'), utc('2026-10-07T07:00'), 'Europe/Warsaw')).not.toBeNull()
  })
})
