import dixelsLogo from '../assets/dixels-logo.png'
import './SplashScreen.css'

/* Shown while the sign-in finishes: the beating Dixels icon and "Please wait…". */
export function SplashScreen({ detail }: { detail?: string }) {
  return (
    <div className="splash" role="status" aria-live="polite">
      <div className="splash-icon">
        <img src={dixelsLogo} alt="Dixels" />
      </div>
      <div>
        <p className="splash-title">Please wait…</p>
        {detail && <p className="splash-detail">{detail}</p>}
      </div>
    </div>
  )
}
