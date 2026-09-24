import { QueryClientProvider } from '@tanstack/react-query'
import { ReactQueryDevtools } from '@tanstack/react-query-devtools'
import { BrowserRouter, Navigate, Route, Routes } from 'react-router-dom'
import { AuthProvider } from 'react-oidc-context'
import { queryClient } from './api/queryClient'
import { AppLayout } from './app/AppLayout'
import { RequireAdmin, RequireAuth } from './app/guards'
import { oidcConfig } from './auth/oidcConfig'
import { BookingsPage } from './features/bookings/BookingsPage'
import { DashboardPage as AppDashboardPage } from './features/dashboard/DashboardPage'
import { FindSpacePage } from './features/find/FindSpacePage'
import { BuildingsPage, FloorsPage, SpacesPage, SpaceTypesPage } from './features/spaces/EstatePages'
import AuthCallbackPage from './pages/AuthCallbackPage'
import LoginPage from './pages/LoginPage'

function App() {
  return (
    <AuthProvider
      {...oidcConfig}
      onSigninCallback={() => {
        window.history.replaceState({}, document.title, window.location.pathname)
      }}
    >
      <QueryClientProvider client={queryClient}>
        <BrowserRouter>
          <Routes>
            <Route path="/" element={<LoginPage />} />
            <Route path="/callback" element={<AuthCallbackPage />} />
            <Route path="/app" element={<RequireAuth><AppLayout /></RequireAuth>}>
              <Route index element={<Navigate to="find" replace />} />
              <Route path="find" element={<FindSpacePage />} />
              <Route path="dashboard" element={<AppDashboardPage />} />
              <Route path="bookings" element={<BookingsPage />} />
              <Route path="buildings" element={<RequireAdmin><BuildingsPage /></RequireAdmin>} />
              <Route path="floors" element={<RequireAdmin><FloorsPage /></RequireAdmin>} />
              <Route path="spaces" element={<RequireAdmin><SpacesPage /></RequireAdmin>} />
              <Route path="space-types" element={<RequireAdmin><SpaceTypesPage /></RequireAdmin>} />
              <Route path="*" element={<Navigate to="find" replace />} />
            </Route>
          </Routes>
        </BrowserRouter>
        {import.meta.env.DEV && <ReactQueryDevtools buttonPosition="bottom-right" />}
      </QueryClientProvider>
    </AuthProvider>
  )
}

export default App
