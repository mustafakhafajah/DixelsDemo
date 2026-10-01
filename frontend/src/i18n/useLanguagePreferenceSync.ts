import { useEffect, useRef } from 'react'
import { useAuth } from 'react-oidc-context'
import { useTranslation } from 'react-i18next'
import { useApi } from '../api/client'
import { setLanguage } from '.'
import { isSupported } from './languages'

const PATH = '/api/app/language-preference'

/* The language is saved on the account, so it follows the person to any device. Once signed in, the saved
 * choice wins; an account that never chose one keeps (and saves) the current language. After that, every
 * switch is saved too. Used once, in the signed-in app shell. */
export function useLanguagePreferenceSync() {
  const api = useApi()
  const { i18n } = useTranslation()
  const signedIn = !!useAuth().user?.access_token
  /* The language the account has on the server, once known; null until then. */
  const saved = useRef<string | null>(null)

  useEffect(() => {
    if (!signedIn || saved.current) return
    let stale = false
    const asked = i18n.language
    const save = (language: string) => {
      saved.current = language
      api('PUT', PATH, { language }).catch(() => { saved.current = null })
    }
    api<{ language: string | null }>('GET', PATH).then((r) => {
      if (stale) return
      /* Switched while the answer was on its way: the fresh choice wins. */
      if (i18n.language !== asked || !isSupported(r.language)) return save(i18n.language)
      saved.current = r.language
      setLanguage(r.language)
    }, () => undefined)
    return () => { stale = true }
  }, [signedIn, api, i18n])

  useEffect(() => {
    if (!signedIn) return
    const onChange = (language: string) => {
      if (!saved.current || language === saved.current || !isSupported(language)) return
      saved.current = language
      api('PUT', PATH, { language }).catch(() => undefined)
    }
    i18n.on('languageChanged', onChange)
    return () => i18n.off('languageChanged', onChange)
  }, [signedIn, api, i18n])
}
