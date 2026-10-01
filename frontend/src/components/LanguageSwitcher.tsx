import { useTranslation } from 'react-i18next'
import { GlobeIcon } from 'lucide-react'
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
        <SelectTrigger aria-label={t('language.label')}
          className="tw:h-auto tw:data-[size=default]:h-auto tw:w-auto tw:gap-2 tw:rounded-lg tw:border-[#d9dbd6] tw:bg-white tw:px-3 tw:py-2 tw:text-[13px] tw:text-[#161b22] tw:shadow-none tw:[&_svg]:text-[#5c6670] tw:[&_svg]:opacity-100 tw:[&_svg:not([class*='size-'])]:size-3.5">
          <GlobeIcon />
          <SelectValue />
        </SelectTrigger>
        <SelectContent align="end">
          {LANGUAGES.map((l) => <SelectItem key={l.code} value={l.code} lang={l.code}>{l.name}</SelectItem>)}
        </SelectContent>
      </Select>
    </div>
  )
}
