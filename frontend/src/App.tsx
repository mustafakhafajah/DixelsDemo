import { BrowserRouter, Route, Routes } from 'react-router-dom'
import { AuthProvider } from 'react-oidc-context'
import LoginPage from './pages/LoginPage'
import DashboardPage from './pages/DashboardPage'
import AuthCallbackPage from './pages/AuthCallbackPage'
import AdminLandingPage from './pages/AdminLandingPage'
import UserLandingPage from './pages/UserLandingPage'
import { oidcConfig } from './auth/oidcConfig'

function App() {
  return (
    <AuthProvider
      {...oidcConfig}
      onSigninCallback={() => {
        window.history.replaceState({}, document.title, window.location.pathname)
      }}
    >
      <BrowserRouter>
        <Routes>
          <Route path="/" element={<LoginPage />} />
          <Route path="/dashboard" element={<DashboardPage />} />
          <Route path="/callback" element={<AuthCallbackPage />} />
          <Route path="/app/admin" element={<AdminLandingPage />} />
          <Route path="/app/user" element={<UserLandingPage />} />
        </Routes>
      </BrowserRouter>
    </AuthProvider>
  )
}

export default App
