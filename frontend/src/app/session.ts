import { useAuth } from 'react-oidc-context'
import { useCurrentUser, useGrantedPolicies } from '../api/hooks'
import { displayName } from '../api/types'
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

/* Who is signed in and what they may do, straight from ABP: its current user and its granted permissions.
 * Every decision asks for the permission it needs through can(); there is no separate "admin" flag. */
export function useSession() {
  const auth = useAuth()
  const user = useCurrentUser().data
  const policies = useGrantedPolicies()
  const granted = policies.data
  const claims = auth.user?.profile
  /* Until the server's grants arrive, everyone is assumed to hold the employee set. */
  const can: Can = (name) => (granted ? !!granted[name] : DEFAULT_EMPLOYEE_POLICIES.has(name))
  return {
    userId: user?.id ?? (claims?.sub as string | undefined) ?? '',
    name: (user && displayName(user.name, user.surName, user.userName)) || (claims?.name as string | undefined) || (claims?.preferred_username as string | undefined) || 'You',
    /* The roles stored for this user; the token's role claim until ABP's configuration arrives. */
    roles: user?.roles ?? extractRoles(claims?.role),
    can,
    /* Bookings.ManageAll: sees, books for and changes everyone's bookings, not only their own. */
    managesAll: can(P.Bookings.ManageAll),
    /* A failed load counts as loaded, so the guards fall back to the defaults above instead of waiting forever. */
    permissionsLoaded: !!granted || policies.isError,
    home: homePath(can),
    signOut: () => auth.signoutRedirect(),
  }
}
