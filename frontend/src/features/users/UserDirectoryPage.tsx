import { useEffect, useState } from 'react'
import { useNavigate } from 'react-router-dom'
import { errorText } from '../../api/client'
import { useSession } from '../../app/session'
import { P } from '../../auth/permissions'
import { initials, LoadError, Loading } from '../../components/bits'
import { Pagination } from '../../components/Pagination'
import { RowMenu, type RowMenuItem } from '../../components/RowMenu'
import { parseUtc } from '../../lib/dateUtils'
import { useDebouncedValue } from '../../lib/useDebouncedValue'
import { useScheduleStore } from '../../state/scheduleStore'
import { toast } from '../../state/toastStore'
import { roleLabel, useLockUser, useSetUserActive, useUnlockUser, useUserDirectory, useUserRoles, type UserDirectoryItem, type UserDirectoryRole, type UserRole } from './api'
import { ChangeRoleModal } from './ChangeRoleModal'
import { EMPTY_USER_FILTERS, UserDirectoryFilters, type UserDirectoryFilterValues } from './UserDirectoryFilters'
import { UserFormModal } from './UserFormModal'
import { UserPermissionsModal } from './UserPermissionsModal'
import './users.css'

const LOCK_LABEL = new Intl.DateTimeFormat('en-GB', { day: 'numeric', month: 'short', year: 'numeric', hour: '2-digit', minute: '2-digit', hour12: false, timeZone: 'UTC' })

/* "Locked until 3 Oct 2026 14:00" (UTC); a lockout with no end, or one years away, reads as just "Locked". */
function LockCell({ user }: { user: UserDirectoryItem }) {
  if (!user.isLocked) return <span className="lock-off">Not locked</span>
  const end = user.lockoutEnd ? parseUtc(user.lockoutEnd) : null
  const open = !end || Number.isNaN(end.getTime()) || end.getUTCFullYear() >= 9000
  return <span className="lock-on" title={open ? 'Until someone unlocks it' : 'Time in UTC'}>{open ? 'Locked' : `Locked until ${LOCK_LABEL.format(end)}`}</span>
}

const isAdminRole = (role: UserRole | null, roles?: UserDirectoryRole[]) =>
  !!role && (role.toLowerCase() === 'admin' || !!roles?.find((r) => r.name.toLowerCase() === role.toLowerCase())?.isAdmin)

/* Admin-type roles get the accent pill; every other role is grey. */
function RolePill({ role, roles }: { role: UserRole | null; roles?: UserDirectoryRole[] }) {
  if (!role) return <span className="lock-off">No role</span>
  return <span className={`pill ${isAdminRole(role, roles) ? 'pill-confirmed' : 'pill-ended'}`}><span className="dot" />{roleLabel(role, roles)}</span>
}

function StatusPill({ active }: { active: boolean }) {
  return active
    ? <span className="pill pill-active"><span className="dot" />Active</span>
    : <span className="pill pill-cancelled"><span className="dot" />Inactive</span>
}

const rejected = (e: unknown) => { const { code, message } = errorText(e); toast('err', 'Request rejected', message, code) }

