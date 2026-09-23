import { useAuth } from 'react-oidc-context'
import { useProfile } from '../api/hooks'
import { extractRoles } from '../auth/roles'

/* The token's role claim decides routing instantly; the server profile confirms it. */
export function useSession() {
  const auth = useAuth()
  const profile = useProfile()
  const roleAdmin = extractRoles(auth.user?.profile.role).includes('admin')
  const claims = auth.user?.profile
  return {
    userId: profile.data?.id ?? (claims?.sub as string | undefined) ?? '',
    name: profile.data?.name ?? (claims?.name as string | undefined) ?? (claims?.preferred_username as string | undefined) ?? 'You',
    teamName: profile.data?.teamName ?? null,
    teamId: profile.data?.teamId ?? null,
    isAdmin: profile.data?.isAdmin ?? roleAdmin,
    signOut: () => auth.signoutRedirect(),
  }
}
