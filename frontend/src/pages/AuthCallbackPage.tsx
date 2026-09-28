import { useEffect } from 'react'
import { useNavigate } from 'react-router-dom'
import { useAuth } from 'react-oidc-context'
import { SplashScreen } from '../components/SplashScreen'

/* Admins and booking users share one app shell; the role only gates what's inside it. */
function AuthCallbackPage() {
  const auth = useAuth()
  const navigate = useNavigate()

  useEffect(() => {
    if (auth.isLoading || auth.error || !auth.isAuthenticated || !auth.user) return
    /* /app sends each user on to the first page their permissions let them see. */
    navigate('/app', { replace: true })
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

  return <SplashScreen detail="Signing you in" />
}

export default AuthCallbackPage
