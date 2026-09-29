import { keepPreviousData, useQuery } from '@tanstack/react-query'
import { useAuth } from 'react-oidc-context'
import { useApi } from '../../api/client'

/* The ABP server, which also hosts the administration site (Identity users and roles); same default as api/client.ts. */
export const ADMIN_SITE_URL = import.meta.env.VITE_API_URL ?? 'https://localhost:44393'

/* ─── Shapes (camelCase, as the server sends them) ─── */

/* A role's name, e.g. 'admin' or 'employee'; any role the server has. */
export type UserRole = string

export interface UserDirectoryRole {
  name: UserRole
  displayName: string
  /* ABP's admin role, or a role that can manage everyone's bookings. */
  isAdmin: boolean
}

export interface UserDirectoryItem {
  id: string
  name: string
  userName: string
  email: string
  /* The main role (admin wins), or null when the user has none. */
  role: UserRole | null
  roles: UserRole[]
  isActive: boolean
  /* UTC ISO instant, or null when the account has no lockout. */
  lockoutEnd: string | null
  isLocked: boolean
}

export interface UserDirectoryQuery {
  page: number
  pageSize: number
  filter?: string
  role?: UserRole
  isActive?: boolean
  isLocked?: boolean
}

interface ListResult<T> { items: T[] }
interface PagedResult<T> { totalCount: number; items: T[] }

const BASE = '/api/app/user-directory'

function useEnabled() {
  return !!useAuth().user?.access_token
}

/* ─── Queries ─── */
export function useUserDirectory(q: UserDirectoryQuery) {
  const api = useApi()
  return useQuery({
    queryKey: ['users', 'directory', q],
    queryFn: () => api<PagedResult<UserDirectoryItem>>('GET', BASE, undefined, {
      Filter: q.filter,
      Role: q.role,
      IsActive: q.isActive,
      IsLocked: q.isLocked,
      SkipCount: (q.page - 1) * q.pageSize,
      MaxResultCount: q.pageSize,
    }),
    enabled: useEnabled(),
    /* Keep showing the current page while the next one loads, instead of flashing "Loading…". */
    placeholderData: keepPreviousData,
  })
}

/* Every role, admin first. */
export function useUserRoles() {
  const api = useApi()
  return useQuery({
    queryKey: ['users', 'roles'],
    queryFn: async () => (await api<ListResult<UserDirectoryRole>>('GET', `${BASE}/roles`)).items,
    enabled: useEnabled(),
  })
}

/* "front_desk" -> "Front desk": for a role the roles list doesn't (yet) have. */
export function roleLabel(role: UserRole, roles?: UserDirectoryRole[]) {
  const known = roles?.find((r) => r.name.toLowerCase() === role.toLowerCase())
  if (known) return known.displayName
  const words = role.replace(/[_-]/g, ' ').trim()
  return words ? words[0].toUpperCase() + words.slice(1) : role
}
