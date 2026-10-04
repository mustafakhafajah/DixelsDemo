import { useTranslation } from 'react-i18next'
import { useNavigate } from 'react-router-dom'
import { useSession } from '../../app/session'
import { initials, LoadError, Loading } from '../../components/bits'
import { Pagination } from '../../components/Pagination'
import { RowMenu, type RowMenuItem } from '../../components/RowMenu'
import { formatDate, parseUtc } from '../../lib/dateUtils'
import { useServerPaging, useStayOnRealPage } from '../../lib/useServerPaging'
import { urlParam, useDropUnknown, useUrlSearchBox, useUrlState } from '../../lib/useUrlState'
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

const isAdminRole = (role: UserRole | null, roles?: UserDirectoryRole[]) =>
  !!role && (role.toLowerCase() === 'admin' || !!roles?.find((r) => r.name.toLowerCase() === role.toLowerCase())?.isAdmin)

/* Admin-type roles get the accent pill; every other role is grey. */
function RolePill({ role, roles }: { role: UserRole | null; roles?: UserDirectoryRole[] }) {
  const { t } = useTranslation()
  if (!role) return <span className="lock-off">{t('nav.noRole')}</span>
  return <span className={`pill ${isAdminRole(role, roles) ? 'pill-confirmed' : 'pill-ended'}`}><span className="dot" />{roleLabel(role, roles)}</span>
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
  const { userId, isAdmin } = useSession()
  const navigate = useNavigate()
  /* Every filter and the page live in the address (?q=…&role=…&status=…&lock=…&page=…), so a copied link shows
   * the same list. The typed search reaches the address (and the server) once typing pauses; dropdowns at once. */
  const [url, setUrl] = useUrlState({
    search: urlParam.text('q'),
    role: urlParam.text('role'),
    status: urlParam.oneOf('status', ['', 'active', 'inactive'], ''),
    lock: urlParam.oneOf('lock', ['', 'locked', 'unlocked'], ''),
  })
  const paging = useServerPaging()
  const { page, pageSize, setPage } = paging
  const [searchText, setSearchText] = useUrlSearchBox(url.search, (search) => { setUrl({ search }); setPage(1) })
  const filters: UserDirectoryFilterValues = { ...url, search: searchText }

  const roles = useUserRoles().data
  /* A shared link with a role that no longer exists falls back to every role. */
  useDropUnknown(url.role, roles?.map((r) => r.id), () => setUrl({ role: '' }))

  const q = useUserDirectory({
    page, pageSize, filter: url.search || undefined,
    roleId: url.role || undefined,
    isActive: url.status ? url.status === 'active' : undefined,
    isLocked: url.lock ? url.lock === 'locked' : undefined,
  })
  const total = q.data?.totalCount ?? 0
  useStayOnRealPage(paging, q.data?.totalCount)

  const changeFilters = (v: UserDirectoryFilterValues) => {
    setSearchText(v.search)
    if (v.role !== url.role || v.status !== url.status || v.lock !== url.lock || (!v.search && url.search)) {
      setUrl({ role: v.role, status: v.status as typeof url.status, lock: v.lock as typeof url.lock, ...(v.search ? {} : { search: '' }) })
      setPage(1)
    }
  }
  const changePageSize = paging.setPageSize
  const filtered = Object.values(filters).some((v) => v !== '')

  const menuItems = (u: UserDirectoryItem): RowMenuItem[] => isAdmin
    ? [{
        label: t('users.viewBookings'),
        onClick: () => navigate(`/app/bookings?user=${encodeURIComponent(u.id)}`),
      }]
    : []

  return (
    <section className="user-dir">
      <div className="card" style={{ overflow: 'hidden' }}>
        <UserDirectoryFilters value={filters} onChange={changeFilters} />
        {q.isError ? <LoadError what={t('load.users')} error={q.error} onRetry={() => q.refetch()} /> : !q.data ? <Loading /> : !q.data.items.length ? (
          <p className="empty-note">
            {filtered ? t('users.noMatch') : t('users.none')}
            {filtered && <button type="button" className="btn btn-sm" style={{ marginInlineStart: 10 }} onClick={() => changeFilters(EMPTY_USER_FILTERS)}>{t('common.clearFilters')}</button>}
          </p>
        ) : (
          <div className="table-scroll">
            <table className="grid" style={{ opacity: q.isPlaceholderData ? 0.6 : 1 }}>
              <thead>
                <tr><th>{t('users.col.user')}</th><th>{t('users.col.userName')}</th><th>{t('users.col.role')}</th><th>{t('users.col.status')}</th><th>{t('users.col.lock')}</th>{isAdmin && <th style={{ textAlign: 'end' }}>{t('common.actions')}</th>}</tr>
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
                            <div style={{ fontWeight: 600 }}><bdi>{u.name || u.userName}</bdi>{u.id === userId && <span className="lock-off" style={{ fontWeight: 400 }}> {t('users.youMark')}</span>}</div>
                            <div className="user-email"><bdi>{u.email}</bdi></div>
                          </div>
                        </div>
                      </td>
                      <td className="mono" style={{ fontSize: 12 }}><bdi>{u.userName}</bdi></td>
                      <td><RolePill role={u.role} roles={roles} /></td>
                      <td><StatusPill active={u.isActive} /></td>
                      <td><LockCell user={u} /></td>
                      {isAdmin && (
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
