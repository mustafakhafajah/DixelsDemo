import { useMemo, useState } from 'react'
import { useNavigate } from 'react-router-dom'
import { useAuth } from 'react-oidc-context'
import dixelsLogo from '../assets/dixels-logo.png'
import { DEMO_ACCOUNTS, type DemoAccount } from '../data/demoAccounts'
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

function LoginPage() {
  const navigate = useNavigate()
  const auth = useAuth()
  const promoGrid = useMemo(buildPromoGrid, [])

  const [email, setEmail] = useState('')
  const [password, setPassword] = useState('')
  const [error, setError] = useState(false)

  function attemptSignIn(candidateEmail: string, candidatePassword: string) {
    const normalizedEmail = candidateEmail.trim().toLowerCase()
    const account = DEMO_ACCOUNTS.find((a) => a.email === normalizedEmail)

    if (!account || !candidatePassword || candidatePassword !== account.password) {
      setError(true)
      return
    }

    navigate('/dashboard', {
      state: { name: account.name, role: account.role },
    })
  }

  function handleSignIn() {
    attemptSignIn(email, password)
  }

  function handlePickAccount(account: DemoAccount) {
    setEmail(account.email)
    setPassword(account.password)
    setError(false)
    attemptSignIn(account.email, account.password)
  }

  function handleRealSignIn() {
    auth.signinRedirect()
  }

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
          <p className="lead">Use your company account. Your role comes from it.</p>

          <div className="form-fields">
            <div>
              <label className="lbl" htmlFor="si-email">
                Work email
              </label>
              <input
                id="si-email"
                className="inp"
                type="email"
                autoComplete="username"
                value={email}
                onChange={(e) => {
                  setEmail(e.target.value)
                  setError(false)
                }}
              />
            </div>
            <div>
              <label className="lbl" htmlFor="si-pass">
                Password
              </label>
              <input
                id="si-pass"
                className="inp"
                type="password"
                autoComplete="current-password"
                value={password}
                onChange={(e) => {
                  setPassword(e.target.value)
                  setError(false)
                }}
                onKeyDown={(e) => {
                  if (e.key === 'Enter') handleSignIn()
                }}
              />
            </div>
            <p className={`err${error ? ' show' : ''}`}>
              <span className="errcode">401 auth.invalid_credentials</span>
              <span className="msg">That account is not in the demo directory. Pick one below.</span>
            </p>
            <button
              type="button"
              className="btn btn-primary btn-lg btn-block"
              onClick={handleSignIn}
            >
              Sign in
            </button>
          </div>

          <div className="divider">
            <span className="line" />
            <span className="label">or pick a demo account</span>
            <span className="line" />
          </div>

          <div className="demo-acct-list">
            {DEMO_ACCOUNTS.map((account) => (
              <button
                key={account.id}
                type="button"
                className="demo-acct"
                onClick={() => handlePickAccount(account)}
              >
                <span className={`avatar${account.role === 'administrator' ? ' admin' : ''}`}>
                  {account.initials}
                </span>
                <span className="demo-acct-info">
                  <span className="demo-acct-name">{account.name}</span>
                  <span className="demo-acct-role">
                    {account.role === 'administrator' ? 'Space administrator' : 'Booking user'}
                  </span>
                </span>
                <span className="demo-acct-use">Use</span>
              </button>
            ))}
          </div>

          <p className="disclaimer">
            This is a mock interface. No password is checked and nothing is sent anywhere.
            Choosing an account only changes which identity the screens behave as.
          </p>

          <div className="divider">
            <span className="line" />
            <span className="label">or use your real company account</span>
            <span className="line" />
          </div>

          <button
            type="button"
            className="btn btn-block real-signin-btn"
            onClick={handleRealSignIn}
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
            Sign in with company account
          </button>
        </div>
      </div>
    </div>
  )
}

export default LoginPage
