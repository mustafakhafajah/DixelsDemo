import { useTranslation } from 'react-i18next'
import { LanguagesIcon } from 'lucide-react'
import { Select, SelectContent, SelectItem, SelectTrigger, SelectValue } from '@/components/ui/select'
import { setLanguage } from '../i18n'
import { LANGUAGES } from '../i18n/languages'

/* The sign-in page's language picker (signed in, the account menu has it): a shadcn Select. Each language is
 * listed by its own name, so anyone can find theirs whatever the page is in now. */
export function LanguageSwitcher({ className }: { className?: string }) {
  const { t, i18n } = useTranslation()
  return (
    <div className={className}>
      <Select value={i18n.language} onValueChange={(code) => { void setLanguage(code) }}>
        <SelectTrigger size="sm" aria-label={t('language.label')} className="tw:w-auto tw:gap-2">
          <LanguagesIcon className="tw:opacity-70" />
          <SelectValue />
        </SelectTrigger>
        <SelectContent align="end">
          {LANGUAGES.map((l) => <SelectItem key={l.code} value={l.code} lang={l.code}>{l.name}</SelectItem>)}
        </SelectContent>
      </Select>
    </div>
  )
}
