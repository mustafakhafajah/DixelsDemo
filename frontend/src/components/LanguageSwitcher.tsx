import { useTranslation } from 'react-i18next'
import { setLanguage } from '../i18n'
import { LANGUAGES } from '../i18n/languages'
import './LanguageSwitcher.css'

/* Each language is listed by its own name, so anyone can find theirs whatever the page is in now. */
export function LanguageSwitcher({ className }: { className?: string }) {
  const { t, i18n } = useTranslation()
  return (
    <label className={`lang-switch${className ? ` ${className}` : ''}`}>
      <svg width="14" height="14" viewBox="0 0 16 16" fill="none" stroke="currentColor" strokeWidth="1.5" aria-hidden="true">
        <circle cx="8" cy="8" r="6.2" /><path d="M1.8 8h12.4M8 1.8c1.7 1.8 2.5 3.8 2.5 6.2S9.7 12.4 8 14.2C6.3 12.4 5.5 10.4 5.5 8S6.3 3.6 8 1.8z" />
      </svg>
      <select aria-label={t('language.label')} value={i18n.language} onChange={(e) => setLanguage(e.target.value)}>
        {LANGUAGES.map((l) => <option key={l.code} value={l.code} lang={l.code}>{l.name}</option>)}
      </select>
    </label>
  )
}
