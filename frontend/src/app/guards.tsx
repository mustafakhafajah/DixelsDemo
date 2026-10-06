import { useEffect, useState, type ReactNode } from 'react'
import { Navigate, useLocation } from 'react-router-dom'
import { useAuth } from 'react-oidc-context'
import { renewSession, signInAgain } from '../auth/renewal'
import { SplashScreen } from '../components/SplashScreen'
import { useSession } from './session'

/* Signed out: to sign-in, remembering the page asked for (with its filters), so a shared link opens after signing in. */
export function RequireAuth({ children }: { children: ReactNode }) {
  const auth = useAuth()
  const { pathname, search } = useLocation()
  /* Signed in before, but the access token ran out meanwhile (e.g. the computer slept): one quiet renewal first. */
  const [renewFailed, setRenewFailed] = useState(false)
  const renewing = !auth.isLoading && !auth.isAuthenticated && !!auth.user?.refresh_token && !renewFailed
  useEffect(() => {
    if (!renewing) return
    let live = true
    void renewSession().then((token) => { if (!token && live) setRenewFailed(true) })
    return () => { live = false }
  }, [renewing])
  /* The token ran out while the app was open and the background renewal did not manage: try once more, then
   * off to sign in, back to this page afterwards. Without this the pages would stay up with a dead session. */
  useEffect(() => auth.events.addAccessTokenExpired(() => {
    void renewSession().then((token) => { if (!token) signInAgain() })
  }), [auth.events])
  if (auth.isLoading || renewing) return <SplashScreen />
  if (!auth.isAuthenticated) return <Navigate to="/" replace state={{ returnTo: pathname + search }} />
  return <>{children}</>
}

/* A page for holders of any of these permissions; everyone else lands on the first page they can see.
 * Waits for the real grants so a stale default never flashes a page (or a redirect) at the user. */
export function RequirePermission({ any, children }: { any: readonly string[]; children: ReactNode }) {
  const { can, permissionsLoaded, home } = useSession()
  const { pathname } = useLocation()
  if (!permissionsLoaded) return null
  /* Someone who can see no page at all stays put on an empty shell rather than bouncing between redirects. */
  if (!any.some(can)) return pathname === home ? null : <Navigate to={home} replace />
  return <>{children}</>
}

/* The landing page: the first page this user can see. */
export function HomeRedirect() {
  const { permissionsLoaded, home } = useSession()
  if (!permissionsLoaded) return null
  return <Navigate to={home} replace />
}
