import { useEffect, useRef, useState } from 'react'
import { useTranslation } from 'react-i18next'
import i18n from 'i18next'
import { ErrorLine, RequiredMark } from '../../components/bits'
import { Dropdown } from '../../components/pickers'
import { DEFAULT_LANGUAGE, LANGUAGES, languageOf } from '../../i18n/languages'
import type { FieldError, FieldErrors } from '../../lib/useFieldErrors'
import { nameId, noteId, scriptProblem, type TranslationDraft } from './translationDrafts'

/* One text box for a name (or description) in every portal language; the dropdown beside it picks which
 * language the box shows. English is the default and the one that is required. Another language starts
 * empty, and what was typed in each language is still there after switching back. Nothing is sent until
 * the form is saved, which sends every language in the same request.
 *
 * A problem is never hidden behind the dropdown: every language's message for this field shows under the box
 * (another language's with its name and a "Show" button), the dropdown marks the languages that need a fix,
 * and every failed Save (attempt) brings the first language with a problem into the box. */
export function LocalizedInput({ prefix, field, id, label, required, english, onEnglish, drafts, onDrafts, errors, attempt, clear,
  placeholder, className = 'inp', maxLength, autoFocus, onEnter }: {
  prefix: string
  /* Which text of the translations this box edits. */
  field: 'name' | 'note'
  /* The English box's id, e.g. "bld-name"; the other languages use nameId / noteId. */
  id: string
  label: string
  required?: boolean
  english: string
  onEnglish: (value: string) => void
  drafts: TranslationDraft[]
  onDrafts: (drafts: TranslationDraft[]) => void
  errors: FieldErrors
  /* useFieldErrors().attempt: goes up on every Save that found a problem. */
  attempt: number
  clear: (...ids: string[]) => void
  placeholder?: string
  className?: string
  maxLength?: number
  autoFocus?: boolean
  onEnter?: () => void
}) {
  const { t } = useTranslation()
  const [language, setLanguage] = useState(DEFAULT_LANGUAGE)
  const isEnglish = language === DEFAULT_LANGUAGE
  const idOf = (code: string) => (code === DEFAULT_LANGUAGE ? id : field === 'name' ? nameId(prefix, code) : noteId(prefix, code))
  const textOf = (code: string) => (code === DEFAULT_LANGUAGE ? english : drafts.find((d) => d.language === code)?.[field] ?? '')
  const boxId = idOf(language)

  /* A language's message: what Save or the server found, else the letters rule, checked as the person types. */
  const errorOf = (code: string): FieldError | undefined => errors[idOf(code)] ?? scriptProblem(code, textOf(code))
  const withError = LANGUAGES.map((l) => l.code).filter((c) => errorOf(c))

  const focus = (code: string) => setTimeout(() => document.getElementById(idOf(code))?.focus())

  /* After each failed Save: stay on this language if it has a problem, else go to English, else the first one.
   * Switched while rendering (React's way to follow a changed prop), so the right language is on screen at once. */
  const [seenAttempt, setSeenAttempt] = useState(attempt)
  if (attempt !== seenAttempt) {
    setSeenAttempt(attempt)
    const failing = LANGUAGES.map((l) => l.code).filter((c) => errors[idOf(c)])
    if (failing.length) setLanguage(failing.includes(language) ? language : failing.includes(DEFAULT_LANGUAGE) ? DEFAULT_LANGUAGE : failing[0])
  }
  /* ...and the cursor goes into the box with the problem, once per Save, unless an earlier field took it. */
  const focusedAttempt = useRef(attempt)
  useEffect(() => {
    if (attempt === focusedAttempt.current) return
    focusedAttempt.current = attempt
    if (!errors[boxId] || document.activeElement?.getAttribute('aria-invalid') === 'true') return
    document.getElementById(boxId)?.focus()
  }, [attempt, boxId, errors])

  /* After a language is picked the box shows it, and the cursor goes there rather than back to the dropdown. */
  const picked = useRef<string | null>(null)
  const pick = (code: string) => {
    picked.current = code
    setLanguage(code)
  }
  const focusBox = (e: Event) => {
    const code = picked.current
    if (code === null) return
    picked.current = null
    e.preventDefault()
    focus(code)
  }
  const show = (code: string) => { setLanguage(code); focus(code) }

  const change = (value: string) => {
    clear(boxId)
    if (isEnglish) return onEnglish(value)
    const old = drafts.find((d) => d.language === language)
    const next = { ...(old ?? { language, name: '', note: '' }), [field]: value }
    /* A language with nothing typed in it is not a translation, so it is not sent, and its messages go too. */
    if (!next.name && !next.note) {
      clear(nameId(prefix, language), noteId(prefix, language))
      return onDrafts(drafts.filter((d) => d.language !== language))
    }
    onDrafts(old ? drafts.map((d) => (d.language === language ? next : d)) : [...drafts, next])
  }

  const options = LANGUAGES.map((l) => ({
    value: l.code,
    label: errorOf(l.code) ? t('forms.translations.needsFix', { language: l.name })
      : textOf(l.code).trim() ? t('forms.translations.filled', { language: l.name }) : l.name,
  }))
  const error = errorOf(language)
  const others = withError.filter((c) => c !== language)

  return (
    <div>
      <label className={`lbl${required && isEnglish ? ' req' : ''}`} htmlFor={boxId}>{label}{required && isEnglish && <RequiredMark />}</label>
      <div className="loc-row">
        <input id={boxId} key={language} className={className} lang={language} dir={i18n.dir(language)}
          value={textOf(language)} placeholder={isEnglish ? placeholder : undefined} maxLength={maxLength} autoFocus={autoFocus && isEnglish}
          aria-invalid={error ? true : undefined} aria-describedby={error ? `${boxId}-error` : undefined}
          onChange={(e) => change(e.target.value)}
          onKeyDown={onEnter && ((e) => { if (e.key === 'Enter') onEnter() })} />
        {LANGUAGES.length > 1 && (
          <Dropdown id={`${id}-lang`} value={language} onChange={pick} options={options} onCloseAutoFocus={focusBox} invalid={others.length > 0}
            aria-label={`${label}: ${t('forms.translations.language')}`} style={{ width: 150, flex: 'none' }} />
        )}
      </div>
      <ErrorLine id={`${boxId}-error`} error={error} />
      {/* Problems in the languages that are not in the box right now. */}
      {others.map((code) => (
        <p key={code} className="err" role="alert" data-code={errorOf(code)!.code}>
          <span className="msg"><b lang={code}>{languageOf(code).name}:</b> {errorOf(code)!.message}</span>
          <button type="button" className="link-btn" onClick={() => show(code)}>{t('forms.translations.show')}</button>
        </p>
      ))}
      {!isEnglish && !error && !others.length && (
        <p className="card-sub" style={{ marginTop: 5 }}>{t('forms.translations.optional', { language: languageOf(language).name })}</p>
      )}
    </div>
  )
}
