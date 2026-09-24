import { useMemo } from 'react'
import { NavLink, Outlet, useLocation } from 'react-router-dom'
import dixelsLogo from '../assets/dixels-logo.png'
import { useBookings, useBuildings, useFloors, useSpaces, useSpaceTypes } from '../api/hooks'
import { initials } from '../components/bits'
import { Toasts } from '../components/Toasts'
import { dayAt, todayKey } from '../lib/dateUtils'
import { modals } from '../state/modalStore'
import { Overlays } from './Overlays'
import { useSession } from './session'
import './appShell.css'

const PAGE_META: Record<string, [string, string]> = {
  find: ['Find a space', 'See every room at a glance, or search a specific time. No approval step.'],
  dashboard: ['Dashboard', 'Your day at a glance.'],
  bookings: ['Bookings', 'A calendar view of your bookings.'],
  buildings: ['Buildings', 'The estate every floor and space belongs to.'],
  floors: ['Floors', 'Every floor across every building, and what cleaning applies to it.'],
  spaces: ['Spaces', 'The units people can book, and who may book them.'],
  'space-types': ['Space types', 'The kinds of space an admin can give a space.'],
}

/* "New booking" only where booking is the task at hand. */
const BOOKING_VIEWS = new Set(['find', 'bookings'])

const Icon = {
  find: <svg width="16" height="16" viewBox="0 0 16 16" fill="none" stroke="currentColor" strokeWidth="1.6"><circle cx="7" cy="7" r="4.6" /><path d="M10.4 10.4L14 14" strokeLinecap="round" /></svg>,
  dashboard: <svg width="16" height="16" viewBox="0 0 16 16" fill="none" stroke="currentColor" strokeWidth="1.6"><rect x="1.8" y="1.8" width="5.2" height="5.2" rx="1.3" /><rect x="9" y="1.8" width="5.2" height="5.2" rx="1.3" /><rect x="1.8" y="9" width="5.2" height="5.2" rx="1.3" /><rect x="9" y="9" width="5.2" height="5.2" rx="1.3" /></svg>,
  bookings: <svg width="16" height="16" viewBox="0 0 16 16" fill="none" stroke="currentColor" strokeWidth="1.6"><rect x="2" y="3.2" width="12" height="10.6" rx="1.8" /><path d="M2 6.4h12M5.4 1.8v2.6M10.6 1.8v2.6" strokeLinecap="round" /></svg>,
  estate: <svg width="13" height="13" viewBox="0 0 16 16" fill="none" stroke="currentColor" strokeWidth="1.6"><rect x="1.8" y="1.8" width="12.4" height="12.4" rx="1.5" /><path d="M1.8 7h12.4M7 1.8v12.4" strokeLinecap="round" /></svg>,
  signOut: <svg width="15" height="15" viewBox="0 0 16 16" fill="none" stroke="currentColor" strokeWidth="1.6"><path d="M6.4 2.6H3.6A1.2 1.2 0 002.4 3.8v8.4a1.2 1.2 0 001.2 1.2h2.8M10 11l3-3-3-3M13 8H6.2" strokeLinecap="round" strokeLinejoin="round" /></svg>,
}

const navClass = ({ isActive }: { isActive: boolean }) => `nav-link${isActive ? ' active' : ''}`

function Sidebar() {
  const session = useSession()
  const today = useMemo(() => dayAt(todayKey()), [])
  const bookings = useBookings({ from: today, ownerUserId: session.isAdmin ? undefined : session.userId || undefined }, !!session.userId)
  const spaces = useSpaces()
  const buildings = useBuildings()
  const floors = useFloors()
  const spaceTypes = useSpaceTypes()
  const now = Date.now()
  const upcoming = bookings.data?.filter((b) => b.end.getTime() > now).length

  return (
    <aside className="sidebar">
      <div className="sidebar-logo">
        <img src={dixelsLogo} alt="Dixels" />
      </div>

      <nav style={{ display: 'flex', flexDirection: 'column', gap: 18 }}>
        <div>
          <NavLink to="/app/find" className={({ isActive }) => `${navClass({ isActive })} nav-primary`}>{Icon.find}Find a space</NavLink>
          <NavLink to="/app/dashboard" className={({ isActive }) => `${navClass({ isActive })} nav-primary`}>{Icon.dashboard}Dashboard</NavLink>
        </div>
        <div>
          <NavLink to="/app/bookings" className={navClass}>
            {Icon.bookings}
            <span>{session.isAdmin ? 'Schedule' : 'My Schedule'}</span>
            <span className="nav-count">{upcoming ?? ''}</span>
          </NavLink>
          {session.isAdmin && (
            <div>
              <p className="nav-group-title" style={{ display: 'flex', alignItems: 'center', gap: 6, marginTop: 8 }}>{Icon.estate}Space management</p>
              <div style={{ marginLeft: 6, paddingLeft: 9, borderLeft: '1px solid var(--line)', display: 'flex', flexDirection: 'column', gap: 1, marginBottom: 8 }}>
                <NavLink to="/app/buildings" className={navClass}>Buildings<span className="nav-count">{buildings.data?.length ?? ''}</span></NavLink>
                <NavLink to="/app/floors" className={navClass}>Floors<span className="nav-count">{floors.data?.length ?? ''}</span></NavLink>
                <NavLink to="/app/spaces" className={navClass}>Spaces<span className="nav-count">{spaces.data?.filter((s) => s.status === 'Active').length ?? ''}</span></NavLink>
                <NavLink to="/app/space-types" className={navClass}>Space types<span className="nav-count">{spaceTypes.data?.length ?? ''}</span></NavLink>
              </div>
            </div>
          )}
        </div>
      </nav>

      <div style={{ marginTop: 'auto', borderTop: '1px solid var(--line)', paddingTop: 13 }}>
        <div style={{ display: 'flex', alignItems: 'center', gap: 10, padding: '0 4px 10px' }}>
          <div className={`avatar${session.isAdmin ? ' admin' : ''}`}>{initials(session.name)}</div>
          <div style={{ minWidth: 0, flex: 1 }}>
            <div style={{ fontSize: 13, fontWeight: 600, color: 'var(--ink)', overflow: 'hidden', textOverflow: 'ellipsis', whiteSpace: 'nowrap' }}>{session.name}</div>
            <div style={{ fontSize: 11, color: 'var(--slate)' }}>
              {session.isAdmin ? 'Space administrator' : 'Booking user'}
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

function Topbar() {
  const { pathname } = useLocation()
  const { isAdmin } = useSession()
  const view = pathname.split('/')[2] || 'find'
  const meta = PAGE_META[view] ?? PAGE_META.find
  const title = view === 'bookings' ? (isAdmin ? 'Schedule' : 'My Schedule')
    : view === 'dashboard' ? (isAdmin ? 'Estate dashboard' : 'Dashboard') : meta[0]
  return (
    <header className="topbar">
      <div>
        <h1 style={{ fontSize: 16, letterSpacing: '-.01em' }}>{title}</h1>
        <p style={{ fontSize: 12, color: 'var(--slate)', margin: '2px 0 0' }}>{meta[1]}</p>
      </div>
      {BOOKING_VIEWS.has(view) && <button type="button" className="btn btn-primary" onClick={() => modals.booking()}>New booking</button>}
    </header>
  )
}

export function AppLayout() {
  return (
    <div className="app-shell">
      <Sidebar />
      <div style={{ display: 'flex', flexDirection: 'column', minWidth: 0 }}>
        <Topbar />
        <main className="main">
          <Outlet />
        </main>
      </div>
      <Overlays />
      <Toasts />
    </div>
  )
}
