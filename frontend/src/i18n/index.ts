import i18n from 'i18next'
import { initReactI18next } from 'react-i18next'
import { queryClient } from '../api/queryClient'
import en from '../locales/en.json'
import { DEFAULT_LANGUAGE, isSupported } from './languages'

/* English ships in the main bundle (it is also every missing key's fallback); any other language is
 * fetched the first time it is needed. A new src/locales/<code>.json is picked up here on its own. */
const bundles = import.meta.glob<{ default: Record<string, unknown> }>(['../locales/*.json', '!../locales/en.json'])

const STORAGE_KEY = 'dixels-language'

/* localStorage can throw (private windows, blocked site data), so every access is guarded. */
function storedLanguage(): string | null {
  try {
    const value = localStorage.getItem(STORAGE_KEY)
    return isSupported(value) ? value : null
  } catch {
    return null
  }
}

/* The first of the browser's languages we offer, matched on the language part ("ar-SA" -> "ar"). */
function browserLanguage(): string | null {
  const wanted = typeof navigator !== 'undefined' ? navigator.languages ?? [navigator.language] : []
  return wanted.map((l) => l.split('-')[0].toLowerCase()).find(isSupported) ?? null
}

async function ensureLoaded(code: string) {
  if (i18n.hasResourceBundle(code, 'translation')) return
  const load = bundles[`../locales/${code}.json`]
  if (load) i18n.addResourceBundle(code, 'translation', (await load()).default)
}

/* lang for fonts and screen readers, dir for right-to-left languages, and the tab title. */
function applyToDocument(code: string) {
  const root = document.documentElement
  root.lang = code
  root.dir = i18n.dir(code)
  document.title = i18n.t('app.title')
}

const initial = storedLanguage() ?? browserLanguage() ?? DEFAULT_LANGUAGE

/* Resolves once the starting language is loaded, so the first screen is never shown in the wrong one. */
export const i18nReady = i18n
  .use(initReactI18next)
  .init({
    resources: { en: { translation: en } },
    lng: DEFAULT_LANGUAGE,
    fallbackLng: DEFAULT_LANGUAGE,
    interpolation: { escapeValue: false },
    react: { useSuspense: false },
  })
  .then(() => ensureLoaded(initial))
  .then(() => i18n.changeLanguage(initial))
  /* A language file that fails to load leaves the app in English rather than not starting at all. */
  .catch(() => undefined)
  .then(() => applyToDocument(i18n.language))

/* Switches the whole app: texts, direction, and every list refetched so names come back in the new language. */
export async function setLanguage(code: string) {
  if (!isSupported(code) || code === i18n.language) return
  await ensureLoaded(code)
  await i18n.changeLanguage(code)
  applyToDocument(code)
  try {
    localStorage.setItem(STORAGE_KEY, code)
  } catch {
    /* Not stored: the choice still holds until the page is reloaded. */
  }
  queryClient.invalidateQueries()
}

export default i18n
