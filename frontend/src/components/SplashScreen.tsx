import { useTranslation } from 'react-i18next'
import dixelsLogo from '../assets/dixels-logo.png'
import './SplashScreen.css'

/* Shown while the sign-in finishes: the beating Dixels icon and "Please wait…". */
export function SplashScreen({ detail }: { detail?: string }) {
  const { t } = useTranslation()
  return (
    <div className="splash" role="status" aria-live="polite">
      <div className="splash-icon">
        <img src={dixelsLogo} alt="Dixels" />
      </div>
      <div>
        <p className="splash-title">{t('splash.wait')}</p>
        {detail && <p className="splash-detail">{detail}</p>}
      </div>
    </div>
  )
}
