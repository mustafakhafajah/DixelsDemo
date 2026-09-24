import { useEffect, useState } from 'react'
import { errorText } from '../../api/client'
import { useBuildings, useFloors, useSetStatus, useSpaceRegistry } from '../../api/hooks'
import { SPACE_TYPE_LABELS, type EstateStatus } from '../../api/types'
import { EstatePill, Loading, plural, shortId } from '../../components/bits'
import { Pagination } from '../../components/Pagination'
import { RowMenu } from '../../components/RowMenu'
import { pad } from '../../lib/dateUtils'
import { useDebouncedValue } from '../../lib/useDebouncedValue'
import { modals } from '../../state/modalStore'
import { toast } from '../../state/toastStore'
import { EMPTY_SPACE_FILTERS, SpaceRegistryFilters, type SpaceRegistryFilterValues } from './SpaceRegistryFilters'

function useToggleStatus() {
  const setStatus = useSetStatus()
  return (kind: 'building' | 'floor' | 'space', id: string, name: string, current: EstateStatus, scope: string) => {
    const status: EstateStatus = current === 'Active' ? 'Inactive' : 'Active'
    setStatus.mutateAsync({ kind, id, status }).then(
      () => toast(status === 'Active' ? 'ok' : 'warn', `${name} is now ${status.toLowerCase()}`,
        status === 'Inactive' ? `Every space ${scope} is blocked from new bookings. Existing bookings are kept.` : 'It can be booked again.'),
      (e) => { const { code, message } = errorText(e); toast('err', 'Request rejected', message, code) })
  }
}

function RegistryCard({ title, sub, addLabel, onAdd, children }: { title: string; sub: string; addLabel: string; onAdd: () => void; children: React.ReactNode }) {
  return (
    <div className="card" style={{ overflow: 'hidden' }}>
      <div className="card-head">
        <div><h2 className="card-title">{title}</h2><p className="card-sub">{sub}</p></div>
        <button type="button" className="btn btn-accent btn-sm" onClick={onAdd}>{addLabel}</button>
      </div>
      {children}
    </div>
  )
}

export function BuildingsPage() {
  const q = useBuildings()
  const toggle = useToggleStatus()
  return (
    <section>
      <RegistryCard title="Building registry" sub="Every building in the estate." addLabel="Add a building" onAdd={() => modals.building()}>
        <div style={{ padding: '6px 0' }}>
          {!q.data ? <Loading /> : q.data.length ? q.data.map((b) => (
            <div key={b.id} className="registry-row">
              <div style={{ flex: 1, minWidth: 0 }}>
                <div style={{ display: 'flex', alignItems: 'center', gap: 9 }}>
                  <span style={{ fontSize: 13.5, fontWeight: 600 }}>{b.name}</span>
                  <span className="tag mono">{b.timeZone}</span>
                  <EstatePill status={b.status} />
                </div>
                <div style={{ fontSize: 11.5, color: 'var(--slate)', marginTop: 3 }}>
                  {plural(b.floorCount, 'floor')} · {plural(b.spaceCount, 'space')} · {pad(b.openHour)}:00–{pad(b.closeHour)}:00 · {b.minBookingMinutes}m–{b.maxBookingHours}h
                  {b.holidays.length ? ` · ${plural(b.holidays.length, 'holiday')}` : ''}
                </div>
              </div>
              <RowMenu items={[
                { label: 'Edit', onClick: () => modals.building(b) },
                { label: b.status === 'Active' ? 'Deactivate' : 'Activate', onClick: () => toggle('building', b.id, b.name, b.status, 'in it') },
                { label: 'Clean', onClick: () => modals.maintenance({ scopeType: 'Building', scopeId: b.id, label: b.name }) },
              ]} />
            </div>
          )) : <p className="empty-note">No buildings yet. Add one above.</p>}
        </div>
      </RegistryCard>
    </section>
  )
}

export function FloorsPage() {
  const q = useFloors()
  const buildings = useBuildings()
  const toggle = useToggleStatus()
  const byId = Object.fromEntries((buildings.data ?? []).map((b) => [b.id, b]))
  return (
    <section>
      <RegistryCard title="Floor registry" sub="Every floor across every building." addLabel="Add a floor" onAdd={() => modals.floor()}>
        <div style={{ padding: '6px 0' }}>
          {!q.data ? <Loading /> : q.data.length ? q.data.map((f) => {
            const b = byId[f.buildingId]
            const overridden = [f.openHourOverride, f.closeHourOverride, f.minBookingMinutesOverride, f.maxBookingHoursOverride].some((v) => v != null)
            return (
              <div key={f.id} className="registry-row">
                <div style={{ flex: 1, minWidth: 0 }}>
                  <div style={{ display: 'flex', alignItems: 'center', gap: 9 }}>
                    <span style={{ fontSize: 13.5, fontWeight: 600 }}>{f.buildingName} · Floor {f.name}</span>
                    <EstatePill status={f.status} />
                  </div>
                  <div style={{ fontSize: 11.5, color: 'var(--slate)', marginTop: 3 }}>
                    {plural(f.spaceCount, 'space')}
                    {overridden && b
                      ? ` · ${pad(f.openHourOverride ?? b.openHour)}:00–${pad(f.closeHourOverride ?? b.closeHour)}:00 · ${f.minBookingMinutesOverride ?? b.minBookingMinutes}m–${f.maxBookingHoursOverride ?? b.maxBookingHours}h`
                      : ' · inherits building hours'}
                  </div>
                </div>
                <RowMenu items={[
                  { label: 'Edit', onClick: () => modals.floor(f) },
                  { label: f.status === 'Active' ? 'Deactivate' : 'Activate', onClick: () => toggle('floor', f.id, `Floor ${f.name}`, f.status, 'on it') },
                  { label: 'Clean', onClick: () => modals.maintenance({ scopeType: 'Floor', scopeId: f.id, label: `${f.buildingName} · Floor ${f.name}` }) },
                ]} />
              </div>
            )
          }) : <p className="empty-note">No floors yet. Add one above.</p>}
        </div>
      </RegistryCard>
    </section>
  )
}

