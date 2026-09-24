import type { ReactNode } from 'react'
import { Navigate } from 'react-router-dom'
import { useAuth } from 'react-oidc-context'
import { useSession } from './session'

export function RequireAuth({ children }: { children: ReactNode }) {
  const auth = useAuth()
  if (auth.isLoading) return <p style={{ padding: 40, fontFamily: 'Inter, sans-serif' }}>Loading…</p>
  if (!auth.isAuthenticated) return <Navigate to="/" replace />
  return <>{children}</>
}

/* Mirrors the mock's go() guard: admin-only views fall back to Find a space. */
export function RequireAdmin({ children }: { children: ReactNode }) {
  const { isAdmin } = useSession()
  if (!isAdmin) return <Navigate to="/app/find" replace />
  return <>{children}</>
}
