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
import { useLockUser, useSetUserActive, useSetUserRole, useUnlockUser, useUserDirectory, type UserDirectoryItem, type UserRole } from './api'
import { EMPTY_USER_FILTERS, UserDirectoryFilters, type UserDirectoryFilterValues } from './UserDirectoryFilters'
import { UserFormModal } from './UserFormModal'
import { UserPermissionsModal } from './UserPermissionsModal'
import './users.css'

const LOCK_LABEL = new Intl.DateTimeFormat('en-GB', { day: 'numeric', month: 'short', year: 'numeric', hour: '2-digit', minute: '2-digit', hour12: false, timeZone: 'UTC' })
const DAY_MS = 24 * 60 * 60_000

/* "Locked until 3 Oct 2026 14:00" (UTC); a lockout with no end, or one years away, reads as just "Locked". */
function LockCell({ user }: { user: UserDirectoryItem }) {
  if (!user.isLocked) return <span className="lock-off">Not locked</span>
  const end = user.lockoutEnd ? parseUtc(user.lockoutEnd) : null
  const open = !end || Number.isNaN(end.getTime()) || end.getUTCFullYear() >= 9000
  return <span className="lock-on" title={open ? 'Until someone unlocks it' : 'Time in UTC'}>{open ? 'Locked' : `Locked until ${LOCK_LABEL.format(end)}`}</span>
}

function RolePill({ role }: { role: UserRole | null }) {
  if (role === 'admin') return <span className="pill pill-confirmed"><span className="dot" />Admin</span>
  if (role === 'employee') return <span className="pill pill-ended"><span className="dot" />Employee</span>
  return <span className="lock-off">No role</span>
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
  /* Only the typed search is debounced; dropdowns apply at once. */
  const search = useDebouncedValue(filters.search.trim())

  const setRole = useSetUserRole()
  const lock = useLockUser()
  const unlock = useUnlockUser()
  const setActive = useSetUserActive()

  const q = useUserDirectory({
    page, pageSize, filter: search || undefined,
    role: (filters.role || undefined) as UserRole | undefined,
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

  const lockFor = (u: UserDirectoryItem, days: number | null) => {
    if (days == null && !window.confirm(`Lock ${u.name} until someone unlocks the account? They are signed out and cannot sign in.`)) return
    const until = days == null ? null : new Date(Date.now() + days * DAY_MS).toISOString()
    lock.mutateAsync({ id: u.id, until }).then(
      () => toast('ok', 'User locked', days == null ? `${u.name} stays locked until unlocked.` : `${u.name} is locked for ${days === 1 ? '1 day' : `${days} days`}.`),
      rejected)
  }

  const menuItems = (u: UserDirectoryItem): RowMenuItem[] => {
    const self = u.id === userId
    const items: RowMenuItem[] = []
    if (canEdit && !self) {
      const nextRole: UserRole = u.role === 'admin' ? 'employee' : 'admin'
      items.push({
        label: nextRole === 'admin' ? 'Make admin' : 'Make employee',
        onClick: () => setRole.mutateAsync({ id: u.id, role: nextRole }).then(
          () => toast('ok', 'Role changed', `${u.name} is now ${nextRole === 'admin' ? 'an admin' : 'an employee'}.`), rejected),
      })
      if (u.isLocked) {
        items.push({
          label: 'Unlock',
          onClick: () => unlock.mutateAsync(u.id).then(() => toast('ok', 'User unlocked', `${u.name} can sign in again.`), rejected),
        })
      } else {
        items.push(
          { label: 'Lock for 1 day', onClick: () => lockFor(u, 1) },
          { label: 'Lock for 7 days', onClick: () => lockFor(u, 7) },
          { label: 'Lock until unlocked', onClick: () => lockFor(u, null) },
        )
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
                          <span className={`avatar${u.role === 'admin' ? ' admin' : ''}`} aria-hidden="true">{initials(u.name || u.userName)}</span>
                          <div style={{ minWidth: 0 }}>
                            <div style={{ fontWeight: 600 }}>{u.name || u.userName}{u.id === userId && <span className="lock-off" style={{ fontWeight: 400 }}> (you)</span>}</div>
                            <div className="user-email">{u.email}</div>
                          </div>
                        </div>
                      </td>
                      <td className="mono" style={{ fontSize: 12 }}>{u.userName}</td>
                      <td><RolePill role={u.role} /></td>
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
      {permsFor && (
        <UserPermissionsModal userId={permsFor.id} userName={permsFor.name || permsFor.userName}
          readOnly={permsFor.id === userId} onClose={() => setPermsFor(null)} />
      )}
    </section>
  )
}
