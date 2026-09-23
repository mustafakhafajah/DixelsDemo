import { Navigate } from 'react-router-dom'
import { useAuth } from 'react-oidc-context'

function AdminLandingPage() {
  const auth = useAuth()

  if (auth.isLoading) {
    return <p style={{ padding: 40 }}>Loading…</p>
  }

  if (!auth.isAuthenticated) {
    return <Navigate to="/" replace />
  }

  const { name, email } = auth.user?.profile ?? {}

  return (
    <div style={{ padding: 40, fontFamily: 'Inter, ui-sans-serif, system-ui, sans-serif', maxWidth: 560 }}>
      <h1 style={{ fontSize: 28, lineHeight: 1.3 }}>Welcome, {name ?? email ?? 'Administrator'}</h1>
      <p>
        Signed in as {email} · Role: admin
      </p>
      <p>
        As a space administrator, the full interface would let you manage Buildings, Floors, and
        Spaces, view estate-wide dashboard stats (bookings today, spaces free right now, building
        counts), and schedule maintenance windows for spaces. This stub does not implement those
        screens yet.
      </p>
      <button type="button" onClick={() => auth.signoutRedirect()}>
        Sign out
      </button>
    </div>
  )
}

export default AdminLandingPage
