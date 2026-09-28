import type { Building, Floor, SpaceType } from '../../api/types'
import { Dropdown } from '../../components/pickers'

export interface SpaceRegistryFilterValues {
  buildingId: string
  floorId: string
  name: string
  typeId: string
}

export const EMPTY_SPACE_FILTERS: SpaceRegistryFilterValues = { buildingId: '', floorId: '', name: '', typeId: '' }

export function SpaceRegistryFilters({ value, onChange, buildings, floors, types }: {
  value: SpaceRegistryFilterValues
  onChange: (v: SpaceRegistryFilterValues) => void
  buildings: Building[]
  floors: Floor[]
  types: SpaceType[]
}) {
  const set = (p: Partial<SpaceRegistryFilterValues>) => onChange({ ...value, ...p })
  /* Floor stays locked until a building is picked, then lists only that building's floors. */
  const floorOptions = value.buildingId ? floors.filter((f) => f.buildingId === value.buildingId) : []
  const active = Object.values(value).some((v) => v !== '')

  return (
    <div className="filter-bar">
      <div style={{ width: 180 }}>
        <label className="lbl" htmlFor="sf-building">Building</label>
        <Dropdown id="sf-building" value={value.buildingId} onChange={(v) => set({ buildingId: v, floorId: '' })}
          options={[{ value: '', label: 'All buildings' }, ...buildings.map((b) => ({ value: b.id, label: b.name }))]} />
      </div>
      <div style={{ width: 180 }}>
        <label className="lbl" htmlFor="sf-floor">Floor</label>
        <Dropdown id="sf-floor" value={value.floorId} onChange={(v) => set({ floorId: v })}
          disabled={!value.buildingId} placeholder="Building first"
          options={value.buildingId ? [{ value: '', label: 'All floors' }, ...floorOptions.map((f) => ({ value: f.id, label: `Floor ${f.name}` }))] : []} />
      </div>
      <div style={{ flex: 1, minWidth: 180 }}>
        <label className="lbl" htmlFor="sf-name">Space name</label>
        <input id="sf-name" className="inp" placeholder="Contains…" value={value.name}
          onChange={(e) => set({ name: e.target.value })} />
      </div>
      <div style={{ width: 160 }}>
        <label className="lbl" htmlFor="sf-type">Type</label>
        <Dropdown id="sf-type" value={value.typeId} onChange={(v) => set({ typeId: v })}
          options={[{ value: '', label: 'All types' }, ...types.map((t) => ({ value: t.id, label: t.name }))]} />
      </div>
      <button type="button" className="btn btn-sm" disabled={!active} onClick={() => onChange(EMPTY_SPACE_FILTERS)}>Clear filters</button>
    </div>
  )
}
