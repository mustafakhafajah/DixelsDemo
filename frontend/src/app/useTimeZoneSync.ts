import { useEffect } from 'react'
import { useAuth } from 'react-oidc-context'
import { useApi } from '../api/client'

const PATH = '/api/app/time-zone-preference'
const STORAGE_KEY = 'dixels-time-zone-synced'

/* The device's time zone is saved on the account, so the server can write emails in the person's own time. Once
 * signed in it is checked and saved if it changed, once per browser session and person. Errors are silent: the
 * next session simply tries again. Used once, in the signed-in app shell. */
export function useTimeZoneSync() {
  const api = useApi()
  const auth = useAuth()
  const signedIn = !!auth.user?.access_token
  const userId = auth.user?.profile.sub ?? ''

  useEffect(() => {
    if (!signedIn) return
    const timeZone = Intl.DateTimeFormat().resolvedOptions().timeZone
    if (!timeZone) return
    const key = `${STORAGE_KEY}:${userId}`
    /* sessionStorage can throw (private windows, blocked site data), so every access is guarded. */
    try {
      if (sessionStorage.getItem(key) === timeZone) return
    } catch { /* checked again below */ }
    let stale = false
    const remember = () => { try { sessionStorage.setItem(key, timeZone) } catch { /* asked again next time */ } }
    api<{ timeZone: string | null }>('GET', PATH)
      .then((r) => {
        if (stale) return
        if (r?.timeZone === timeZone) return remember()
        return api('PUT', PATH, { timeZone }).then(remember)
      })
      .catch(() => undefined)
    return () => { stale = true }
  }, [signedIn, userId, api])
}
