import { useTranslation } from 'react-i18next'
import { Dropdown } from '../../components/pickers'
import { useUserRoles } from './api'

export interface UserDirectoryFilterValues {
  search: string
  /* '' or a role name */
  role: string
  /* '' | 'active' | 'inactive' */
  status: string
  /* '' | 'locked' | 'unlocked' */
  lock: string
}

export const EMPTY_USER_FILTERS: UserDirectoryFilterValues = { search: '', role: '', status: '', lock: '' }

export function UserDirectoryFilters({ value, onChange }: {
  value: UserDirectoryFilterValues
  onChange: (v: UserDirectoryFilterValues) => void
}) {
  const { t } = useTranslation()
  const roles = useUserRoles()
  const set = (p: Partial<UserDirectoryFilterValues>) => onChange({ ...value, ...p })
  const active = Object.values(value).some((v) => v !== '')

  return (
    <div className="filter-bar">
      <div style={{ flex: 1, minWidth: 220 }}>
        <label className="lbl" htmlFor="udf-search">{t('users.search')}</label>
        <input id="udf-search" className="inp" dir="auto" placeholder={t('users.searchPlaceholder')} value={value.search}
          onChange={(e) => set({ search: e.target.value })} />
      </div>
      <div style={{ width: 160 }}>
        <label className="lbl" htmlFor="udf-role-filter">{t('users.col.role')}</label>
        <Dropdown id="udf-role-filter" value={value.role} onChange={(v) => set({ role: v })}
          options={[{ value: '', label: t('users.allRoles') }, ...(roles.data ?? []).map((r) => ({ value: r.name, label: r.displayName }))]} />
      </div>
      <div style={{ width: 160 }}>
        <label className="lbl" htmlFor="udf-status">{t('users.col.status')}</label>
        <Dropdown id="udf-status" value={value.status} onChange={(v) => set({ status: v })}
          options={[{ value: '', label: t('users.allStatuses') }, { value: 'active', label: t('users.active') }, { value: 'inactive', label: t('users.inactive') }]} />
      </div>
      <div style={{ width: 150 }}>
        <label className="lbl" htmlFor="udf-lock">{t('users.col.lock')}</label>
        <Dropdown id="udf-lock" value={value.lock} onChange={(v) => set({ lock: v })}
          options={[{ value: '', label: t('users.all') }, { value: 'locked', label: t('users.locked') }, { value: 'unlocked', label: t('users.notLocked') }]} />
      </div>
      <button type="button" className="btn btn-sm" disabled={!active} onClick={() => onChange(EMPTY_USER_FILTERS)}>{t('common.clearFilters')}</button>
    </div>
  )
}
