import { afterEach, describe, expect, it, vi } from 'vitest'
import { localLabel, openParts, viewerZone, type OpeningRules } from './closedDays'
import { addDays, dayAt, dayKey, hm, minOfDay, monthBounds, stamp, weekStart } from './dateUtils'
import { generateOccurrences } from './recurrence'

/* Tests run as a viewer in Amman (UTC+3 all year, see vitest.setup.ts): every time is read on that clock. */

const utc = (iso: string) => new Date(`${iso}:00Z`)

describe("the viewer's own clock", () => {
  it('runs the tests in Amman', () => {
    expect(viewerZone()).toBe('Asia/Amman')
    expect(utc('2026-10-07T12:00').getTimezoneOffset()).toBe(-180)
  })

  it('reads days and times on that clock', () => {
    /* 22:30 UTC is already 01:30 the next day in Amman. */
    const late = utc('2026-10-07T22:30')
    expect(dayKey(late)).toBe('2026-10-08')
    expect(hm(late)).toBe('01:30')
    expect(minOfDay(late)).toBe(90)
    expect(stamp(late)).toBe('2026-10-08 01:30')
  })

  it('turns a day and a time back into the instant, rolling minutes over', () => {
    expect(dayAt('2026-10-08', 1, 30)).toEqual(utc('2026-10-07T22:30'))
    expect(dayAt('2026-10-07')).toEqual(utc('2026-10-06T21:00'))
    expect(dayAt('2026-10-07', 0, 1440)).toEqual(dayAt('2026-10-08'))
    expect(dayAt('2026-10-07', 0, 13 * 60 + 15)).toEqual(utc('2026-10-07T10:15'))
  })

  it('steps whole days, weeks and months on the local calendar', () => {
    expect(dayKey(addDays(dayAt('2026-10-31', 9), 1))).toBe('2026-11-01')
    expect(hm(addDays(dayAt('2026-10-31', 9), 1))).toBe('09:00')
    expect(weekStart('2026-10-07')).toBe('2026-10-05')
    expect(monthBounds('2026-02-14')).toEqual({ from: '2026-02-01', to: '2026-02-28' })
  })
})

/* A Warsaw building (UTC+2 until 25 October 2026, then UTC+1) seen from Amman (UTC+3 throughout). */
describe('a building whose clock changes while the viewer does not', () => {
  const warsaw: OpeningRules = { timeZone: 'Europe/Warsaw', openMinute: 8 * 60, closeMinute: 20 * 60, holidays: [], closedWeekdays: [] }

  it("moves the building's hours on the viewer's day by an hour", () => {
    /* 08:00-20:00 Warsaw is 09:00-21:00 in Amman before the change and 10:00-22:00 after it. */
    expect(openParts(warsaw, '2026-10-23')).toEqual([{ start: 9 * 60, end: 21 * 60 }])
    expect(openParts(warsaw, '2026-10-25')).toEqual([{ start: 10 * 60, end: 22 * 60 }])
    expect(openParts(warsaw, '2026-10-26')).toEqual([{ start: 10 * 60, end: 22 * 60 }])
  })

  it("shows the building's clock for a time picked on the viewer's", () => {
    expect(localLabel(dayAt('2026-10-23', 9), 'Europe/Warsaw')).toBe('08:00')
    expect(localLabel(dayAt('2026-10-26', 9), 'Europe/Warsaw')).toBe('07:00')
  })
})

describe('repeating bookings', () => {
  afterEach(() => { vi.unstubAllEnvs() })

  it("keeps the organiser's wall-clock time across their own clock change, like Teams", () => {
    /* A viewer in Warsaw books Mondays 09:00-10:00 from 19 October: 09:00 stays 09:00 after the clocks go back,
     * so the instant moves from 07:00 to 08:00 UTC. */
    vi.stubEnv('TZ', 'Europe/Warsaw')
    const start = dayAt('2026-10-19', 9)
    const { occurrences } = generateOccurrences(start, dayAt('2026-10-19', 10), {
      freq: 'weekly', interval: 1, byDay: [], end: { mode: 'count', count: 3 },
    })
    expect(occurrences.map((o) => o.start)).toEqual([utc('2026-10-19T07:00'), utc('2026-10-26T08:00'), utc('2026-11-02T08:00')])
    expect(occurrences.map((o) => hm(o.start))).toEqual(['09:00', '09:00', '09:00'])
    expect(occurrences.every((o) => o.end.getTime() - o.start.getTime() === 3_600_000)).toBe(true)
  })

  it('repeats daily and monthly on local days, keeping the time', () => {
    const daily = generateOccurrences(dayAt('2026-10-30', 23, 30), dayAt('2026-10-31', 0, 30), {
      freq: 'daily', interval: 1, byDay: [], end: { mode: 'count', count: 3 },
    })
    expect(daily.occurrences.map((o) => `${dayKey(o.start)} ${hm(o.start)}`)).toEqual(['2026-10-30 23:30', '2026-10-31 23:30', '2026-11-01 23:30'])
    const monthly = generateOccurrences(dayAt('2026-01-31', 1), dayAt('2026-01-31', 2), {
      freq: 'monthly', interval: 1, byDay: [], end: { mode: 'count', count: 3 },
    })
    expect(monthly.occurrences.map((o) => `${dayKey(o.start)} ${hm(o.start)}`)).toEqual(['2026-01-31 01:00', '2026-02-28 01:00', '2026-03-31 01:00'])
  })

  it('takes weekdays from the local day', () => {
    /* 00:30 on Monday 5 October in Amman is still Sunday in UTC. */
    const { occurrences } = generateOccurrences(dayAt('2026-10-05', 0, 30), dayAt('2026-10-05', 1, 30), {
      freq: 'weekly', interval: 1, byDay: [], end: { mode: 'count', count: 2 },
    })
    expect(occurrences.map((o) => dayKey(o.start))).toEqual(['2026-10-05', '2026-10-12'])
  })
})
