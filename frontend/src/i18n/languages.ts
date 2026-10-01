import i18n from 'i18next'
import type { DayPickerLocale } from 'react-day-picker'
import { ar, enGB } from 'react-day-picker/locale'

/* The languages the portal offers, listed by their own name. To add one: add it here, add
 * src/locales/<code>.json with the same keys as en.json, and add the code to the server's supported languages.
 * locale: the Intl locale for dates and numbers (English reads day-month-year, weeks from Monday).
 * calendar: the date picker's month and weekday names. */
export const LANGUAGES: { code: string; name: string; locale: string; calendar: DayPickerLocale }[] = [
  { code: 'en', name: 'English', locale: 'en-GB', calendar: enGB },
  { code: 'ar', name: 'العربية', locale: 'ar', calendar: ar },
]

export const DEFAULT_LANGUAGE = 'en'

export const isSupported = (code: string | null | undefined): code is string => LANGUAGES.some((l) => l.code === code)

export const languageOf = (code = i18n.language) => LANGUAGES.find((l) => l.code === code) ?? LANGUAGES[0]

/* The Intl locale of the chosen language, for Intl.DateTimeFormat and friends. */
export const intlLocale = (code = i18n.language) => languageOf(code).locale
