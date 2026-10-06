import { describe, expect, it } from 'vitest'
import { hoursSpan, isWithinHours, openParts, type OpeningRules } from './closedDays'

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
