import type { Building, Floor, SpaceType } from '../../api/types'
import { SPACE_TYPE_LABELS } from '../../api/types'

export interface SpaceRegistryFilterValues {
  code: string
  buildingId: string
  floorId: string
  name: string
  type: SpaceType | ''
}

export const EMPTY_SPACE_FILTERS: SpaceRegistryFilterValues = { code: '', buildingId: '', floorId: '', name: '', type: '' }

export function SpaceRegistryFilters({ value, onChange, buildings, floors }: {
  value: SpaceRegistryFilterValues
  onChange: (v: SpaceRegistryFilterValues) => void
  buildings: Building[]
  floors: Floor[]
}) {
  const set = (p: Partial<SpaceRegistryFilterValues>) => onChange({ ...value, ...p })
  /* With a building picked, only its floors make sense; without one, label floors with their building. */
  const floorOptions = value.buildingId ? floors.filter((f) => f.buildingId === value.buildingId) : floors
  const active = Object.values(value).some((v) => v !== '')

  return (
    <div className="filter-bar">
      <div style={{ width: 150 }}>
        <label className="lbl" htmlFor="sf-code">ID</label>
        <input id="sf-code" className="inp mono" placeholder="SP-3F2A…" value={value.code}
          onChange={(e) => set({ code: e.target.value })} />
      </div>
      <div style={{ width: 180 }}>
        <label className="lbl" htmlFor="sf-building">Building</label>
        <select id="sf-building" className="inp" value={value.buildingId}
          onChange={(e) => set({ buildingId: e.target.value, floorId: '' })}>
          <option value="">All buildings</option>
          {buildings.map((b) => <option key={b.id} value={b.id}>{b.name}</option>)}
        </select>
      </div>
      <div style={{ width: 180 }}>
        <label className="lbl" htmlFor="sf-floor">Floor</label>
        <select id="sf-floor" className="inp" value={value.floorId} onChange={(e) => set({ floorId: e.target.value })}>
          <option value="">All floors</option>
          {floorOptions.map((f) => (
            <option key={f.id} value={f.id}>{value.buildingId ? `Floor ${f.name}` : `${f.buildingName} · Floor ${f.name}`}</option>
          ))}
        </select>
      </div>
      <div style={{ flex: 1, minWidth: 180 }}>
        <label className="lbl" htmlFor="sf-name">Space name</label>
        <input id="sf-name" className="inp" placeholder="Contains…" value={value.name}
          onChange={(e) => set({ name: e.target.value })} />
      </div>
      <div style={{ width: 160 }}>
        <label className="lbl" htmlFor="sf-type">Type</label>
        <select id="sf-type" className="inp" value={value.type} onChange={(e) => set({ type: e.target.value as SpaceType | '' })}>
          <option value="">All types</option>
          {(Object.keys(SPACE_TYPE_LABELS) as SpaceType[]).map((t) => <option key={t} value={t}>{SPACE_TYPE_LABELS[t]}</option>)}
        </select>
      </div>
      <button type="button" className="btn btn-sm" disabled={!active} onClick={() => onChange(EMPTY_SPACE_FILTERS)}>Clear filters</button>
    </div>
  )
}
