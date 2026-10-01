import i18n from 'i18next'
import type { Translation } from '../../api/types'
import { DEFAULT_LANGUAGE, LANGUAGES, languageOf, scriptFits } from '../../i18n/languages'
import type { FieldError, FieldErrors } from '../../lib/useFieldErrors'

/* A record's extra languages as the form edits them. English is not here: it has its own required field. */
export interface TranslationDraft { language: string; name: string; note: string }

export const NOTE_MAX = 500

export const englishOf = (translations: Translation[] | undefined) => translations?.find((t) => t.language === DEFAULT_LANGUAGE)

export const draftsOf = (translations: Translation[] | undefined): TranslationDraft[] =>
  (translations ?? []).filter((t) => t.language !== DEFAULT_LANGUAGE).map((t) => ({ language: t.language, name: t.name, note: t.note ?? '' }))

/* The full set of extra languages for the server: one that is left out is removed there. */
export const toTranslations = (drafts: TranslationDraft[], withNote = false): Translation[] =>
  drafts.map((d) => ({ language: d.language, name: d.name.trim(), ...(withNote ? { note: d.note.trim() || null } : {}) }))

export const nameId = (prefix: string, language: string) => `${prefix}-tr-${language}`
export const noteId = (prefix: string, language: string) => `${prefix}-tr-${language}-note`

/* Text in another alphabet than the language's (see scriptFits), or nothing wrong. Shown as the person types and
 * checked again on Save and by the server. */
export const scriptProblem = (language: string, text: string): FieldError | undefined =>
  scriptFits(language, text) ? undefined
    : { code: 'validation.wrong_script', message: i18n.t(`forms.translations.wrongScript.${languageOf(language).script}`) }

/* The same checks the server makes: every added language needs its name in its own letters, and a note fits. */
export function checkTranslations(prefix: string, drafts: TranslationDraft[], withNote = false): FieldErrors {
  const out: FieldErrors = {}
  for (const d of drafts) {
    if (!d.name.trim())
      out[nameId(prefix, d.language)] = { code: 'validation.missing_field', message: i18n.t('forms.translations.nameMissing', { language: languageOf(d.language).name }) }
    else out[nameId(prefix, d.language)] = scriptProblem(d.language, d.name)
    if (withNote && d.note.trim().length > NOTE_MAX)
      out[noteId(prefix, d.language)] = { code: 'validation.too_long', message: i18n.t('forms.space.noteTooLong') }
    else if (withNote) out[noteId(prefix, d.language)] = scriptProblem(d.language, d.note)
  }
  return out
}

/* The server names these fields "translations.ar" and "translations.ar.note"; a problem with the list as a whole shows under the name. */
export const translationServerFields = (prefix: string) => Object.fromEntries([
  ['translations', `${prefix}-name`],
  ...LANGUAGES.flatMap((l) => [[`translations.${l.code}`, nameId(prefix, l.code)], [`translations.${l.code}.note`, noteId(prefix, l.code)]]),
])
