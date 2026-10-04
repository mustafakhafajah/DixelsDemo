import { useEffect } from 'react'
import { Link, useNavigate } from 'react-router-dom'
import { useAuth } from 'react-oidc-context'
import { useTranslation } from 'react-i18next'
import { SplashScreen } from '../components/SplashScreen'

/* Admins and booking users share one app shell; the role only gates what's inside it. */
function AuthCallbackPage() {
  const auth = useAuth()
  const { t } = useTranslation()
  const navigate = useNavigate()

  useEffect(() => {
    if (auth.isLoading || auth.error || !auth.isAuthenticated || !auth.user) return
    /* /app sends each user on to the first page their permissions let them see. */
    navigate('/app', { replace: true })
  }, [auth.isLoading, auth.isAuthenticated, auth.error, auth.user, navigate])

  if (auth.error) {
    return (
      <div style={{ padding: 40, fontFamily: 'Inter, ui-sans-serif, system-ui, sans-serif' }}>
        <h1>{t('login.failed')}</h1>
        <p>{auth.error.message}</p>
        <Link to="/">{t('login.back')}</Link>
      </div>
    )
  }

  return <SplashScreen detail={t('login.signingIn')} />
}

export default AuthCallbackPage
