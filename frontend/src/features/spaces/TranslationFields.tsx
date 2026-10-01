import { useTranslation } from 'react-i18next'
import i18n from 'i18next'
import { ErrorLine } from '../../components/bits'
import { Dropdown } from '../../components/pickers'
import { DEFAULT_LANGUAGE, LANGUAGES, languageOf } from '../../i18n/languages'
import type { FieldError, FieldErrors } from '../../lib/useFieldErrors'
import { nameId, NOTE_MAX, noteId, type TranslationDraft } from './translationDrafts'

/* Optional names in the portal's other languages, one box per language, added from a dropdown. Each box is
 * typed in its own language's direction, whatever the screen's language is. */
export function TranslationFields({ prefix, drafts, onChange, errors, clear, withNote, namePlaceholder, notePlaceholder }: {
  prefix: string
  drafts: TranslationDraft[]
  onChange: (drafts: TranslationDraft[]) => void
  errors: FieldErrors
  clear: (...ids: string[]) => void
  withNote?: boolean
  namePlaceholder?: string
  notePlaceholder?: string
}) {
  const { t } = useTranslation()
  const available = LANGUAGES.filter((l) => l.code !== DEFAULT_LANGUAGE && !drafts.some((d) => d.language === l.code))
  if (LANGUAGES.length < 2) return null

  const update = (language: string, patch: Partial<TranslationDraft>) =>
    onChange(drafts.map((d) => (d.language === language ? { ...d, ...patch } : d)))
  const remove = (language: string) => {
    onChange(drafts.filter((d) => d.language !== language))
    clear(nameId(prefix, language), noteId(prefix, language))
  }
  const add = (language: string) => {
    onChange([...drafts, { language, name: '', note: '' }])
    clear(`${prefix}-tr`)
    /* The new box is drawn on the next render; put the cursor in it. */
    setTimeout(() => document.getElementById(nameId(prefix, language))?.focus())
  }

  return (
    <div>
      <span className="lbl">{t('forms.translations.title')}</span>
      <p className="card-sub" style={{ marginTop: 0, marginBottom: 8 }}>{t('forms.translations.hint')}</p>
      {drafts.map((d) => {
        const lang = languageOf(d.language)
        const dir = i18n.dir(d.language)
        return (
          <div key={d.language} className="tr-box">
            <div className="tr-head">
              <span lang={d.language} className="tr-lang">{lang.name}</span>
              <button type="button" className="btn btn-sm" onClick={() => remove(d.language)}
                aria-label={t('forms.translations.removeLanguage', { language: lang.name })}>{t('forms.translations.remove')}</button>
            </div>
            <label className="lbl req" htmlFor={nameId(prefix, d.language)}>{t('forms.translations.name', { language: lang.name })}</label>
            <input id={nameId(prefix, d.language)} className="inp" lang={d.language} dir={dir} value={d.name} placeholder={namePlaceholder}
              onChange={(e) => { update(d.language, { name: e.target.value }); clear(nameId(prefix, d.language)) }} />
            <ErrorLine id={`${nameId(prefix, d.language)}-error`} error={errors[nameId(prefix, d.language)]} />
            {withNote && (
              <>
                <label className="lbl" htmlFor={noteId(prefix, d.language)} style={{ marginTop: 8 }}>{t('forms.translations.note', { language: lang.name })}</label>
                <input id={noteId(prefix, d.language)} className="inp" lang={d.language} dir={dir} value={d.note} maxLength={NOTE_MAX} placeholder={notePlaceholder}
                  onChange={(e) => { update(d.language, { note: e.target.value }); clear(noteId(prefix, d.language)) }} />
                <ErrorLine id={`${noteId(prefix, d.language)}-error`} error={errors[noteId(prefix, d.language)]} />
              </>
            )}
          </div>
        )
      })}
      {available.length > 0 && (
        <Dropdown id={`${prefix}-tr`} value="" onChange={add} placeholder={t('forms.translations.add')}
          aria-label={t('forms.translations.add')}
          options={available.map((l) => ({ value: l.code, label: l.name }))} />
      )}
      <ErrorLine id={`${prefix}-tr-error`} error={errors[`${prefix}-tr`] as FieldError | undefined} />
    </div>
  )
}
