import { keepPreviousData, useMutation, useQuery, useQueryClient } from '@tanstack/react-query'
import { useAuth } from 'react-oidc-context'
import { useApi } from '../../api/client'

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

export interface CreateUserInput {
  name: string
  email: string
  userName?: string
  password: string
  role: UserRole
}

/* role: from one of the user's roles · user: added for this user · blocked: turned off for this user · none: not granted. */
export type PermissionSource = 'role' | 'user' | 'blocked' | 'none'

export interface UserPermission {
  name: string
  displayName: string
  parentName: string | null
  groupName: string
  isGranted: boolean
  fromRole: boolean
  source: PermissionSource
}

interface ListResult<T> { items: T[] }
interface PagedResult<T> { totalCount: number; items: T[] }

const BASE = '/api/app/user-directory'

function useEnabled() {
  return !!useAuth().user?.access_token
}

/* ['users'] also refreshes the schedule's user picker, so a new or renamed account shows there too. */
function useInvalidateUsers() {
  const qc = useQueryClient()
  return () => qc.invalidateQueries({ queryKey: ['users'] })
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

/* Every role a user can be given, admin first. */
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

export function useUserPermissions(id: string | null) {
  const api = useApi()
  const ok = useEnabled()
  return useQuery({
    queryKey: ['users', 'permissions', id],
    queryFn: async () => (await api<ListResult<UserPermission>>('GET', `${BASE}/${id}/permissions`)).items,
    enabled: ok && !!id,
  })
}

/* ─── Mutations ─── */

export function useCreateUser() {
  const api = useApi()
  const invalidate = useInvalidateUsers()
  return useMutation({
    mutationFn: (input: CreateUserInput) => api<UserDirectoryItem>('POST', BASE, input),
    onSuccess: () => invalidate(),
  })
}

export function useSetUserRole() {
  const api = useApi()
  const invalidate = useInvalidateUsers()
  return useMutation({
    mutationFn: ({ id, role }: { id: string; role: UserRole }) => api<UserDirectoryItem>('POST', `${BASE}/${id}/role`, { role }),
    onSettled: () => invalidate(),
  })
}

/* until: UTC ISO instant, or null to keep the account locked until someone unlocks it. */
export function useLockUser() {
  const api = useApi()
  const invalidate = useInvalidateUsers()
  return useMutation({
    mutationFn: ({ id, until }: { id: string; until: string | null }) => api<UserDirectoryItem>('POST', `${BASE}/${id}/lock`, { until }),
    onSettled: () => invalidate(),
  })
}

export function useUnlockUser() {
  const api = useApi()
  const invalidate = useInvalidateUsers()
  return useMutation({
    mutationFn: (id: string) => api<UserDirectoryItem>('POST', `${BASE}/${id}/unlock`),
    onSettled: () => invalidate(),
  })
}

export function useSetUserActive() {
  const api = useApi()
  const invalidate = useInvalidateUsers()
  return useMutation({
    mutationFn: ({ id, isActive }: { id: string; isActive: boolean }) => api<UserDirectoryItem>('POST', `${BASE}/${id}/active`, { isActive }),
    onSettled: () => invalidate(),
  })
}

/* Sends only the changed items. Refreshes ['permissions'] too, in case it is the signed-in user's own. */
export function useSaveUserPermissions() {
  const api = useApi()
  const qc = useQueryClient()
  return useMutation({
    mutationFn: async ({ id, permissions }: { id: string; permissions: { name: string; isGranted: boolean }[] }) =>
      (await api<ListResult<UserPermission>>('PUT', `${BASE}/${id}/permissions`, { permissions })).items,
    onSuccess: () => Promise.all([
      qc.invalidateQueries({ queryKey: ['users'] }),
      qc.invalidateQueries({ queryKey: ['permissions'] }),
    ]),
  })
}
