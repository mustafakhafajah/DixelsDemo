import i18n from 'i18next'
import type { DayPickerLocale } from 'react-day-picker'
import { ar, enGB } from 'react-day-picker/locale'

/* The alphabet a language is written in, for the "letters of this language only" rule (scriptFits). */
export type Script = 'latin' | 'arabic'

/* The languages the portal offers, listed by their own name. To add one: add it here, add
 * src/locales/<code>.json with the same keys as en.json, and add the code to the server's supported languages.
 * locale: the Intl locale for dates and numbers (English reads day-month-year, weeks from Monday).
 * calendar: the date picker's month and weekday names.
 * script: which letters text in that language may use (the server's PortalLanguages says the same). */
export const LANGUAGES: { code: string; name: string; locale: string; calendar: DayPickerLocale; script: Script }[] = [
  { code: 'en', name: 'English', locale: 'en-GB', calendar: enGB, script: 'latin' },
  { code: 'ar', name: 'العربية', locale: 'ar', calendar: ar, script: 'arabic' },
]

export const DEFAULT_LANGUAGE = 'en'

export const isSupported = (code: string | null | undefined): code is string => LANGUAGES.some((l) => l.code === code)

export const languageOf = (code = i18n.language) => LANGUAGES.find((l) => l.code === code) ?? LANGUAGES[0]

/* The Intl locale of the chosen language, for Intl.DateTimeFormat and friends. */
export const intlLocale = (code = i18n.language) => languageOf(code).locale

/* The same Unicode ranges as the server's ScriptRules, so the form and the server agree exactly. */
const LATIN = /[A-Za-zÀ-ÖØ-öø-ɏḀ-ỿ]/u
const ARABIC = /[؀-ۿݐ-ݿࡰ-ࣿﭐ-﷿ﹰ-﻿]/u
const LETTER = /\p{L}/gu

/* Only letters count: digits, spaces, punctuation and Arabic diacritics are fine anywhere, and text with no
 * letters ("2") fits every language. Latin: every letter is Latin. Arabic: every letter is Arabic or Latin
 * (codes such as "VIP" may stay), and at least one is Arabic. */
export function scriptFits(code: string, text: string): boolean {
  const script = languageOf(code).script
  const letters = text.match(LETTER) ?? []
  if (script === 'latin') return letters.every((c) => LATIN.test(c))
  return letters.length === 0 || (letters.every((c) => ARABIC.test(c) || LATIN.test(c)) && letters.some((c) => ARABIC.test(c)))
}
