import { useTranslation } from 'react-i18next'
import type { Building, Floor, SpaceType } from '../../api/types'
import { Dropdown } from '../../components/pickers'

export interface SpaceRegistryFilterValues {
  buildingId: string
  floorId: string
  name: string
  typeId: string
}

export function SpaceRegistryFilters({ value, onChange, buildings, floors, types }: {
  value: SpaceRegistryFilterValues
  onChange: (v: SpaceRegistryFilterValues) => void
  buildings: Building[]
  floors: Floor[]
  types: SpaceType[]
}) {
  const { t } = useTranslation()
  const set = (p: Partial<SpaceRegistryFilterValues>) => onChange({ ...value, ...p })
  /* Floor stays locked until a building is picked, then lists only that building's floors. */
  const floorOptions = value.buildingId ? floors.filter((f) => f.buildingId === value.buildingId) : []

  return (
    <div className="filter-bar">
      <div style={{ flex: 1, minWidth: 180, alignSelf: 'flex-end' }}>
        <input id="sf-name" className="inp" dir="auto" placeholder={t('estate.spaceName')} aria-label={t('estate.spaceName')} value={value.name}
          onChange={(e) => set({ name: e.target.value })} />
      </div>
      <div style={{ width: 180 }}>
        <label className="lbl" htmlFor="sf-building">{t('common.building')}</label>
        <Dropdown id="sf-building" value={value.buildingId} onChange={(v) => set({ buildingId: v, floorId: '' })}
          options={[{ value: '', label: t('common.allBuildings') }, ...buildings.map((b) => ({ value: b.id, label: b.name }))]} />
      </div>
      <div style={{ width: 180 }}>
        <label className="lbl" htmlFor="sf-floor">{t('common.floor')}</label>
        <Dropdown id="sf-floor" value={value.floorId} onChange={(v) => set({ floorId: v })}
          disabled={!value.buildingId} placeholder={t('common.buildingFirst')}
          options={value.buildingId ? [{ value: '', label: t('common.allFloors') }, ...floorOptions.map((f) => ({ value: f.id, label: t('common.floorName', { name: f.name }) }))] : []} />
      </div>
      <div style={{ width: 160 }}>
        <label className="lbl" htmlFor="sf-type">{t('common.type')}</label>
        <Dropdown id="sf-type" value={value.typeId} onChange={(v) => set({ typeId: v })}
          options={[{ value: '', label: t('common.allTypes') }, ...types.map((t) => ({ value: t.id, label: t.name }))]} />
      </div>
    </div>
  )
}