export function SpacesPage() {
  const buildings = useBuildings()
  const floors = useFloors()
  const toggle = useToggleStatus()
  const [filters, setFilters] = useState<SpaceRegistryFilterValues>(EMPTY_SPACE_FILTERS)
  const [page, setPage] = useState(1)
  const [pageSize, setPageSize] = useState(25)
  /* Only the typed fields are debounced; dropdowns apply at once. */
  const code = useDebouncedValue(filters.code.trim())
  const name = useDebouncedValue(filters.name.trim())

  const q = useSpaceRegistry({
    page, pageSize, code: code || undefined, name: name || undefined,
    buildingId: filters.buildingId || undefined, floorId: filters.floorId || undefined, type: filters.type || undefined,
  })
  const total = q.data?.totalCount ?? 0

  /* If the result shrinks (a filter, or the last row of the last page was changed), stay on a real page. */
  useEffect(() => {
    const pageCount = Math.max(1, Math.ceil(total / pageSize))
    if (q.data && page > pageCount) setPage(pageCount)
  }, [q.data, total, page, pageSize])

  const changeFilters = (v: SpaceRegistryFilterValues) => { setFilters(v); setPage(1) }
  const changePageSize = (s: number) => { setPageSize(s); setPage(1) }
  const filtered = Object.values(filters).some((v) => v !== '')

  return (
    <section>
      <div className="card" style={{ overflow: 'hidden' }}>
        <div className="card-head">
          <div><h2 className="card-title">Space registry</h2><p className="card-sub">One row is one physical unit. Inactive units cannot be booked.</p></div>
          <button type="button" className="btn btn-accent btn-sm" onClick={() => modals.space()}>Add a space</button>
        </div>
        <SpaceRegistryFilters value={filters} onChange={changeFilters} buildings={buildings.data ?? []} floors={floors.data ?? []} />
        {!q.data ? <Loading /> : !q.data.items.length ? (
          <p className="empty-note">
            {filtered ? 'No spaces match these filters.' : 'No spaces yet. Add one above.'}
            {filtered && <button type="button" className="btn btn-sm" style={{ marginLeft: 10 }} onClick={() => changeFilters(EMPTY_SPACE_FILTERS)}>Clear filters</button>}
          </p>
        ) : (
          <table className="grid" style={{ opacity: q.isPlaceholderData ? 0.6 : 1 }}>
            <thead>
              <tr><th>ID</th><th>Space</th><th>Type</th><th>Location</th><th>TZ</th><th>Status</th><th style={{ textAlign: 'right' }}>Actions</th></tr>
            </thead>
            <tbody>
              {q.data.items.map((s) => (
                <tr key={s.id}>
                  <td className="mono" style={{ fontSize: 12, color: 'var(--slate)' }}>{shortId(s.id, 'SP')}</td>
                  <td><div style={{ fontWeight: 600 }}>{s.name}</div><div style={{ fontSize: 11, color: 'var(--slate)' }}>{s.note || 'No description'}</div></td>
                  <td>{SPACE_TYPE_LABELS[s.type]}</td>
                  <td>{s.buildingName}<div style={{ fontSize: 11, color: 'var(--slate)' }}>Floor {s.floorName}</div></td>
                  <td className="mono" style={{ fontSize: 11.5, color: 'var(--slate)' }}>{s.timeZone}</td>
                  <td>
                    <EstatePill status={s.status} />
                    <div style={{ fontSize: 11, color: 'var(--slate)', marginTop: 3 }}>{q.data.upcomingBookingCounts[s.id] ?? 0} upcoming</div>
                  </td>
                  <td style={{ textAlign: 'right' }}>
                    <div style={{ display: 'inline-block' }}>
                      <RowMenu items={[
                        { label: 'Edit', onClick: () => modals.space(s) },
                        { label: s.status === 'Active' ? 'Deactivate' : 'Activate', onClick: () => toggle('space', s.id, s.name, s.status, 'here') },
                        { label: 'Clean', onClick: () => modals.maintenance({ scopeType: 'Space', scopeId: s.id, label: s.name }) },
                      ]} />
                    </div>
                  </td>
                </tr>
              ))}
            </tbody>
          </table>
        )}
        {q.data && total > 0 && (
          <Pagination page={page} pageSize={pageSize} total={total} noun="spaces" onPage={setPage} onPageSize={changePageSize} />
        )}
      </div>
    </section>
  )
}
