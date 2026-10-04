import 'i18next'
import type en from '../locales/en.json'

/* t('...') only accepts keys that exist in en.json, so a typo fails the build instead of showing a raw key. */
declare module 'i18next' {
  interface CustomTypeOptions {
    resources: { translation: typeof en }
  }
}
