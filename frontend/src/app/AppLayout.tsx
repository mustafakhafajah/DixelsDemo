import { useEffect, useMemo, useState } from 'react'
import { NavLink, Outlet, useLocation } from 'react-router-dom'
import dixelsLogo from '../assets/dixels-logo.png'
import { useBookings, useBuildings, useFloors, useSpaces, useSpaceTypes } from '../api/hooks'
import { initials } from '../components/bits'
import { Toasts } from '../components/Toasts'
import { dayAt, todayKey } from '../lib/dateUtils'
import { modals } from '../state/modalStore'
import { useNavCountStore } from '../state/navCountStore'
import { useThemeStore } from '../state/themeStore'
import { Overlays } from './Overlays'
import { PageActionsSlot } from './pageActions'
import { ESTATE_PAGES, manageAny, useSession } from './session'
import { P } from '../auth/permissions'
import './appShell.css'

const PAGE_META: Record<string, [string, string]> = {
  find: ['Find a space', 'See every room at a glance, or search a specific time. No approval step.'],
  bookings: ['Bookings', 'A calendar view of your bookings.'],
  buildings: ['Buildings', "The estate every floor and space belongs to. A building's time zone applies to every floor and space in it."],
  floors: ['Floors', "Every floor across every building. A floor uses its building's time zone."],
  spaces: ['Spaces', "The units people can book. One row is one physical unit; a space uses its building's time zone."],
  'space-types': ['Space types', 'The kinds of space an admin can give a space. Renaming a type updates every space that uses it.'],
  users: ['User directory', 'Portal accounts, their roles and access.'],
}

/* "New booking" only where booking is the task at hand. */
const BOOKING_VIEWS = new Set(['find', 'bookings'])

const Icon = {
  find: <svg width="16" height="16" viewBox="0 0 16 16" fill="none" stroke="currentColor" strokeWidth="1.6"><circle cx="7" cy="7" r="4.6" /><path d="M10.4 10.4L14 14" strokeLinecap="round" /></svg>,
  bookings: <svg width="16" height="16" viewBox="0 0 16 16" fill="none" stroke="currentColor" strokeWidth="1.6"><rect x="2" y="3.2" width="12" height="10.6" rx="1.8" /><path d="M2 6.4h12M5.4 1.8v2.6M10.6 1.8v2.6" strokeLinecap="round" /></svg>,
  users: <svg width="13" height="13" viewBox="0 0 16 16" fill="none" stroke="currentColor" strokeWidth="1.6"><circle cx="6" cy="5.4" r="2.6" /><path d="M1.6 13.6c.6-2.4 2.4-3.8 4.4-3.8s3.8 1.4 4.4 3.8M10.6 3.2a2.4 2.4 0 010 4.6M12.2 9.9c1.2.5 1.9 1.6 2.2 3.1" strokeLinecap="round" /></svg>,
  estate: <svg width="13" height="13" viewBox="0 0 16 16" fill="none" stroke="currentColor" strokeWidth="1.6"><rect x="1.8" y="1.8" width="12.4" height="12.4" rx="1.5" /><path d="M1.8 7h12.4M7 1.8v12.4" strokeLinecap="round" /></svg>,
  menu: <svg width="18" height="18" viewBox="0 0 16 16" fill="none" stroke="currentColor" strokeWidth="1.6"><path d="M2.5 4h11M2.5 8h11M2.5 12h11" strokeLinecap="round" /></svg>,
  signOut: <svg width="15" height="15" viewBox="0 0 16 16" fill="none" stroke="currentColor" strokeWidth="1.6"><path d="M6.4 2.6H3.6A1.2 1.2 0 002.4 3.8v8.4a1.2 1.2 0 001.2 1.2h2.8M10 11l3-3-3-3M13 8H6.2" strokeLinecap="round" strokeLinejoin="round" /></svg>,
  sun: <svg width="16" height="16" viewBox="0 0 16 16" fill="none" stroke="currentColor" strokeWidth="1.6"><circle cx="8" cy="8" r="3" /><path d="M8 1.4v1.4M8 13.2v1.4M1.4 8h1.4M13.2 8h1.4M3.3 3.3l1 1M11.7 11.7l1 1M3.3 12.7l1-1M11.7 4.3l1-1" strokeLinecap="round" /></svg>,
  moon: <svg width="16" height="16" viewBox="0 0 16 16" fill="none" stroke="currentColor" strokeWidth="1.6"><path d="M13.6 9.9A5.8 5.8 0 016.1 2.4a5.8 5.8 0 107.5 7.5z" strokeLinejoin="round" /></svg>,
}

const navClass = ({ isActive }: { isActive: boolean }) => `nav-link${isActive ? ' active' : ''}`

/* A stored role name as a label: "admin" -> "Admin", "space-manager" -> "Space manager". */
const roleLabel = (role: string) => {
  const words = role.replace(/[-_]+/g, ' ').trim()
  return words.charAt(0).toUpperCase() + words.slice(1)
}

/* Management menu: a link per area the user can change. Its counts need the estate lists,
 * so they are loaded only when the menu is shown. */
