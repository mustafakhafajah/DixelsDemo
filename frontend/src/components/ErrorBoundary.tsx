import { Component, type ErrorInfo, type ReactNode } from 'react'
import { useTranslation } from 'react-i18next'
import dixelsLogo from '../assets/dixels-logo.png'
import './SplashScreen.css'

/* What is shown instead of a blank page when something breaks while drawing the app. */
function CrashScreen() {
  const { t } = useTranslation()
  return (
    <div className="splash" role="alert">
      <div className="splash-icon still">
        <img src={dixelsLogo} alt="Dixels" />
      </div>
      <div>
        <p className="splash-title">{t('crash.title')}</p>
        <p className="splash-detail">{t('crash.detail')}</p>
      </div>
      <button type="button" className="splash-btn" onClick={() => window.location.reload()}>{t('crash.reload')}</button>
    </div>
  )
}

/* Catches an error thrown while drawing any page (including a page that failed to download after an update)
 * and offers a reload, instead of leaving the screen empty. */
export class ErrorBoundary extends Component<{ children: ReactNode }, { failed: boolean }> {
  state = { failed: false }

  static getDerivedStateFromError() {
    return { failed: true }
  }

  componentDidCatch(error: Error, info: ErrorInfo) {
    console.error(error, info.componentStack)
  }

  render() {
    return this.state.failed ? <CrashScreen /> : this.props.children
  }
}
