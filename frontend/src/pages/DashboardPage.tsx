import { Link, Navigate, useLocation } from 'react-router-dom'
import type { DemoRole } from '../data/demoAccounts'

interface DashboardLocationState {
  name: string
  role: DemoRole
  team: string
}

function DashboardPage() {
  const location = useLocation()
  const state = location.state as DashboardLocationState | null

  if (!state) {
    return <Navigate to="/" replace />
  }

  return (
    <div style={{ padding: 40, fontFamily: 'Inter, ui-sans-serif, system-ui, sans-serif' }}>
      <h1>Signed in as {state.name}</h1>
      <p>
        {state.role === 'administrator' ? 'Space administrator' : 'Booking user'} · {state.team} team
      </p>
      <p>
        <Link to="/">Sign out</Link>
      </p>
    </div>
  )
}

export default DashboardPage
