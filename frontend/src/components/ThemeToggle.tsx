import { useTranslation } from 'react-i18next'
import { MoonIcon, SunIcon } from 'lucide-react'
import { useThemeStore } from '../state/themeStore'

/* One click flips light and dark: the top bar's button and the sign-in page's, each styled by its caller.
 * Moon in light, sun in dark: the icon shows what a click switches to. */
export function ThemeToggle({ className }: { className?: string }) {
  const { t } = useTranslation()
  const { theme, toggle } = useThemeStore()
  const dark = theme === 'dark'
  const label = dark ? t('nav.themeLight') : t('nav.themeDark')
  return (
    <button type="button" aria-pressed={dark} aria-label={label} title={label} onClick={toggle} className={className}>
      {dark ? <SunIcon /> : <MoonIcon />}
    </button>
  )
}
