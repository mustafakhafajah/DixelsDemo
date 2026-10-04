import { keepPreviousData, useQuery } from '@tanstack/react-query'
import i18n from 'i18next'
import { useAuth } from 'react-oidc-context'
import { useApi } from '../../api/client'

/* Users are managed in ABP's own Users page; the directory only reads them. Each filter and page change is one
 * request to /api/app/user-directory, which runs the search and the role / status / lock filters in the
 * database through ABP's user repository (ABP's public /api/identity/users only takes a search text). */

/* A role's name, e.g. 'admin' or 'employee'; any role the server has. */
export type UserRole = string

export interface UserDirectoryRole {
  id: string
  name: UserRole
  displayName: string
}

export interface UserDirectoryItem {
  id: string
  name: string
  userName: string
  email: string
  /* The first role by name, or null when the user has none. */
  role: UserRole | null
  roles: UserRole[]
  isActive: boolean
  /* UTC ISO instant, or null when the account is not locked. */
  lockoutEnd: string | null
  isLocked: boolean
}

export interface UserDirectoryQuery {
  page: number
  pageSize: number
  filter?: string
  roleId?: string
  isActive?: boolean
  isLocked?: boolean
}

/* What /api/app/user-directory and ABP's /api/identity/roles/all send. */
interface UserDirectoryDto {
  id: string
  name: string
  userName: string
  email: string
  isActive: boolean
  isLocked: boolean
  lockoutEnd: string | null
  roles: string[]
}
interface AbpRole { id: string; name: string }
interface ListResult<T> { items: T[] }
interface PagedResult<T> extends ListResult<T> { totalCount: number }

function useEnabled() {
  return !!useAuth().user?.access_token
}

/* Roles by name; no role is special. */
const byRole = (a: string, b: string) => a.localeCompare(b)

/* One page of the directory, searched and filtered on the server. */
export function useUserDirectory(q: UserDirectoryQuery) {
  const api = useApi()
  return useQuery({
    queryKey: ['users', 'directory', q],
    queryFn: async () => {
      const page = await api<PagedResult<UserDirectoryDto>>('GET', '/api/app/user-directory', undefined, {
        Filter: q.filter,
        RoleId: q.roleId,
        IsActive: q.isActive === undefined ? undefined : String(q.isActive),
        IsLocked: q.isLocked === undefined ? undefined : String(q.isLocked),
        SkipCount: String((q.page - 1) * q.pageSize),
        MaxResultCount: String(q.pageSize),
      })
      return {
        totalCount: page.totalCount,
        items: page.items.map((u): UserDirectoryItem => {
          const roles = [...u.roles].sort(byRole)
          return { ...u, name: u.name || u.userName, role: roles[0] ?? null, roles }
        }),
      }
    },
    enabled: useEnabled(),
    /* Keep the current page on screen while a changed filter or page loads, instead of flashing "Loading…". */
    placeholderData: keepPreviousData,
  })
}

/* Every role, by name, from ABP. */
export function useUserRoles() {
  const api = useApi()
  return useQuery({
    queryKey: ['users', 'roles'],
    queryFn: async (): Promise<UserDirectoryRole[]> =>
      (await api<ListResult<AbpRole>>('GET', '/api/identity/roles/all')).items
        .sort((a, b) => byRole(a.name, b.name))
        .map((r) => ({ id: r.id, name: r.name, displayName: roleLabel(r.name) })),
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
