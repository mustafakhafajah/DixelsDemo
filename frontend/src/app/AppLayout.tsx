import { useEffect, useMemo, useState } from 'react'
import { useTranslation } from 'react-i18next'
import { NavLink, Outlet, useLocation } from 'react-router-dom'
import { LanguagesIcon, MoonIcon, SunIcon } from 'lucide-react'
import {
  DropdownMenu, DropdownMenuContent, DropdownMenuItem, DropdownMenuLabel, DropdownMenuRadioGroup, DropdownMenuRadioItem, DropdownMenuSeparator,
  DropdownMenuSub, DropdownMenuSubContent, DropdownMenuSubTrigger, DropdownMenuTrigger,
} from '@/components/ui/dropdown-menu'
import dixelsLogo from '../assets/dixels-logo.png'
import { useBookings, useBuildings, useFloors, useSpaces, useSpaceTypes } from '../api/hooks'
import { initials } from '../components/bits'
import { Toasts } from '../components/Toasts'
import { roleLabel } from '../features/users/api'
import { setLanguage } from '../i18n'
import { LANGUAGES } from '../i18n/languages'
import { useLanguagePreferenceSync } from '../i18n/useLanguagePreferenceSync'
import { dayAt, todayKey } from '../lib/dateUtils'
import { modals } from '../state/modalStore'
import { useNavCountStore } from '../state/navCountStore'
import { useThemeStore, type Theme } from '../state/themeStore'
import { Overlays } from './Overlays'
import { PageActionsSlot } from './pageActions'
import { ESTATE_PAGES, manageAny, useSession } from './session'
import { P } from '../auth/permissions'
import './appShell.css'

/* Each page's title and the line under it, as translation keys. */
const PAGE_META = {
  find: ['pages.find.title', 'pages.find.subtitle'],
  bookings: ['pages.bookings.title', 'pages.bookings.subtitle'],
  buildings: ['pages.buildings.title', 'pages.buildings.subtitle'],
  floors: ['pages.floors.title', 'pages.floors.subtitle'],
  spaces: ['pages.spaces.title', 'pages.spaces.subtitle'],
  'space-types': ['pages.spaceTypes.title', 'pages.spaceTypes.subtitle'],
  users: ['pages.users.title', 'pages.users.subtitle'],
} as const

/* "New booking" only where booking is the task at hand. */
const BOOKING_VIEWS = new Set(['find', 'bookings'])

const Icon = {
  find: <svg width="16" height="16" viewBox="0 0 16 16" fill="none" stroke="currentColor" strokeWidth="1.6"><circle cx="7" cy="7" r="4.6" /><path d="M10.4 10.4L14 14" strokeLinecap="round" /></svg>,
  bookings: <svg width="16" height="16" viewBox="0 0 16 16" fill="none" stroke="currentColor" strokeWidth="1.6"><rect x="2" y="3.2" width="12" height="10.6" rx="1.8" /><path d="M2 6.4h12M5.4 1.8v2.6M10.6 1.8v2.6" strokeLinecap="round" /></svg>,
  users: <svg width="13" height="13" viewBox="0 0 16 16" fill="none" stroke="currentColor" strokeWidth="1.6"><circle cx="6" cy="5.4" r="2.6" /><path d="M1.6 13.6c.6-2.4 2.4-3.8 4.4-3.8s3.8 1.4 4.4 3.8M10.6 3.2a2.4 2.4 0 010 4.6M12.2 9.9c1.2.5 1.9 1.6 2.2 3.1" strokeLinecap="round" /></svg>,
  estate: <svg width="13" height="13" viewBox="0 0 16 16" fill="none" stroke="currentColor" strokeWidth="1.6"><rect x="1.8" y="1.8" width="12.4" height="12.4" rx="1.5" /><path d="M1.8 7h12.4M7 1.8v12.4" strokeLinecap="round" /></svg>,
  menu: <svg width="18" height="18" viewBox="0 0 16 16" fill="none" stroke="currentColor" strokeWidth="1.6"><path d="M2.5 4h11M2.5 8h11M2.5 12h11" strokeLinecap="round" /></svg>,
  /* The arrow points out of the door, so it turns round in right-to-left languages. */
  signOut: <svg className="flip-rtl" width="15" height="15" viewBox="0 0 16 16" fill="none" stroke="currentColor" strokeWidth="1.6"><path d="M6.4 2.6H3.6A1.2 1.2 0 002.4 3.8v8.4a1.2 1.2 0 001.2 1.2h2.8M10 11l3-3-3-3M13 8H6.2" strokeLinecap="round" strokeLinejoin="round" /></svg>,
  chevrons: <svg width="14" height="14" viewBox="0 0 16 16" fill="none" stroke="currentColor" strokeWidth="1.6" aria-hidden="true"><path d="M5 6l3-3 3 3M5 10l3 3 3-3" strokeLinecap="round" strokeLinejoin="round" /></svg>,
}

