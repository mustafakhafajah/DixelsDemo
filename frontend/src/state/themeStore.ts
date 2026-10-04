import { create } from 'zustand'

/* Light or dark for the signed-in app. A first visit follows the device setting; once the person
 * picks one (top-bar button or account menu), that choice is stored and wins. index.html applies the same
 * rule before React loads, so a reload never flashes the other theme. */
export type Theme = 'light' | 'dark'

const STORAGE_KEY = 'dixels-theme'
const systemDark = typeof window !== 'undefined' && window.matchMedia ? window.matchMedia('(prefers-color-scheme: dark)') : null

/* localStorage can throw (private windows, blocked site data), so every access is guarded. */
function storedTheme(): Theme | null {
  try {
    const value = localStorage.getItem(STORAGE_KEY)
    return value === 'light' || value === 'dark' ? value : null
  } catch {
    return null
  }
}

function applyTheme(theme: Theme) {
  const root = document.documentElement
  root.dataset.theme = theme
  root.style.colorScheme = theme
}

interface ThemeState {
  theme: Theme
  toggle: () => void
  setTheme: (theme: Theme) => void
}

const saved = storedTheme()
/* Set once the person has picked a theme (even if it could not be stored). */
let chosen = saved !== null
const initial: Theme = saved ?? (systemDark?.matches ? 'dark' : 'light')
applyTheme(initial)

export const useThemeStore = create<ThemeState>((set, get) => ({
  theme: initial,
  toggle: () => get().setTheme(get().theme === 'dark' ? 'light' : 'dark'),
  /* From the top-bar button or the account menu; either way the choice is stored and wins. */
  setTheme: (theme) => {
    chosen = true
    try {
      localStorage.setItem(STORAGE_KEY, theme)
    } catch {
      /* Not stored: the choice still holds until the page is reloaded. */
    }
    applyTheme(theme)
    set({ theme })
  },
}))

/* Until the person has chosen, the app follows the device switching between light and dark. */
systemDark?.addEventListener('change', (e) => {
  if (chosen) return
  const theme: Theme = e.matches ? 'dark' : 'light'
  applyTheme(theme)
  useThemeStore.setState({ theme })
})
