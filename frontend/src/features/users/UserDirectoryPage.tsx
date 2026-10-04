import { useState } from 'react'
import { useTranslation } from 'react-i18next'
import { useNavigate } from 'react-router-dom'
import { useSession } from '../../app/session'
import { P } from '../../auth/permissions'
import { initials, LoadError, Loading } from '../../components/bits'
import { EmptyState } from '../../components/EmptyState'
import { Pagination } from '../../components/Pagination'
import { RowMenu, type RowMenuItem } from '../../components/RowMenu'
import { formatDate, parseUtc } from '../../lib/dateUtils'
import { useDebouncedValue } from '../../lib/useDebouncedValue'
import { useServerPaging, useStayOnRealPage } from '../../lib/useServerPaging'
import { useScheduleStore } from '../../state/scheduleStore'
import { roleLabel, useUserDirectory, useUserRoles, type UserDirectoryItem, type UserDirectoryRole, type UserRole } from './api'
import { EMPTY_USER_FILTERS, UserDirectoryFilters, type UserDirectoryFilterValues } from './UserDirectoryFilters'
import './users.css'

const LOCK_LABEL: Intl.DateTimeFormatOptions = { day: 'numeric', month: 'short', year: 'numeric', hour: '2-digit', minute: '2-digit', hour12: false }

/* "Locked until 3 Oct 2026 14:00" (UTC); a lockout with no end, or one years away, reads as just "Locked". */
function LockCell({ user }: { user: UserDirectoryItem }) {
  const { t } = useTranslation()
  if (!user.isLocked) return <span className="lock-off">{t('users.notLocked')}</span>
  const end = user.lockoutEnd ? parseUtc(user.lockoutEnd) : null
  const open = !end || Number.isNaN(end.getTime()) || end.getUTCFullYear() >= 9000
  return <span className="lock-on" title={open ? t('users.untilUnlocked') : t('users.timeInUtc')}>{open ? t('users.locked') : t('users.lockedUntil', { date: formatDate(end, LOCK_LABEL) })}</span>
}

/* Every role looks the same; the pill only names it. */
function RolePill({ role, roles }: { role: UserRole | null; roles?: UserDirectoryRole[] }) {
  const { t } = useTranslation()
  if (!role) return <span className="lock-off">{t('nav.noRole')}</span>
  return <span className="pill pill-ended"><span className="dot" />{roleLabel(role, roles)}</span>
}

function StatusPill({ active }: { active: boolean }) {
  const { t } = useTranslation()
  return active
    ? <span className="pill pill-active"><span className="dot" />{t('users.active')}</span>
    : <span className="pill pill-cancelled"><span className="dot" />{t('users.inactive')}</span>
}

/* Roles, permissions and accounts are changed in ABP's administration site; this page only shows them. */
export function UserDirectoryPage() {
  const { t } = useTranslation()
  const { userId, can } = useSession()
  /* "View bookings" opens the Schedule on that person: reading their bookings, so Bookings.ViewAll. */
  const seesBookings = can(P.Bookings.ViewAll)
  const navigate = useNavigate()
  const [filters, setFilters] = useState<UserDirectoryFilterValues>(EMPTY_USER_FILTERS)
  const paging = useServerPaging()
  const { page, pageSize, setPage } = paging
  /* Only the typed search is debounced; dropdowns apply at once. Every filter and page goes to the server. */
  const search = useDebouncedValue(filters.search.trim())

  const roles = useUserRoles().data

  const q = useUserDirectory({
    page, pageSize, filter: search || undefined,
    roleId: filters.role || undefined,
    isActive: filters.status ? filters.status === 'active' : undefined,
    isLocked: filters.lock ? filters.lock === 'locked' : undefined,
  })
  const total = q.data?.totalCount ?? 0
  useStayOnRealPage(paging, q.data?.totalCount)

  const changeFilters = (v: UserDirectoryFilterValues) => { setFilters(v); setPage(1) }
  const changePageSize = paging.setPageSize
  const filtered = Object.values(filters).some((v) => v !== '')

  const menuItems = (u: UserDirectoryItem): RowMenuItem[] => seesBookings
    ? [{
        label: t('users.viewBookings'),
        onClick: () => { useScheduleStore.getState().patch('my', { userId: u.id }); navigate('/app/bookings') },
      }]
    : []

  return (
    <section className="user-dir">
      <div className="card" style={{ overflow: 'hidden' }}>
        <UserDirectoryFilters value={filters} onChange={changeFilters} />
        {q.isError ? <LoadError what={t('load.users')} error={q.error} onRetry={() => q.refetch()} /> : !q.data ? <Loading /> : !q.data.items.length ? (
          filtered
            ? <EmptyState art="user" title={t('empty.usersFiltered.title')} text={t('empty.usersFiltered.text')}
                action={{ label: t('common.clearFilters'), onClick: () => changeFilters(EMPTY_USER_FILTERS) }} />
            : <EmptyState art="user" title={t('empty.users.title')} text={t('empty.users.text')} />
        ) : (
          <div className="table-scroll">
            <table className="grid" style={{ opacity: q.isPlaceholderData ? 0.6 : 1 }}>
              <thead>
                <tr><th>{t('users.col.user')}</th><th>{t('users.col.userName')}</th><th>{t('users.col.role')}</th><th>{t('users.col.status')}</th><th>{t('users.col.lock')}</th>{seesBookings && <th style={{ textAlign: 'end' }}>{t('common.actions')}</th>}</tr>
              </thead>
              <tbody>
                {q.data.items.map((u) => {
                  const items = menuItems(u)
                  return (
                    <tr key={u.id}>
                      <td>
                        <div className="user-cell">
                          <span className="avatar" aria-hidden="true">{initials(u.name || u.userName)}</span>
                          <div style={{ minWidth: 0 }}>
                            <div style={{ fontWeight: 600 }}><bdi>{u.name || u.userName}</bdi>{u.id === userId && <span className="lock-off" style={{ fontWeight: 400 }}> {t('users.youMark')}</span>}</div>
                            <div className="user-email"><bdi>{u.email}</bdi></div>
                          </div>
                        </div>
                      </td>
                      <td className="mono" style={{ fontSize: 12 }}><bdi>{u.userName}</bdi></td>
                      <td><RolePill role={u.role} roles={roles} /></td>
                      <td><StatusPill active={u.isActive} /></td>
                      <td><LockCell user={u} /></td>
                      {seesBookings && (
                        <td style={{ textAlign: 'end' }}>
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
          <Pagination page={page} pageSize={pageSize} total={total} noun="user" onPage={setPage} onPageSize={changePageSize} />
        )}
      </div>
    </section>
  )
}