const navClass = ({ isActive }: { isActive: boolean }) => `nav-link${isActive ? ' active' : ''}`

/* Management menu: a link per area the user can change. Its counts need the estate lists,
 * so they are loaded only when the menu is shown. */
function SpaceManagementNav({ show }: { show: Record<string, boolean> }) {
  const { t } = useTranslation()
  const spaces = useSpaces()
  const buildings = useBuildings()
  const floors = useFloors()
  const spaceTypes = useSpaceTypes()
  /* A filtered Floors or Spaces page puts its match count here instead of the full count. */
  const filtered = useNavCountStore((s) => s.counts)
  const counts: Record<string, number | undefined> = {
    '/app/buildings': buildings.data?.length,
    '/app/floors': filtered.floors ?? floors.data?.length,
    '/app/spaces': filtered.spaces ?? spaces.data?.length,
    '/app/space-types': spaceTypes.data?.length,
  }
  return (
    <div>
      <p className="nav-group-title" style={{ display: 'flex', alignItems: 'center', gap: 6, marginTop: 8 }}>{Icon.estate}{t('nav.spaceManagement')}</p>
      <div className="nav-sub">
        {ESTATE_PAGES.filter((p) => show[p.path]).map((p) => (
          <NavLink key={p.path} to={p.path} className={navClass}>{t(p.label)}<span className="nav-count">{counts[p.path] ?? ''}</span></NavLink>
        ))}
      </div>
    </div>
  )
}

