import { useEffect, useLayoutEffect, useRef, useState, type CSSProperties, type ReactNode } from 'react'
import { useTranslation } from 'react-i18next'
import { useAuth } from 'react-oidc-context'
import {
  ArrowRightIcon, CalendarCheck2Icon, LockIcon, MailIcon, ShieldCheckIcon,
  UserCheckIcon, ZapIcon,
} from 'lucide-react'
import '@fontsource-variable/inter-tight'
import '@fontsource-variable/jetbrains-mono'
import dixelsLogo from '../assets/dixels-logo.png'
import { LanguageSwitcher } from '../components/LanguageSwitcher'
import { intlLocale } from '../i18n/languages'
import './LoginPage.css'

/* The sample "Today" board on the brand panel (pen.dev frame "Sign in — Booking board"). Its bookings are made up;
 * only the date and the NOW line are real. Times run 09:00–18:00 in half-hour steps: 18 columns per row. */
const FIRST_HOUR = 9
const HOURS = 9
type Kind = 'booked' | 'yours' | 'free'
type RoomKey = 'atlas' | 'harbor' | 'studioKit' | 'podcastKit' | 'focusPod'
type BookingKey = 'leadership' | 'interview' | 'designCrit' | 'sprintPlanning' | 'sprintReview' | 'productShoot' | 'bookNow' | 'workshop'
  | 'recording' | 'editSession' | 'focus' | 'deepWork'
interface Block { start: number; span: number; kind: Kind; label?: BookingKey }
interface Row { key: RoomKey; blocks: Block[] }

const ROWS: Row[] = [
  { key: 'atlas', blocks: [{ start: 0, span: 3, kind: 'booked', label: 'leadership' }, { start: 5, span: 2, kind: 'booked', label: 'interview' },
    { start: 10, span: 4, kind: 'booked', label: 'designCrit' }] },
  { key: 'harbor', blocks: [{ start: 2, span: 4, kind: 'booked', label: 'sprintPlanning' }, { start: 11, span: 3, kind: 'yours', label: 'sprintReview' }] },
  { key: 'studioKit', blocks: [{ start: 0, span: 6, kind: 'booked', label: 'productShoot' }, { start: 9, span: 3, kind: 'free', label: 'bookNow' },
    { start: 14, span: 4, kind: 'booked', label: 'workshop' }] },
  { key: 'podcastKit', blocks: [{ start: 4, span: 4, kind: 'booked', label: 'recording' }, { start: 12, span: 3, kind: 'booked', label: 'editSession' }] },
  { key: 'focusPod', blocks: [{ start: 1, span: 2, kind: 'booked', label: 'focus' }, { start: 8, span: 1, kind: 'booked' },
    { start: 13, span: 4, kind: 'booked', label: 'deepWork' }] },
]

const PRINCIPLES = [
  { key: 'instant', Icon: ZapIcon },
  { key: 'exclusive', Icon: ShieldCheckIcon },
  { key: 'roleAware', Icon: UserCheckIcon },
] as const

/* The current time, refreshed every minute, for the live chip and the NOW line. */
function useNow() {
  const [now, setNow] = useState(() => new Date())
  useEffect(() => {
    const timer = setInterval(() => setNow(new Date()), 60_000)
    return () => clearInterval(timer)
  }, [])
  return now
}

