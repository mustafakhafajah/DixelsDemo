import { useAuth } from 'react-oidc-context'
import i18n from 'i18next'
import { useGrantedPolicies, useProfile } from '../api/hooks'
import { DEFAULT_EMPLOYEE_POLICIES, P } from '../auth/permissions'
import { extractRoles } from '../auth/roles'

type Can = (name: string) => boolean

/* Space management pages, in sidebar order, with the permission area each one manages.
 * label is a translation key, translated where it is shown. */
export const ESTATE_PAGES = [
  { path: '/app/buildings', label: 'nav.buildings', area: P.Buildings },
  { path: '/app/floors', label: 'nav.floors', area: P.Floors },
  { path: '/app/spaces', label: 'nav.spaces', area: P.Spaces },
  { path: '/app/space-types', label: 'nav.spaceTypes', area: P.SpaceTypes },
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

/* What the signed-in user may do is always a named permission: can(P.Bookings.ViewAll), can(P.Bookings.EditAll), ...
 * There is no "admin" flag - ABP's grants decide, whatever role they come from. */
export function useSession() {
  const auth = useAuth()
  const profile = useProfile()
  const policies = useGrantedPolicies()
  const granted = policies.data
  const claims = auth.user?.profile
  /* The roles stored for this user; the token's role claim until the profile arrives. */
  const roles = profile.data?.roles ?? extractRoles(auth.user?.profile.role)
  /* Until the server's grants arrive, everyone is given the employee set; the pages wait for the real grants
   * (RequirePermission), and the server checks every call against them anyway. */
  const can: Can = (name) => (granted ? !!granted[name] : DEFAULT_EMPLOYEE_POLICIES.has(name))
  return {
    userId: profile.data?.id ?? (claims?.sub as string | undefined) ?? '',
    name: profile.data?.name ?? (claims?.name as string | undefined) ?? (claims?.preferred_username as string | undefined) ?? i18n.t('common.you'),
    roles,
    can,
    /* A failed load counts as loaded, so the guards fall back to the defaults above instead of waiting forever. */
    permissionsLoaded: !!granted || policies.isError,
    home: homePath(can),
    signOut: () => auth.signoutRedirect(),
  }
}