function Sidebar({ open, onNavigate }: { open: boolean; onNavigate: () => void }) {
  const { t, i18n } = useTranslation()
  const session = useSession()
  const { theme, setTheme } = useThemeStore()
  const today = useMemo(() => dayAt(todayKey()), [])
  const seesBookings = session.can(P.Bookings.Default)
  const bookings = useBookings({ from: today, ownerUserId: session.can(P.Bookings.ViewAll) ? undefined : session.userId || undefined }, !!session.userId && seesBookings)
  const now = Date.now()
  const upcoming = bookings.data?.filter((b) => b.end.getTime() > now).length
  const showEstate = Object.fromEntries(ESTATE_PAGES.map((p) => [p.path, manageAny(p.area).some(session.can)]))

  return (
    <aside className={`sidebar${open ? ' open' : ''}`} id="app-nav">
      {/* Light: the purple wordmark. Dark: the black tile with the white mark (appShell.css shows one). */}
      <div className="sidebar-logo">
        <img className="logo-light" src={dixelsLogo} alt="Dixels" />
        <span className="logo-dark" role="img" aria-label="Dixels">
          <span className="logo-tile"><span className="logo-mark"><img src={dixelsLogo} alt="" /></span></span>
          <span className="logo-word" aria-hidden="true">DIXELS</span>
        </span>
      </div>

      {/* Picking a page closes the slide-in menu on phones and tablets. */}
      <nav style={{ display: 'flex', flexDirection: 'column', gap: 18 }} onClick={(e) => (e.target as HTMLElement).closest('a') && onNavigate()}>
        {seesBookings && (
          <div>
            <NavLink to="/app/bookings" className={navClass}>
              {Icon.bookings}
              <span>{session.can(P.Bookings.ViewAll) ? t('nav.schedule') : t('nav.mySchedule')}</span>
              <span className="nav-count">{upcoming ?? ''}</span>
            </NavLink>
          </div>
        )}
        <div>
          {session.can(P.Spaces.Default) && <NavLink to="/app/find" className={({ isActive }) => `${navClass({ isActive })} nav-primary`}>{Icon.find}{t('nav.findSpace')}</NavLink>}
          {Object.values(showEstate).some(Boolean) && <SpaceManagementNav show={showEstate} />}
          {/* Administration: admin-only tools, shown to whoever holds the permission. */}
          {session.can(P.Users.Default) && (
            <div>
              <p className="nav-group-title" style={{ display: 'flex', alignItems: 'center', gap: 6, marginTop: 8 }}>{Icon.users}{t('nav.administration')}</p>
              <div className="nav-sub">
                <NavLink to="/app/users" className={navClass}>{t('nav.userDirectory')}</NavLink>
              </div>
            </div>
          )}
        </div>
      </nav>

      <div style={{ marginTop: 'auto', borderTop: '1px solid var(--line)', paddingTop: 13 }}>
        {/* The person's name opens a small menu: who is signed in, Language and Theme submenus, then Sign out. */}
        <DropdownMenu>
          <DropdownMenuTrigger asChild>
            <button type="button" className="user-chip" aria-label={t('nav.accountMenu', { name: session.name })}>
              <div className="avatar">{initials(session.name)}</div>
              <div style={{ minWidth: 0, flex: 1 }}>
                <div style={{ fontSize: 13, fontWeight: 600, color: 'var(--ink)', overflow: 'hidden', textOverflow: 'ellipsis', whiteSpace: 'nowrap' }}><bdi>{session.name}</bdi></div>
                <div style={{ fontSize: 11, color: 'var(--slate)' }}>
                  {session.roles.length ? session.roles.map((r) => roleLabel(r)).join(t('common.listSeparator')) : t('nav.noRole')}
                </div>
              </div>
              {Icon.chevrons}
            </button>
          </DropdownMenuTrigger>
          {/* The menu is drawn outside the app shell, so it uses the shadcn colours rather than the shell's. */}
          <DropdownMenuContent side="top" align="start" className="tw:w-(--radix-dropdown-menu-trigger-width)">
            <DropdownMenuLabel className="tw:flex tw:items-center tw:gap-2.5 tw:font-normal">
              <span className="tw:flex tw:size-8 tw:shrink-0 tw:items-center tw:justify-center tw:rounded-full tw:bg-primary tw:text-xs tw:font-bold tw:text-primary-foreground">{initials(session.name)}</span>
              <span className="tw:min-w-0">
                <span className="tw:block tw:truncate tw:text-sm tw:font-semibold"><bdi>{session.name}</bdi></span>
                <span className="tw:block tw:truncate tw:text-xs tw:text-muted-foreground">
                  {session.roles.length ? session.roles.map((r) => roleLabel(r)).join(t('common.listSeparator')) : t('nav.noRole')}
                </span>
              </span>
            </DropdownMenuLabel>
            <DropdownMenuSeparator />
            <DropdownMenuSub>
              <DropdownMenuSubTrigger><LanguagesIcon />{t('language.label')}</DropdownMenuSubTrigger>
              <DropdownMenuSubContent className="tw:min-w-40">
                {/* Each language is listed by its own name, so anyone can find theirs whatever the page is in now. */}
                <DropdownMenuRadioGroup value={i18n.language} onValueChange={(code) => { void setLanguage(code) }}>
                  {LANGUAGES.map((l) => <DropdownMenuRadioItem key={l.code} value={l.code} lang={l.code}>{l.name}</DropdownMenuRadioItem>)}
                </DropdownMenuRadioGroup>
              </DropdownMenuSubContent>
            </DropdownMenuSub>
            <DropdownMenuSub>
              <DropdownMenuSubTrigger>{theme === 'dark' ? <MoonIcon /> : <SunIcon />}{t('nav.theme')}</DropdownMenuSubTrigger>
              <DropdownMenuSubContent className="tw:min-w-40">
                <DropdownMenuRadioGroup value={theme} onValueChange={(v) => setTheme(v as Theme)}>
                  <DropdownMenuRadioItem value="light"><SunIcon />{t('nav.themeLightName')}</DropdownMenuRadioItem>
                  <DropdownMenuRadioItem value="dark"><MoonIcon />{t('nav.themeDarkName')}</DropdownMenuRadioItem>
                </DropdownMenuRadioGroup>
              </DropdownMenuSubContent>
            </DropdownMenuSub>
            <DropdownMenuSeparator />
            <DropdownMenuItem onSelect={session.signOut}>{Icon.signOut}{t('nav.signOut')}</DropdownMenuItem>
          </DropdownMenuContent>
        </DropdownMenu>
      </div>
    </aside>
  )
}

