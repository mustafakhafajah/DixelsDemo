import { useEffect, useRef } from 'react'
import { useNavigate } from 'react-router-dom'
import { useAuth } from 'react-oidc-context'
import { apiRequest } from '../api/client'

/* Admins and booking users share one app shell; the role only gates what's inside it. */
function AuthCallbackPage() {
  const auth = useAuth()
  const navigate = useNavigate()
  const recorded = useRef(false)

  useEffect(() => {
    if (auth.isLoading || auth.error || !auth.isAuthenticated || !auth.user) return
    if (!recorded.current) {
      recorded.current = true
      apiRequest(auth.user.access_token, 'POST', '/api/app/activity-log/record-sign-in').catch(() => undefined)
    }
    navigate('/app/dashboard', { replace: true })
  }, [auth.isLoading, auth.isAuthenticated, auth.error, auth.user, navigate])

  if (auth.error) {
    return (
      <div style={{ padding: 40, fontFamily: 'Inter, ui-sans-serif, system-ui, sans-serif' }}>
        <h1>Sign-in failed</h1>
        <p>{auth.error.message}</p>
        <a href="/">Back to sign in</a>
      </div>
    )
  }

  return (
    <div style={{ padding: 40, fontFamily: 'Inter, ui-sans-serif, system-ui, sans-serif' }}>
      <p>Signing you in…</p>
    </div>
  )
}

export default AuthCallbackPage
