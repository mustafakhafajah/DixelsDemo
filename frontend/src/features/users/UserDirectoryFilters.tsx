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
  const roles = useUserRoles()
  const set = (p: Partial<UserDirectoryFilterValues>) => onChange({ ...value, ...p })
  const active = Object.values(value).some((v) => v !== '')

  return (
    <div className="filter-bar">
      <div style={{ flex: 1, minWidth: 220 }}>
        <label className="lbl" htmlFor="udf-search">Search</label>
        <input id="udf-search" className="inp" placeholder="Search by name, username or email" value={value.search}
          onChange={(e) => set({ search: e.target.value })} />
      </div>
      <div style={{ width: 160 }}>
        <label className="lbl" htmlFor="udf-role-filter">Role</label>
        <Dropdown id="udf-role-filter" value={value.role} onChange={(v) => set({ role: v })}
          options={[{ value: '', label: 'All roles' }, ...(roles.data ?? []).map((r) => ({ value: r.name, label: r.displayName }))]} />
      </div>
      <div style={{ width: 160 }}>
        <label className="lbl" htmlFor="udf-status">Status</label>
        <Dropdown id="udf-status" value={value.status} onChange={(v) => set({ status: v })}
          options={[{ value: '', label: 'All statuses' }, { value: 'active', label: 'Active' }, { value: 'inactive', label: 'Inactive' }]} />
      </div>
      <div style={{ width: 150 }}>
        <label className="lbl" htmlFor="udf-lock">Lock</label>
        <Dropdown id="udf-lock" value={value.lock} onChange={(v) => set({ lock: v })}
          options={[{ value: '', label: 'All' }, { value: 'locked', label: 'Locked' }, { value: 'unlocked', label: 'Not locked' }]} />
      </div>
      <button type="button" className="btn btn-sm" disabled={!active} onClick={() => onChange(EMPTY_USER_FILTERS)}>Clear filters</button>
    </div>
  )
}