function Topbar({ onMenu, onSlot }: { onMenu: () => void; onSlot: (el: HTMLDivElement | null) => void }) {
  const { t } = useTranslation()
  const { pathname } = useLocation()
  const { can } = useSession()
  const view = pathname.split('/')[2] || 'bookings'
  const meta = PAGE_META[view as keyof typeof PAGE_META] ?? PAGE_META.bookings
  const title = view === 'bookings' ? (can(P.Bookings.ViewAll) ? t('nav.schedule') : t('nav.mySchedule')) : t(meta[0])
  const { theme, toggle } = useThemeStore()
  const dark = theme === 'dark'
  const themeLabel = dark ? t('nav.themeLight') : t('nav.themeDark')
  return (
    <header className="topbar">
      {/* Only shown on phones and tablets, where the sidebar slides in instead of standing beside the page. */}
      <button type="button" className="iconbtn menu-btn" onClick={onMenu} aria-label={t('nav.openMenu')} aria-controls="app-nav">{Icon.menu}</button>
      <div style={{ flex: 1, minWidth: 0 }}>
        <h1 style={{ fontSize: 16, letterSpacing: '-.01em' }}>{title}</h1>
        <p style={{ fontSize: 12, color: 'var(--slate)', margin: '2px 0 0' }}>{t(meta[1])}</p>
      </div>
      {/* Moon in light, sun in dark: the icon shows what a click switches to. */}
      {/* One click flips light and dark; the account menu offers the same choice. */}
      <button type="button" aria-pressed={dark} aria-label={themeLabel} title={themeLabel} onClick={toggle}
        className="tw:inline-flex tw:size-9 tw:shrink-0 tw:items-center tw:justify-center tw:rounded-md tw:border-0 tw:bg-transparent tw:text-muted-foreground tw:cursor-pointer tw:outline-none tw:hover:bg-accent tw:hover:text-accent-foreground tw:focus-visible:ring-3 tw:focus-visible:ring-ring/50 tw:[&_svg]:size-[18px]">
        {dark ? <SunIcon /> : <MoonIcon />}
      </button>
      {/* The page's own main buttons (PageActions) land here, next to New booking. */}
      <div ref={onSlot} className="page-actions" />
      {BOOKING_VIEWS.has(view) && can(P.Bookings.Create) && <button type="button" className="btn btn-primary" onClick={() => modals.booking()}>{t('nav.newBooking')}</button>}
    </header>
  )
}

export function AppLayout() {
  const [navOpen, setNavOpen] = useState(false)
  const [actionsSlot, setActionsSlot] = useState<HTMLDivElement | null>(null)
  useLanguagePreferenceSync()
  useEffect(() => {
    if (!navOpen) return
    const onKey = (e: KeyboardEvent) => e.key === 'Escape' && setNavOpen(false)
    window.addEventListener('keydown', onKey)
    return () => window.removeEventListener('keydown', onKey)
  }, [navOpen])

  return (
    <div className="app-shell">
      <Sidebar open={navOpen} onNavigate={() => setNavOpen(false)} />
      {navOpen && <div className="nav-backdrop" onClick={() => setNavOpen(false)} />}
      <div style={{ display: 'flex', flexDirection: 'column', minWidth: 0 }}>
        <Topbar onMenu={() => setNavOpen(true)} onSlot={setActionsSlot} />
        <main className="main">
          <PageActionsSlot.Provider value={actionsSlot}>
            <Outlet />
          </PageActionsSlot.Provider>
        </main>
      </div>
      <Overlays />
      <Toasts />
    </div>
  )
}
