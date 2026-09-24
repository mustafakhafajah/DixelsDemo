import { useMemo } from 'react'
import { useAuth } from 'react-oidc-context'
import dixelsLogo from '../assets/dixels-logo.png'
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
  const promoGrid = useMemo(buildPromoGrid, [])

  return (
    <div className="login-page">
      <div className="promo">
        <div className="promo-logo">
          <img src={dixelsLogo} alt="Dixels" />
        </div>
        <div className="promo-copy">
          <h1>Every room and every kit, booked once and only once.</h1>
          <p>
            Reserve shared spaces across the company. If the space is free and your window fits
            the rules, it is yours straight away. No approval, no waiting.
          </p>
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
        <div className="signin-card">
          <h2>Sign in</h2>
          <p className="lead">Use your email and password. Your role comes from your account.</p>

          <button
            type="button"
            className="btn btn-primary btn-lg btn-block real-signin-btn"
            onClick={() => auth.signinRedirect()}
          >
            <svg
              width="16"
              height="16"
              viewBox="0 0 16 16"
              fill="none"
              aria-hidden="true"
              className="real-signin-icon"
            >
              <path
                d="M6 2h5a1 1 0 0 1 1 1v10a1 1 0 0 1-1 1H6M9 8H2m0 0 3-3M2 8l3 3"
                stroke="currentColor"
                strokeWidth="1.4"
                strokeLinecap="round"
                strokeLinejoin="round"
              />
            </svg>
            Sign in with email
          </button>
        </div>
      </div>
    </div>
  )
}

export default LoginPage
