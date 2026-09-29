import { QueryClientProvider } from '@tanstack/react-query'
import { BrowserRouter, Route, Routes } from 'react-router-dom'
import { AuthProvider } from 'react-oidc-context'
import { queryClient } from './api/queryClient'
import { AppLayout } from './app/AppLayout'
import { HomeRedirect, RequireAuth, RequirePermission } from './app/guards'
import { manageAny } from './app/session'
import { P } from './auth/permissions'
import { oidcConfig } from './auth/oidcConfig'
import { BookingsPage } from './features/bookings/BookingsPage'
import { FindSpacePage } from './features/find/FindSpacePage'
import { BuildingsPage, FloorsPage, SpacesPage, SpaceTypesPage } from './features/spaces/EstatePages'
import { UserDirectoryPage } from './features/users/UserDirectoryPage'
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
              <Route index element={<HomeRedirect />} />
              <Route path="find" element={<RequirePermission any={[P.Spaces.Default]}><FindSpacePage /></RequirePermission>} />
              <Route path="bookings" element={<RequirePermission any={[P.Bookings.Default]}><BookingsPage /></RequirePermission>} />
              <Route path="buildings" element={<RequirePermission any={manageAny(P.Buildings)}><BuildingsPage /></RequirePermission>} />
              <Route path="floors" element={<RequirePermission any={manageAny(P.Floors)}><FloorsPage /></RequirePermission>} />
              <Route path="spaces" element={<RequirePermission any={manageAny(P.Spaces)}><SpacesPage /></RequirePermission>} />
              <Route path="space-types" element={<RequirePermission any={manageAny(P.SpaceTypes)}><SpaceTypesPage /></RequirePermission>} />
              <Route path="users" element={<RequirePermission any={[P.Users.Default]}><UserDirectoryPage /></RequirePermission>} />
              <Route path="*" element={<HomeRedirect />} />
            </Route>
          </Routes>
        </BrowserRouter>
      </QueryClientProvider>
    </AuthProvider>
  )
}

export default App