function BookingBoard() {
  const { t } = useTranslation()
  const now = useNow()
  const minutes = (now.getHours() - FIRST_HOUR) * 60 + now.getMinutes()
  /* Where the NOW line sits along the day (0..1); hidden outside the board's hours. */
  const progress = minutes >= 0 && minutes <= HOURS * 60 ? minutes / (HOURS * 60) : null
  const time = new Intl.DateTimeFormat(intlLocale(), { hour: '2-digit', minute: '2-digit', hourCycle: 'h23' }).format(now)
  const hours = Array.from({ length: HOURS }, (_, i) => `${String(FIRST_HOUR + i).padStart(2, '0')}:00`)
  return (
    <div className="board" aria-hidden="true">
      <div className="board-head">
        <div className="board-title">
          <span className="board-today">{t('login.board.today')}</span>
          <span className="board-count">{t('login.board.count', { booked: 5, total: 14 })}</span>
        </div>
        <div className="board-legend">
          {(['booked', 'yours', 'free'] as const).map((kind) => (
            <span key={kind} className="legend-item"><span className={`swatch ${kind}`} />{t(`login.board.legend.${kind}`)}</span>
          ))}
        </div>
      </div>
      <div className="board-grid" style={progress === null ? undefined : { '--now': progress } as CSSProperties}>
        <div className="board-hours">
          <span />
          <div className="track">{hours.map((h) => <span key={h} className="hour">{h}</span>)}</div>
        </div>
        {ROWS.map((row) => (
          <div key={row.key} className="board-row">
            <div className="board-space">
              <span className="space-name">{t(`login.board.rooms.${row.key}.name`)}</span>
              <span className="space-meta">{t(`login.board.rooms.${row.key}.meta`)}</span>
            </div>
            <div className="track">
              {row.blocks.map((b) => (
                <span key={b.start} className={`block ${b.kind}`} style={{ gridColumn: `${b.start + 1} / span ${b.span}` }}>
                  {b.label && <span className="block-label">{t(`login.board.bookings.${b.label}`)}</span>}
                </span>
              ))}
            </div>
          </div>
        ))}
        {progress !== null && (
          <>
            <span className="now-line" />
            <span className="now-pill">{t('login.board.now', { time })}</span>
          </>
        )}
      </div>
    </div>
  )
}

/* The page never scrolls: when the window is shorter than a panel's content, the content is shrunk evenly
 * (CSS zoom) to fit, so the design keeps its proportions. It never grows past the design's size, and stops at
 * MIN_FIT so text stays readable; a window shorter than that scrolls the panel instead. */
const MIN_FIT = 0.6

function FitPanel({ className, children }: { className: string; children: ReactNode }) {
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
  return <section className={className}><div ref={ref} className="panel-fit">{children}</div></section>
}

/* One way in: the real ABP sign-in page (email + password), which returns a token whose role
 * decides the admin or user interface. */
function LoginPage() {
  const auth = useAuth()
  const { t } = useTranslation()
  const now = useNow()
  const today = new Intl.DateTimeFormat(intlLocale(), { weekday: 'short', day: 'numeric', month: 'short' }).format(now)

  return (
    <div className="login-page">
      <FitPanel className="brand-panel">
        <div className="brand-top">
          <img className="brand-logo" src={dixelsLogo} alt="Dixels" />
          <span className="live-chip"><span className="live-dot" />{t('login.live', { date: today })}</span>
        </div>
        <div className="brand-main">
          <div className="hero">
            <p className="eyebrow">{t('login.eyebrow')}</p>
            <h1>{t('login.headline')}</h1>
            <p className="lede">{t('login.pitch')}</p>
          </div>
          <BookingBoard />
        </div>
        <div className="principles">
          {PRINCIPLES.map(({ key, Icon }) => (
            <div key={key} className="principle">
              <div className="principle-head"><Icon aria-hidden="true" />{t(`login.principles.${key}.title`)}</div>
              <p>{t(`login.principles.${key}.desc`)}</p>
            </div>
          ))}
        </div>
      </FitPanel>

      <FitPanel className="signin-panel">
        <div className="signin-top">
          <LanguageSwitcher className="login-lang" />
        </div>
        <div className="signin-center">
          <div className="signin-form">
            <div className="signin-heading">
              <span className="mark" aria-hidden="true"><CalendarCheck2Icon /></span>
              <h2>{t('login.welcome')}</h2>
              <p className="subtitle">{t('login.subtitle')}</p>
            </div>
            <div className="signin-actions">
              <button type="button" className="signin-btn" onClick={() => auth.signinRedirect()}>
                <MailIcon className="signin-mail" aria-hidden="true" />
                {t('login.button')}
                <ArrowRightIcon className="flip-rtl" aria-hidden="true" />
              </button>
              <p className="signin-note"><LockIcon aria-hidden="true" />{t('login.companyOnly')}</p>
            </div>
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
