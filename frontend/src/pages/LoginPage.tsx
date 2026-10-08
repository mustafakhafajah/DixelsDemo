import { useEffect, useLayoutEffect, useRef, useState, type ReactNode, type RefObject } from 'react'
import { useTranslation } from 'react-i18next'
import { useAuth } from 'react-oidc-context'
import { useLocation } from 'react-router-dom'
import { ArrowRightIcon, LockIcon, MailCheckIcon, MailIcon, MousePointer2Icon, OrbitIcon, ShieldCheckIcon } from 'lucide-react'
import dixelsLogo from '../assets/dixels-logo.png'
import { LanguageSwitcher } from '../components/LanguageSwitcher'
import { ThemeToggle } from '../components/ThemeToggle'
import { intlLocale } from '../i18n/languages'
import { UniverseCanvas } from './UniverseCanvas'
import './LoginPage.css'

/* The pen.dev frame "Sign in — Resource Universe": a live, pointer-driven scene of everything the organization
 * manages on the brand panel, and a single way in on the sign-in panel. The scene's numbers are illustrative;
 * only the date and time are real. */
const SCENE = { total: 38, ready: 23 }
/* One shape per orbit, matching the shader (dot, diamond, square, ring from the inside out). */
const KINDS = [
  { key: 'people', shape: 'dot' },
  { key: 'assets', shape: 'diamond' },
  { key: 'projects', shape: 'square' },
  { key: 'data', shape: 'ring' },
] as const
const PRINCIPLES = ['realtime', 'conflictFree', 'roleAware'] as const

/* The current time, refreshed every minute, for the live chip and the scene's stats. */
function useNow() {
  const [now, setNow] = useState(() => new Date())
  useEffect(() => {
    const timer = setInterval(() => setNow(new Date()), 60_000)
    return () => clearInterval(timer)
  }, [])
  return now
}

/* The labels over the live scene: what sits at the centre, a few numbers, a key to the shapes and colours, and the
 * hint to move the pointer. The canvas behind is centred on this element. */
function SceneOverlay({ sceneRef }: { sceneRef: RefObject<HTMLDivElement | null> }) {
  const { t } = useTranslation()
  const now = useNow()
  const time = new Intl.DateTimeFormat(intlLocale(), { hour: '2-digit', minute: '2-digit', hourCycle: 'h23' }).format(now)
  return (
    <div ref={sceneRef} className="scene" aria-hidden="true">
      <div className="scene-row">
        <span className="hud-chip mono"><OrbitIcon className="hud-icon lilac" />{t('login.scene.hub')}</span>
        <span className="hud-chip mono ready"><span className="live-dot" />{t('login.scene.stats', { ...SCENE, time })}</span>
      </div>
      <div className="scene-row">
        <span className="hud-chip legend">
          {KINDS.map(({ key, shape }) => (
            <span key={key} className="legend-item"><span className={`kind-shape ${shape}`} />{t(`login.scene.kinds.${key}`)}</span>
          ))}
          <span className="legend-divider" />
          <span className="legend-item"><span className="status-dot ready" />{t('login.scene.status.ready')}</span>
          <span className="legend-item"><span className="status-dot busy" />{t('login.scene.status.inProgress')}</span>
        </span>
        <span className="hud-chip"><MousePointer2Icon className="hud-icon green flip-rtl" />{t('login.scene.hint')}</span>
      </div>
    </div>
  )
}

/* The three principles take turns being highlighted: the active one's bar fills, and when it is full the next one
 * takes over. Hovering pauses the bar; clicking picks one. With reduced motion the bar never runs, so the first
 * stays highlighted until another is clicked. */
function Principles() {
  const { t } = useTranslation()
  const [active, setActive] = useState(0)
  return (
    <div className="principles">
      {PRINCIPLES.map((key, i) => (
        <div key={key} className={`principle${i === active ? ' active' : ''}`} onClick={() => setActive(i)}>
          <span className="principle-bar">
            <span className="principle-fill" onAnimationEnd={() => setActive((i + 1) % PRINCIPLES.length)} />
          </span>
          <span className="principle-head">
            <span className="principle-num">{String(i + 1).padStart(2, '0')}</span>
            {t(`login.principles.${key}.title`)}
          </span>
          <p>{t(`login.principles.${key}.desc`)}</p>
        </div>
      ))}
    </div>
  )
}

