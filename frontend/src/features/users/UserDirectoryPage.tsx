import { useEffect, useState } from 'react'
import { useNavigate } from 'react-router-dom'
import { useSession } from '../../app/session'
import { initials, LoadError, Loading } from '../../components/bits'
import { Pagination } from '../../components/Pagination'
import { RowMenu, type RowMenuItem } from '../../components/RowMenu'
import { parseUtc } from '../../lib/dateUtils'
import { useDebouncedValue } from '../../lib/useDebouncedValue'
import { useScheduleStore } from '../../state/scheduleStore'
import { roleLabel, useUserDirectory, useUserRoles, type UserDirectoryItem, type UserDirectoryRole, type UserRole } from './api'
import { EMPTY_USER_FILTERS, UserDirectoryFilters, type UserDirectoryFilterValues } from './UserDirectoryFilters'
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

/* Roles, permissions and accounts are changed in ABP's administration site; this page only shows them. */
export function UserDirectoryPage() {
  const { userId, isAdmin } = useSession()
  const navigate = useNavigate()
  const [filters, setFilters] = useState<UserDirectoryFilterValues>(EMPTY_USER_FILTERS)
  const [page, setPage] = useState(1)
  const [pageSize, setPageSize] = useState(25)
  /* Only the typed search is debounced; dropdowns apply at once. */
  const search = useDebouncedValue(filters.search.trim())

  const roles = useUserRoles().data

  const q = useUserDirectory({
    page, pageSize, filter: search || undefined,
    role: filters.role || undefined,
    isActive: filters.status ? filters.status === 'active' : undefined,
    isLocked: filters.lock ? filters.lock === 'locked' : undefined,
  })
  const total = q.data?.totalCount ?? 0

  /* If the result shrinks (a filter, or accounts removed elsewhere), stay on a real page. */
  useEffect(() => {
    const pageCount = Math.max(1, Math.ceil(total / pageSize))
    if (q.data && page > pageCount) setPage(pageCount)
  }, [q.data, total, page, pageSize])

  const changeFilters = (v: UserDirectoryFilterValues) => { setFilters(v); setPage(1) }
  const changePageSize = (s: number) => { setPageSize(s); setPage(1) }
  const filtered = Object.values(filters).some((v) => v !== '')

  const menuItems = (u: UserDirectoryItem): RowMenuItem[] => isAdmin
    ? [{
        label: 'View bookings',
        onClick: () => { useScheduleStore.getState().patch('my', { userId: u.id }); navigate('/app/bookings') },
      }]
    : []

  return (
    <section className="user-dir">
      <div className="card" style={{ overflow: 'hidden' }}>
        <div className="card-head">
          <div>
            <h2 className="card-title">User directory</h2>
            <p className="card-sub">Portal accounts, their roles and access.</p>
          </div>
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
                <tr><th>User</th><th>Username</th><th>Role</th><th>Status</th><th>Lock</th>{isAdmin && <th style={{ textAlign: 'right' }}>Actions</th>}</tr>
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
                      {isAdmin && (
                        <td style={{ textAlign: 'right' }}>
                          <div style={{ display: 'inline-block' }}>{items.length > 0 && <RowMenu items={items} />}</div>
                        </td>
                      )}
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
    </section>
  )
}
