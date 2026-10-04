import { useMemo } from 'react'
import { useQuery } from '@tanstack/react-query'
import i18n from 'i18next'
import { useAuth } from 'react-oidc-context'
import { useApi } from '../../api/client'

/* The directory reads ABP's own Identity API (/api/identity/...): no Dixels service of its own.
 * ABP only searches and pages, so the page loads every account once (up to MAX_USERS) with each one's
 * roles, and searches, filters and pages them here. Fine for a company-sized list. */

/* A role's name, e.g. 'admin' or 'employee'; any role the server has. */
export type UserRole = string

export interface UserDirectoryRole {
  name: UserRole
  displayName: string
  /* ABP's admin role. */
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

/* The parts of ABP's IdentityUserDto / IdentityRoleDto the page uses. */
interface AbpUser {
  id: string
  userName: string
  name: string | null
  surname: string | null
  email: string
  isActive: boolean
  lockoutEnabled: boolean
  lockoutEnd: string | null
}
interface AbpRole { name: string }
interface ListResult<T> { items: T[] }

/* ABP's default cap on one page of results. */
const MAX_USERS = 1000
const ADMIN_ROLE = 'admin'

function useEnabled() {
  return !!useAuth().user?.access_token
}

/* Admin first, then by name. */
const byRole = (a: string, b: string) =>
  (a.toLowerCase() === ADMIN_ROLE ? -1 : b.toLowerCase() === ADMIN_ROLE ? 1 : a.localeCompare(b))

/* Every account with its roles, from ABP. */
function useAllUsers() {
  const api = useApi()
  return useQuery({
    queryKey: ['users', 'directory', 'all'],
    queryFn: async (): Promise<UserDirectoryItem[]> => {
      const users = (await api<ListResult<AbpUser>>('GET', '/api/identity/users', undefined,
        { Sorting: 'userName', MaxResultCount: MAX_USERS })).items
      const now = Date.now()
      return Promise.all(users.map(async (u) => {
        const roles = (await api<ListResult<AbpRole>>('GET', `/api/identity/users/${u.id}/roles`)).items
          .map((r) => r.name).sort(byRole)
        const locked = u.lockoutEnabled && !!u.lockoutEnd && new Date(u.lockoutEnd).getTime() > now
        return {
          id: u.id,
          name: [u.name, u.surname].filter(Boolean).join(' ') || u.userName,
          userName: u.userName,
          email: u.email,
          role: roles[0] ?? null,
          roles,
          isActive: u.isActive,
          lockoutEnd: locked ? u.lockoutEnd : null,
          isLocked: locked,
        }
      }))
    },
    enabled: useEnabled(),
  })
}

/* One page of the directory, searched and filtered in the browser (ABP's API can't filter by role,
 * status or lock). Shaped like a query so the page can treat it as one. */
export function useUserDirectory(q: UserDirectoryQuery) {
  const all = useAllUsers()
  const data = useMemo(() => {
    if (!all.data) return undefined
    const text = q.filter?.toLowerCase()
    const role = q.role?.toLowerCase()
    const matches = all.data
      .filter((u) => !text || [u.name, u.userName, u.email].some((v) => v.toLowerCase().includes(text)))
      .filter((u) => !role || u.roles.some((r) => r.toLowerCase() === role))
      .filter((u) => q.isActive === undefined || u.isActive === q.isActive)
      .filter((u) => q.isLocked === undefined || u.isLocked === q.isLocked)
      .sort((a, b) => a.name.localeCompare(b.name))
    const start = (q.page - 1) * q.pageSize
    return { totalCount: matches.length, items: matches.slice(start, start + q.pageSize) }
  }, [all.data, q.filter, q.role, q.isActive, q.isLocked, q.page, q.pageSize])
  return { data, isError: all.isError, error: all.error, refetch: all.refetch, isPlaceholderData: false }
}

/* Every role, admin first, from ABP. */
export function useUserRoles() {
  const api = useApi()
  return useQuery({
    queryKey: ['users', 'roles'],
    queryFn: async (): Promise<UserDirectoryRole[]> =>
      (await api<ListResult<AbpRole>>('GET', '/api/identity/roles/all')).items
        .map((r) => r.name).sort(byRole)
        .map((name) => ({ name, displayName: roleLabel(name), isAdmin: name.toLowerCase() === ADMIN_ROLE })),
    enabled: useEnabled(),
  })
}

/* The roles the portal seeds get a translated name; any other role is shown as the server names it. */
const ROLE_KEYS = { admin: 'roles.admin', employee: 'roles.employee' } as const

/* "front_desk" -> "Front desk": a readable label for a role name. */
export function roleLabel(role: UserRole, roles?: UserDirectoryRole[]) {
  const key = ROLE_KEYS[role.toLowerCase() as keyof typeof ROLE_KEYS]
  if (key) return i18n.t(key)
  const known = roles?.find((r) => r.name.toLowerCase() === role.toLowerCase())
  if (known) return known.displayName
  const words = role.replace(/[_-]/g, ' ').trim()
  return words ? words[0].toUpperCase() + words.slice(1) : role
}
