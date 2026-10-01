import { useMemo } from 'react'
import { useTranslation } from 'react-i18next'
import { useAuth } from 'react-oidc-context'
import dixelsLogo from '../assets/dixels-logo.png'
import { LanguageSwitcher } from '../components/LanguageSwitcher'
import './LoginPage.css'

interface PromoSegment {
  on: boolean
  alt: boolean
  flex: number
}

function buildPromoGrid(): PromoSegment[][] {
  const lanes: PromoSegment[][] = []
  for (let l = 0; l < 5; l++) {
    const lane: PromoSegment[] = []
    for (let s = 0; s < 26; s++) {
      const on = (l * 7 + s * 3) % 11 < 3
      const alt = (s + l) % 5 === 0
      lane.push({ on, alt: on && alt, flex: on ? 1 + ((s + l) % 3) : 1 })
    }
    lanes.push(lane)
  }
  return lanes
}

/* One way in: the real ABP sign-in page (email + password), which returns a token whose role
 * decides the admin or user interface. */
function LoginPage() {
  const auth = useAuth()
  const { t } = useTranslation()
  const promoGrid = useMemo(buildPromoGrid, [])
  return (
    <div className="login-page">
      <div className="promo">
        <div className="promo-logo">
          <img src={dixelsLogo} alt="Dixels" />
        </div>
        <div className="promo-copy">
          <h1>{t('login.headline')}</h1>
          <p>{t('login.pitch')}</p>
        </div>
        <div className="promo-grid" aria-hidden="true">
          {promoGrid.map((lane, laneIndex) => (
            <div className="lane" key={laneIndex}>
              {lane.map((seg, segIndex) => (
                <div
                  key={segIndex}
                  className={`pseg${seg.on ? ' on' : ''}${seg.alt ? ' alt' : ''}`}
                  style={{ flex: seg.flex }}
                />
              ))}
            </div>
          ))}
        </div>
      </div>

      <div className="signin-panel">
        <LanguageSwitcher className="login-lang" />
        <div className="signin-card">
          <h2>{t('login.title')}</h2>
          <p className="lead">{t('login.lead')}</p>

          <button
            type="button"
            className="btn btn-primary btn-lg btn-block real-signin-btn"
            onClick={() => auth.signinRedirect()}
          >
            {t('login.button')}
            <svg
              width="16"
              height="16"
              viewBox="0 0 16 16"
              fill="none"
              aria-hidden="true"
              className="real-signin-icon flip-rtl"
            >
              <path
                d="M10 2h2a1 1 0 0 1 1 1v10a1 1 0 0 1-1 1h-2M2 8h7m0 0L6 5m3 3-3 3"
                stroke="currentColor"
                strokeWidth="1.4"
                strokeLinecap="round"
                strokeLinejoin="round"
              />
            </svg>
          </button>
        </div>
      </div>
    </div>
  )
}

export default LoginPage