export function UserDirectoryPage() {
  const { userId, isAdmin, can } = useSession()
  const navigate = useNavigate()
  const [filters, setFilters] = useState<UserDirectoryFilterValues>(EMPTY_USER_FILTERS)
  const [page, setPage] = useState(1)
  const [pageSize, setPageSize] = useState(25)
  const [adding, setAdding] = useState(false)
  const [permsFor, setPermsFor] = useState<UserDirectoryItem | null>(null)
  const [roleFor, setRoleFor] = useState<UserDirectoryItem | null>(null)
  /* Only the typed search is debounced; dropdowns apply at once. */
  const search = useDebouncedValue(filters.search.trim())

  const roles = useUserRoles().data
  const lock = useLockUser()
  const unlock = useUnlockUser()
  const setActive = useSetUserActive()

  const q = useUserDirectory({
    page, pageSize, filter: search || undefined,
    role: filters.role || undefined,
    isActive: filters.status ? filters.status === 'active' : undefined,
    isLocked: filters.lock ? filters.lock === 'locked' : undefined,
  })
  const total = q.data?.totalCount ?? 0

  /* If the result shrinks (a filter, or the last row of the last page was changed), stay on a real page. */
  useEffect(() => {
    const pageCount = Math.max(1, Math.ceil(total / pageSize))
    if (q.data && page > pageCount) setPage(pageCount)
  }, [q.data, total, page, pageSize])

  const changeFilters = (v: UserDirectoryFilterValues) => { setFilters(v); setPage(1) }
  const changePageSize = (s: number) => { setPageSize(s); setPage(1) }
  const filtered = Object.values(filters).some((v) => v !== '')

  const canEdit = can(P.Users.Edit)
  const canAdd = can(P.Users.Create)
  const canPermissions = can(P.Users.ManagePermissions)

  /* Locks until someone unlocks the account. */
  const lockUser = (u: UserDirectoryItem) => {
    if (!window.confirm(`Lock ${u.name}? They won't be able to sign in until you unlock them.`)) return
    lock.mutateAsync({ id: u.id, until: null }).then(
      () => toast('ok', 'User locked', `${u.name} stays locked until unlocked.`), rejected)
  }

  const menuItems = (u: UserDirectoryItem): RowMenuItem[] => {
    const self = u.id === userId
    const items: RowMenuItem[] = []
    if (canEdit && !self) {
      items.push({ label: 'Change role…', onClick: () => setRoleFor(u) })
      if (u.isLocked) {
        items.push({
          label: 'Unlock',
          onClick: () => unlock.mutateAsync(u.id).then(() => toast('ok', 'User unlocked', `${u.name} can sign in again.`), rejected),
        })
      } else {
        items.push({ label: 'Lock', onClick: () => lockUser(u) })
      }
      items.push(u.isActive
        ? {
            label: 'Deactivate',
            onClick: () => {
              if (!window.confirm(`Deactivate ${u.name}? They can no longer sign in until the account is activated again.`)) return
              setActive.mutateAsync({ id: u.id, isActive: false }).then(() => toast('ok', 'User deactivated', `${u.name} can no longer sign in.`), rejected)
            },
          }
        : {
            label: 'Activate',
            onClick: () => setActive.mutateAsync({ id: u.id, isActive: true }).then(() => toast('ok', 'User activated', `${u.name} can sign in again.`), rejected),
          })
    }
    if (isAdmin) {
      items.push({
        label: 'View bookings',
        onClick: () => { useScheduleStore.getState().patch('my', { userId: u.id }); navigate('/app/bookings') },
      })
    }
    if (canPermissions) items.push({ label: 'Permissions…', onClick: () => setPermsFor(u) })
    return items
  }

  return (
    <section className="user-dir">
      <div className="card" style={{ overflow: 'hidden' }}>
        <div className="card-head">
          <div><h2 className="card-title">User directory</h2><p className="card-sub">Manage portal accounts, their roles and access</p></div>
          {canAdd && <button type="button" className="btn btn-accent btn-sm" onClick={() => setAdding(true)}>Add user</button>}
        </div>
        <UserDirectoryFilters value={filters} onChange={changeFilters} />
        {q.isError ? <LoadError what="the users" error={q.error} onRetry={() => q.refetch()} /> : !q.data ? <Loading /> : !q.data.items.length ? (
          <p className="empty-note">
            {filtered ? 'No users match these filters.' : 'No users yet.'}
            {filtered && <button type="button" className="btn btn-sm" style={{ marginLeft: 10 }} onClick={() => changeFilters(EMPTY_USER_FILTERS)}>Clear filters</button>}
          </p>
        ) : (
          <div className="table-scroll">
            <table className="grid" style={{ opacity: q.isPlaceholderData ? 0.6 : 1 }}>
              <thead>
                <tr><th>User</th><th>Username</th><th>Role</th><th>Status</th><th>Lock</th><th style={{ textAlign: 'right' }}>Actions</th></tr>
              </thead>
              <tbody>
                {q.data.items.map((u) => {
                  const items = menuItems(u)
                  return (
                    <tr key={u.id}>
                      <td>
                        <div className="user-cell">
                          <span className={`avatar${isAdminRole(u.role, roles) ? ' admin' : ''}`} aria-hidden="true">{initials(u.name || u.userName)}</span>
                          <div style={{ minWidth: 0 }}>
                            <div style={{ fontWeight: 600 }}>{u.name || u.userName}{u.id === userId && <span className="lock-off" style={{ fontWeight: 400 }}> (you)</span>}</div>
                            <div className="user-email">{u.email}</div>
                          </div>
                        </div>
                      </td>
                      <td className="mono" style={{ fontSize: 12 }}>{u.userName}</td>
                      <td><RolePill role={u.role} roles={roles} /></td>
                      <td><StatusPill active={u.isActive} /></td>
                      <td><LockCell user={u} /></td>
                      <td style={{ textAlign: 'right' }}>
                        <div style={{ display: 'inline-block' }}>{items.length > 0 && <RowMenu items={items} />}</div>
                      </td>
                    </tr>
                  )
                })}
              </tbody>
            </table>
          </div>
        )}
        {q.data && total > 0 && (
          <Pagination page={page} pageSize={pageSize} total={total} noun="users" onPage={setPage} onPageSize={changePageSize} />
        )}
      </div>
      {adding && <UserFormModal onClose={() => setAdding(false)} />}
      {roleFor && <ChangeRoleModal user={roleFor} onClose={() => setRoleFor(null)} />}
      {permsFor && (
        <UserPermissionsModal userId={permsFor.id} userName={permsFor.name || permsFor.userName}
          readOnly={permsFor.id === userId} onClose={() => setPermsFor(null)} />
      )}
    </section>
  )
}
