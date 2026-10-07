import { lazy, Suspense, type ReactNode } from 'react'
import { DirectionProvider } from '@radix-ui/react-direction'
import { useTranslation } from 'react-i18next'
import { QueryClientProvider } from '@tanstack/react-query'
import { BrowserRouter, Route, Routes } from 'react-router-dom'
import { AuthProvider } from 'react-oidc-context'
import { queryClient } from './api/queryClient'
import { AppLayout } from './app/AppLayout'
import { HomeRedirect, RequireAuth, RequirePermission } from './app/guards'
import { manageAny } from './app/session'
import { P } from './auth/permissions'
import { userManager } from './auth/oidcConfig'
import { ErrorBoundary } from './components/ErrorBoundary'
import { SplashScreen } from './components/SplashScreen'
import AuthCallbackPage from './pages/AuthCallbackPage'

/* Each page is downloaded the first time it is opened, so the first screen does not wait for all of them. */
const LoginPage = lazy(() => import('./pages/LoginPage'))
const RespondPage = lazy(() => import('./pages/RespondPage'))
const BookingsPage = lazy(() => import('./features/bookings/BookingsPage').then((m) => ({ default: m.BookingsPage })))
const FindSpacePage = lazy(() => import('./features/find/FindSpacePage').then((m) => ({ default: m.FindSpacePage })))
const MyProfilePage = lazy(() => import('./features/profile/MyProfilePage').then((m) => ({ default: m.MyProfilePage })))
const UserDirectoryPage = lazy(() => import('./features/users/UserDirectoryPage').then((m) => ({ default: m.UserDirectoryPage })))
const BuildingsPage = lazy(() => import('./features/spaces/EstatePages').then((m) => ({ default: m.BuildingsPage })))
const FloorsPage = lazy(() => import('./features/spaces/EstatePages').then((m) => ({ default: m.FloorsPage })))
const SpacesPage = lazy(() => import('./features/spaces/EstatePages').then((m) => ({ default: m.SpacesPage })))
const SpaceTypesPage = lazy(() => import('./features/spaces/EstatePages').then((m) => ({ default: m.SpaceTypesPage })))

/* The Radix pickers (dropdowns, calendar popovers) follow the language's direction too. */
function Direction({ children }: { children: ReactNode }) {
  const { i18n } = useTranslation()
  return <DirectionProvider dir={i18n.dir()}>{children}</DirectionProvider>
}

function App() {
  return (
    <Direction>
      <ErrorBoundary>
        <AuthProvider
          userManager={userManager}
          onSigninCallback={() => {
            window.history.replaceState({}, document.title, window.location.pathname)
          }}
        >
          <QueryClientProvider client={queryClient}>
            {/* Under /portal/ on the shared server; "/" in development. */}
            <BrowserRouter basename={import.meta.env.BASE_URL.replace(/\/$/, '')}>
              {/* The sign-in page while it downloads; pages inside the app shell show the shell's own "Loading…". */}
              <Suspense fallback={<SplashScreen />}>
                <Routes>
                  <Route path="/" element={<LoginPage />} />
                  <Route path="/callback" element={<AuthCallbackPage />} />
                  {/* Public: an outside guest answering an invitation from its private link (no sign-in). */}
                  <Route path="/respond/:token" element={<RespondPage />} />
                  <Route path="/app" element={<RequireAuth><AppLayout /></RequireAuth>}>
                    <Route index element={<HomeRedirect />} />
                    <Route path="find" element={<RequirePermission any={[P.Spaces.Default]}><FindSpacePage /></RequirePermission>} />
                    <Route path="bookings" element={<RequirePermission any={[P.Bookings.Default]}><BookingsPage /></RequirePermission>} />
                    <Route path="buildings" element={<RequirePermission any={manageAny(P.Buildings)}><BuildingsPage /></RequirePermission>} />
                    <Route path="floors" element={<RequirePermission any={manageAny(P.Floors)}><FloorsPage /></RequirePermission>} />
                    <Route path="spaces" element={<RequirePermission any={manageAny(P.Spaces)}><SpacesPage /></RequirePermission>} />
                    <Route path="space-types" element={<RequirePermission any={manageAny(P.SpaceTypes)}><SpaceTypesPage /></RequirePermission>} />
                    <Route path="users" element={<RequirePermission any={[P.Users.Default]}><UserDirectoryPage /></RequirePermission>} />
                    {/* Everyone signed in may see their own profile; no permission is needed. */}
                    <Route path="profile" element={<MyProfilePage />} />
                    <Route path="*" element={<HomeRedirect />} />
                  </Route>
                </Routes>
              </Suspense>
            </BrowserRouter>
          </QueryClientProvider>
        </AuthProvider>
      </ErrorBoundary>
    </Direction>
  )
}

export default App
