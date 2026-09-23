import { useEffect } from 'react'
import { useNavigate } from 'react-router-dom'
import { useAuth } from 'react-oidc-context'
import { extractRoles } from '../auth/roles'

function AuthCallbackPage() {
  const auth = useAuth()
  const navigate = useNavigate()

  useEffect(() => {
    if (auth.isLoading || auth.error) return

    if (auth.isAuthenticated && auth.user) {
      const roles = extractRoles(auth.user.profile.role)
      navigate(roles.includes('admin') ? '/app/admin' : '/app/user', { replace: true })
    }
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
