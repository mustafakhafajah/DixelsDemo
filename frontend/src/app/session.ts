import { useAuth } from 'react-oidc-context'
import { useGrantedPolicies, useProfile } from '../api/hooks'
import { DEFAULT_EMPLOYEE_POLICIES, P } from '../auth/permissions'
import { extractRoles } from '../auth/roles'

type Can = (name: string) => boolean

/* Space management pages, in sidebar order, with the permission area each one manages. */
export const ESTATE_PAGES = [
  { path: '/app/buildings', label: 'Buildings', area: P.Buildings },
  { path: '/app/floors', label: 'Floors', area: P.Floors },
  { path: '/app/spaces', label: 'Spaces', area: P.Spaces },
  { path: '/app/space-types', label: 'Space types', area: P.SpaceTypes },
] as const

/* The permissions that open a management page: anyone who can change something on it. */
export const manageAny = (area: { Create: string; Edit: string; Delete: string }) => [area.Create, area.Edit, area.Delete]

/* The first page this user can open; where sign-in and blocked routes send them. */
function homePath(can: Can) {
  if (can(P.Bookings.Default)) return '/app/bookings'
  if (can(P.Spaces.Default)) return '/app/find'
  const estate = ESTATE_PAGES.find((p) => manageAny(p.area).some(can))?.path
  if (estate) return estate
  return can(P.Users.Default) ? '/app/users' : '/app/find'
}

/* The token's role claim decides routing instantly; the server profile confirms it. */
export function useSession() {
  const auth = useAuth()
  const profile = useProfile()
  const policies = useGrantedPolicies()
  const granted = policies.data
  const roleAdmin = extractRoles(auth.user?.profile.role).includes('admin')
  const claims = auth.user?.profile
  const isAdmin = profile.data?.isAdmin ?? roleAdmin
  /* The roles stored for this user; the token's role claim until the profile arrives. */
  const roles = profile.data?.roles ?? extractRoles(auth.user?.profile.role)
  /* Until the server's grants arrive, admins are assumed to hold everything and others the employee set. */
  const can: Can = (name) => (granted ? !!granted[name] : isAdmin || DEFAULT_EMPLOYEE_POLICIES.has(name))
  return {
    userId: profile.data?.id ?? (claims?.sub as string | undefined) ?? '',
    name: profile.data?.name ?? (claims?.name as string | undefined) ?? (claims?.preferred_username as string | undefined) ?? 'You',
    isAdmin,
    roles,
    can,
    /* A failed load counts as loaded, so the guards fall back to the defaults above instead of waiting forever. */
    permissionsLoaded: !!granted || policies.isError,
    home: homePath(can),
    signOut: () => auth.signoutRedirect(),
  }
}