/* The page never scrolls: when the window is shorter than a panel's content, the content is shrunk evenly
 * (CSS zoom) to fit, so the design keeps its proportions. It never grows past the design's size, and stops at
 * MIN_FIT so text stays readable; a window shorter than that scrolls the panel instead. */
const MIN_FIT = 0.6

function FitPanel({ className, backdrop, children }: { className: string; backdrop?: ReactNode; children: ReactNode }) {
  const ref = useRef<HTMLDivElement>(null)
  const { i18n } = useTranslation()
  useLayoutEffect(() => {
    const el = ref.current
    if (!el) return
    const fit = () => {
      el.style.setProperty('--fit', '1')
      el.style.height = 'auto'
      const natural = el.scrollHeight
      el.style.height = ''
      /* A hidden panel (narrow screens) measures 0 and simply stays at 1. */
      el.style.setProperty('--fit', String(Math.max(MIN_FIT, Math.min(1, window.innerHeight / natural))))
    }
    fit()
    /* Again once the web fonts are in, as they change the text's size. */
    void document.fonts?.ready.then(fit)
    window.addEventListener('resize', fit)
    return () => window.removeEventListener('resize', fit)
  }, [i18n.language])
  return <section className={className}>{backdrop}<div ref={ref} className="panel-fit">{children}</div></section>
}

/* One way in: the real ABP sign-in page (email + password), which returns a token whose role
 * decides the admin or user interface. */
function LoginPage() {
  const auth = useAuth()
  /* Where a signed-out visitor was going (RequireAuth passes it), so sign-in brings them back there. */
  const returnTo = (useLocation().state as { returnTo?: string } | null)?.returnTo
  const { t } = useTranslation()
  const now = useNow()
  const today = new Intl.DateTimeFormat(intlLocale(), { weekday: 'short', day: 'numeric', month: 'short' }).format(now)
  const sceneRef = useRef<HTMLDivElement>(null)

  return (
    <div className="login-page">
      <FitPanel className="brand-panel" backdrop={<UniverseCanvas anchor={sceneRef} />}>
        <div className="brand-top">
          <img className="brand-logo" src={dixelsLogo} alt="Dixels" />
          <span className="live-chip"><span className="live-dot" />{t('login.live', { date: today })}</span>
        </div>
        <div className="hero">
          <p className="eyebrow">{t('login.eyebrow')}</p>
          <h1>{t('login.headline')}</h1>
        </div>
        <SceneOverlay sceneRef={sceneRef} />
        <Principles />
      </FitPanel>

      <FitPanel className="signin-panel">
        <div className="signin-top">
          <LanguageSwitcher className="login-lang" />
          <ThemeToggle className="login-theme" />
        </div>
        <div className="signin-center">
          <div className="signin-form">
            <div className="signin-heading">
              <span className="mark" aria-hidden="true"><OrbitIcon /></span>
              <h2>{t('login.welcome')}</h2>
              <p className="subtitle">{t('login.subtitle')}</p>
            </div>
            <div className="signin-actions">
              <button type="button" className="signin-btn" onClick={() => auth.signinRedirect({ state: { returnTo } })}>
                <MailIcon className="signin-mail" aria-hidden="true" />
                {t('login.button')}
                <ArrowRightIcon className="flip-rtl" aria-hidden="true" />
              </button>
              <p className="signin-note"><LockIcon aria-hidden="true" />{t('login.companyOnly')}</p>
            </div>
            <ul className="signin-next">
              <li>
                <span className="next-icon" aria-hidden="true"><MailCheckIcon /></span>
                <span className="next-text"><strong>{t('login.next.form.title')}</strong>{t('login.next.form.desc')}</span>
              </li>
              <li>
                <span className="next-icon" aria-hidden="true"><ShieldCheckIcon /></span>
                <span className="next-text"><strong>{t('login.next.role.title')}</strong>{t('login.next.role.desc')}</span>
              </li>
            </ul>
          </div>
        </div>
        <footer className="signin-footer">
          <span>{t('login.footer.copyright', { year: now.getFullYear() })}</span>
          <span>{t('login.footer.noAccount')}</span>
        </footer>
      </FitPanel>
    </div>
  )
}

export default LoginPage