function SpaceManagementNav({ show }: { show: Record<string, boolean> }) {
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
      <p className="nav-group-title" style={{ display: 'flex', alignItems: 'center', gap: 6, marginTop: 8 }}>{Icon.estate}Space management</p>
      <div style={{ marginLeft: 6, paddingLeft: 9, borderLeft: '1px solid var(--line)', display: 'flex', flexDirection: 'column', gap: 1, marginBottom: 8 }}>
        {ESTATE_PAGES.filter((p) => show[p.path]).map((p) => (
          <NavLink key={p.path} to={p.path} className={navClass}>{p.label}<span className="nav-count">{counts[p.path] ?? ''}</span></NavLink>
        ))}
      </div>
    </div>
  )
}

function Sidebar({ open, onNavigate }: { open: boolean; onNavigate: () => void }) {
  const session = useSession()
  const today = useMemo(() => dayAt(todayKey()), [])
  const seesBookings = session.can(P.Bookings.Default)
  const bookings = useBookings({ from: today, ownerUserId: session.managesAll ? undefined : session.userId || undefined }, !!session.userId && seesBookings)
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
              <span>{session.managesAll ? 'Schedule' : 'My Schedule'}</span>
              <span className="nav-count">{upcoming ?? ''}</span>
            </NavLink>
          </div>
        )}
        <div>
          {session.can(P.Spaces.Default) && <NavLink to="/app/find" className={({ isActive }) => `${navClass({ isActive })} nav-primary`}>{Icon.find}Find a space</NavLink>}
          {Object.values(showEstate).some(Boolean) && <SpaceManagementNav show={showEstate} />}
          {/* Administration: admin-only tools, shown to whoever holds the permission. */}
          {session.can(P.Users.Default) && (
            <div>
              <p className="nav-group-title" style={{ display: 'flex', alignItems: 'center', gap: 6, marginTop: 8 }}>{Icon.users}Administration</p>
              <div style={{ marginLeft: 6, paddingLeft: 9, borderLeft: '1px solid var(--line)', display: 'flex', flexDirection: 'column', gap: 1, marginBottom: 8 }}>
                <NavLink to="/app/users" className={navClass}>User directory</NavLink>
              </div>
            </div>
          )}
        </div>
      </nav>

      <div style={{ marginTop: 'auto', borderTop: '1px solid var(--line)', paddingTop: 13 }}>
        <div style={{ display: 'flex', alignItems: 'center', gap: 10, padding: '0 4px 10px' }}>
          <div className="avatar">{initials(session.name)}</div>
          <div style={{ minWidth: 0, flex: 1 }}>
            <div style={{ fontSize: 13, fontWeight: 600, color: 'var(--ink)', overflow: 'hidden', textOverflow: 'ellipsis', whiteSpace: 'nowrap' }}>{session.name}</div>
            <div style={{ fontSize: 11, color: 'var(--slate)' }}>
              {session.roles.length ? session.roles.map(roleLabel).join(', ') : 'No role'}
            </div>
          </div>
        </div>
        <button type="button" className="nav-link" style={{ fontSize: 12.5, padding: '7px 10px' }} onClick={session.signOut}>
          {Icon.signOut}Sign out
        </button>
      </div>
    </aside>
  )
}

function Topbar({ onMenu, onSlot }: { onMenu: () => void; onSlot: (el: HTMLDivElement | null) => void }) {
  const { pathname } = useLocation()
  const { managesAll, can } = useSession()
  const view = pathname.split('/')[2] || 'bookings'
  const meta = PAGE_META[view] ?? PAGE_META.bookings
  const title = view === 'bookings' ? (managesAll ? 'Schedule' : 'My Schedule') : meta[0]
  const { theme, toggle } = useThemeStore()
  const dark = theme === 'dark'
  const themeLabel = dark ? 'Switch to light theme' : 'Switch to dark theme'
  return (
    <header className="topbar">
      {/* Only shown on phones and tablets, where the sidebar slides in instead of standing beside the page. */}
      <button type="button" className="iconbtn menu-btn" onClick={onMenu} aria-label="Open menu" aria-controls="app-nav">{Icon.menu}</button>
      <div style={{ flex: 1, minWidth: 0 }}>
        <h1 style={{ fontSize: 16, letterSpacing: '-.01em' }}>{title}</h1>
        <p style={{ fontSize: 12, color: 'var(--slate)', margin: '2px 0 0' }}>{meta[1]}</p>
      </div>
      {/* Moon in light, sun in dark: the icon shows what a click switches to. */}
      <button type="button" className="iconbtn theme-toggle" aria-pressed={dark} aria-label={themeLabel} title={themeLabel} onClick={toggle}>{dark ? Icon.sun : Icon.moon}</button>
      {/* The page's own main buttons (PageActions) land here, next to New booking. */}
      <div ref={onSlot} className="page-actions" />
      {BOOKING_VIEWS.has(view) && can(P.Bookings.Create) && <button type="button" className="btn btn-primary" onClick={() => modals.booking()}>New booking</button>}
    </header>
  )
}

export function AppLayout() {
  const [navOpen, setNavOpen] = useState(false)
  const [actionsSlot, setActionsSlot] = useState<HTMLDivElement | null>(null)
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
