import type { ReactNode } from 'react'
import { Navigate, useLocation } from 'react-router-dom'
import { useAuth } from 'react-oidc-context'
import { SplashScreen } from '../components/SplashScreen'
import { useSession } from './session'

export function RequireAuth({ children }: { children: ReactNode }) {
  const auth = useAuth()
  if (auth.isLoading) return <SplashScreen />
  if (!auth.isAuthenticated) return <Navigate to="/" replace />
  return <>{children}</>
}

/* Mirrors the mock's go() guard: admin-only views fall back to the schedule. */
export function RequireAdmin({ children }: { children: ReactNode }) {
  const { isAdmin } = useSession()
  if (!isAdmin) return <Navigate to="/app/bookings" replace />
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
