import { useEffect, useState, type ReactNode } from 'react'
import { errorText } from '../../api/client'
import { useBuildings, useDeleteSpaceType, useFloors, useSetBookable, useSpaceRegistry, useSpaceTypes, type EstateKind } from '../../api/hooks'
import type { SpaceType } from '../../api/types'
import { useSession } from '../../app/session'
import { P } from '../../auth/permissions'
import { BookablePill, LoadError, Loading, plural } from '../../components/bits'
import { Dropdown } from '../../components/pickers'
import { Pagination } from '../../components/Pagination'
import { RowMenu } from '../../components/RowMenu'
import { pad } from '../../lib/dateUtils'
import { useClientPaging } from '../../lib/useClientPaging'
import { useDebouncedValue } from '../../lib/useDebouncedValue'
import { modals } from '../../state/modalStore'
import { useFilteredNavCount } from '../../state/navCountStore'
import { toast } from '../../state/toastStore'
import { EMPTY_SPACE_FILTERS, SpaceRegistryFilters, type SpaceRegistryFilterValues } from './SpaceRegistryFilters'

/* The row menu item: "Make not bookable" asks first (it shows the bookings already made there);
 * "Make bookable" can't hurt anyone, so it applies straight away. */
function useBookableMenuItem() {
  const setBookable = useSetBookable()
  return (kind: EstateKind, id: string, label: string, isBookable: boolean) => isBookable
    ? { label: 'Make not bookable', onClick: () => modals.notBookable({ kind, id, label }) }
    : {
        label: 'Make bookable',
        onClick: () => setBookable.mutateAsync({ kind, id, isBookable: true }).then(
          () => toast('ok', `${label} is bookable`, 'People can book it again.'),
          (e) => { const { code, message } = errorText(e); toast('err', 'Request rejected', message, code) }),
      }
}

/* A row-menu entry only for those allowed to use it. */
const when = <T,>(allowed: boolean, item: T): T[] => (allowed ? [item] : [])

/* Without onAdd (no permission to create) there is no add button. */
function RegistryCard({ title, sub, addLabel, onAdd, children }: { title: string; sub: string; addLabel: string; onAdd?: () => void; children: ReactNode }) {
  return (
    <div className="card" style={{ overflow: 'hidden' }}>
      <div className="card-head">
        <div><h2 className="card-title">{title}</h2><p className="card-sub">{sub}</p></div>
        {onAdd && <button type="button" className="btn btn-accent btn-sm" onClick={onAdd}>{addLabel}</button>}
      </div>
      {children}
    </div>
  )
}

const muted = { fontSize: 11, color: 'var(--slate)' } as const

function Actions({ children }: { children: ReactNode }) {
  return <td style={{ textAlign: 'right' }}><div style={{ display: 'inline-block' }}>{children}</div></td>
}

/* ─── Buildings ─── */

export function BuildingsPage() {
  const q = useBuildings()
  const { can } = useSession()
  const bookableItem = useBookableMenuItem()
  const paging = useClientPaging(q.data ?? [])
  return (
    <section>
      <RegistryCard title="Building registry" sub="Every building in the estate. Its time zone applies to every floor and space in it." addLabel="Add a building" onAdd={can(P.Buildings.Create) ? () => modals.building() : undefined}>
        {q.isError ? <LoadError what="the buildings" error={q.error} onRetry={() => q.refetch()} /> : !q.data ? <Loading /> : !q.data.length ? <p className="empty-note">No buildings yet. Add one above.</p> : (
          <>
            <table className="grid">
              <thead>
                <tr><th>Building</th><th>Time zone</th><th>Hours (UTC)</th><th>Booking length</th><th>Floors · Spaces</th><th>Bookable</th><th style={{ textAlign: 'right' }}>Actions</th></tr>
              </thead>
              <tbody>
                {paging.rows.map((b) => (
                  <tr key={b.id}>
                    <td>
                      <div style={{ fontWeight: 600 }}>{b.name}</div>
                      <div style={muted}>{b.holidays.length ? plural(b.holidays.length, 'holiday') : 'No holidays'}</div>
                    </td>
                    <td className="mono" style={{ fontSize: 11.5 }}>{b.timeZone}</td>
                    <td className="mono" style={{ fontSize: 12 }}>{pad(b.openHour)}:00–{pad(b.closeHour)}:00</td>
                    <td className="mono" style={{ fontSize: 12 }}>{b.minBookingMinutes}m–{b.maxBookingHours}h</td>
                    <td>{plural(b.floorCount, 'floor')}<div style={muted}>{plural(b.spaceCount, 'space')}</div></td>
                    <td><BookablePill bookable={b.isBookable} /></td>
                    <Actions>
                      <RowMenu items={[
                        ...when(can(P.Buildings.Edit), { label: 'Edit', onClick: () => modals.building(b) }),
                        ...when(can(P.Buildings.Edit), bookableItem('building', b.id, b.name, b.isBookable)),
                        ...when(can(P.Maintenance.Create), { label: 'Block time', onClick: () => modals.maintenance({ scopeType: 'Building', scopeId: b.id, label: b.name }) }),
                      ]} />
                    </Actions>
                  </tr>
                ))}
              </tbody>
            </table>
            <Pagination page={paging.page} pageSize={paging.pageSize} total={paging.total} noun="buildings" onPage={paging.setPage} onPageSize={paging.setPageSize} />
          </>
        )}
      </RegistryCard>
    </section>
  )
}

/* ─── Floors ─── */

export function FloorsPage() {
  const q = useFloors()
  const buildings = useBuildings()
  const { can } = useSession()
  const bookableItem = useBookableMenuItem()
  const [buildingId, setBuildingId] = useState('')
  const shown = (q.data ?? []).filter((f) => !buildingId || f.buildingId === buildingId)
  const paging = useClientPaging(shown)
  useFilteredNavCount('floors', buildingId ? shown.length : undefined)
  const byId = Object.fromEntries((buildings.data ?? []).map((b) => [b.id, b]))
  return (
    <section>
      <RegistryCard title="Floor registry" sub="Every floor across every building. A floor uses its building's time zone." addLabel="Add a floor" onAdd={can(P.Floors.Create) ? () => modals.floor() : undefined}>
        <div className="filter-bar">
          <div style={{ width: 220 }}>
            <label className="lbl" htmlFor="ff-building">Building</label>
            <Dropdown id="ff-building" value={buildingId} onChange={(v) => { setBuildingId(v); paging.setPage(1) }}
              options={[{ value: '', label: 'All buildings' }, ...(buildings.data ?? []).map((b) => ({ value: b.id, label: b.name }))]} />
          </div>
          <button type="button" className="btn btn-sm" disabled={!buildingId} onClick={() => setBuildingId('')}>Clear filters</button>
        </div>
        {q.isError ? <LoadError what="the floors" error={q.error} onRetry={() => q.refetch()} /> : !q.data ? <Loading /> : !q.data.length ? <p className="empty-note">No floors yet. Add one above.</p> : !shown.length ? (
          <p className="empty-note">
            This building has no floors yet.
            <button type="button" className="btn btn-sm" style={{ marginLeft: 10 }} onClick={() => setBuildingId('')}>Clear filters</button>
          </p>
        ) : (
          <>
            <table className="grid">
              <thead>
                <tr><th>Floor</th><th>Building</th><th>Time zone</th><th>Hours (UTC)</th><th>Booking length</th><th>Spaces</th><th>Bookable</th><th style={{ textAlign: 'right' }}>Actions</th></tr>
              </thead>
              <tbody>
                {paging.rows.map((f) => {
                  const b = byId[f.buildingId]
                  const overridden = [f.openHourOverride, f.closeHourOverride, f.minBookingMinutesOverride, f.maxBookingHoursOverride].some((v) => v != null)
                  return (
                    <tr key={f.id}>
                      <td style={{ fontWeight: 600 }}>Floor {f.name}</td>
                      <td>{f.buildingName}</td>
                      <td className="mono" style={{ fontSize: 11.5 }}>{b?.timeZone ?? '—'}</td>
                      <td className="mono" style={{ fontSize: 12 }}>
                        {b ? `${pad(f.openHourOverride ?? b.openHour)}:00–${pad(f.closeHourOverride ?? b.closeHour)}:00` : '—'}
                        <div style={{ ...muted, fontFamily: 'inherit' }}>{overridden ? 'own rules' : 'from building'}</div>
                      </td>
                      <td className="mono" style={{ fontSize: 12 }}>
                        {b ? `${f.minBookingMinutesOverride ?? b.minBookingMinutes}m–${f.maxBookingHoursOverride ?? b.maxBookingHours}h` : '—'}
                      </td>
                      <td>{plural(f.spaceCount, 'space')}</td>
                      <td>
                        <BookablePill bookable={f.isBookable && !!b?.isBookable} />
                        {f.isBookable && b && !b.isBookable && <div style={muted}>{b.name} is not bookable</div>}
                      </td>
                      <Actions>
                        <RowMenu items={[
                          ...when(can(P.Floors.Edit), { label: 'Edit', onClick: () => modals.floor(f) }),
                          ...when(can(P.Floors.Edit), bookableItem('floor', f.id, `${f.buildingName} · Floor ${f.name}`, f.isBookable)),
                          ...when(can(P.Maintenance.Create), { label: 'Block time', onClick: () => modals.maintenance({ scopeType: 'Floor', scopeId: f.id, label: `${f.buildingName} · Floor ${f.name}` }) }),
                        ]} />
                      </Actions>
                    </tr>
                  )
                })}
              </tbody>
            </table>
            <Pagination page={paging.page} pageSize={paging.pageSize} total={paging.total} noun="floors" onPage={paging.setPage} onPageSize={paging.setPageSize} />
          </>
        )}
      </RegistryCard>
    </section>
  )
}

/* ─── Spaces (paged on the server) ─── */

export function SpacesPage() {
  const buildings = useBuildings()
  const floors = useFloors()
  const types = useSpaceTypes()
  const { can } = useSession()
  const bookableItem = useBookableMenuItem()
  const [filters, setFilters] = useState<SpaceRegistryFilterValues>(EMPTY_SPACE_FILTERS)
  const [page, setPage] = useState(1)
  const [pageSize, setPageSize] = useState(25)
  /* Only the typed fields are debounced; dropdowns apply at once. */
  const name = useDebouncedValue(filters.name.trim())

  const q = useSpaceRegistry({
    page, pageSize, name: name || undefined,
    buildingId: filters.buildingId || undefined, floorId: filters.floorId || undefined, typeId: filters.typeId || undefined,
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
  useFilteredNavCount('spaces', filtered && q.data ? total : undefined)

  return (
    <section>
      <div className="card" style={{ overflow: 'hidden' }}>
        <div className="card-head">
          <div><h2 className="card-title">Space registry</h2><p className="card-sub">One row is one physical unit. Inactive units cannot be booked. A space uses its building's time zone.</p></div>
          {can(P.Spaces.Create) && <button type="button" className="btn btn-accent btn-sm" onClick={() => modals.space()}>Add a space</button>}
        </div>
        <SpaceRegistryFilters value={filters} onChange={changeFilters}
          buildings={buildings.data ?? []} floors={floors.data ?? []} types={types.data ?? []} />
        {q.isError ? <LoadError what="the spaces" error={q.error} onRetry={() => q.refetch()} /> : !q.data ? <Loading /> : !q.data.items.length ? (
          <p className="empty-note">
            {filtered ? 'No spaces match these filters.' : 'No spaces yet. Add one above.'}
            {filtered && <button type="button" className="btn btn-sm" style={{ marginLeft: 10 }} onClick={() => changeFilters(EMPTY_SPACE_FILTERS)}>Clear filters</button>}
          </p>
        ) : (
          <table className="grid" style={{ opacity: q.isPlaceholderData ? 0.6 : 1 }}>
            <thead>
              <tr><th>Space</th><th>Type</th><th>Location</th><th>Time zone</th><th>Bookable</th><th style={{ textAlign: 'right' }}>Actions</th></tr>
            </thead>
            <tbody>
              {q.data.items.map((s) => (
                <tr key={s.id}>
                  <td><div style={{ fontWeight: 600 }}>{s.name}</div><div style={muted}>{s.note || 'No description'}</div></td>
                  <td>{s.typeName}</td>
                  <td>{s.buildingName}<div style={muted}>Floor {s.floorName}</div></td>
                  <td className="mono" style={{ fontSize: 11.5, color: 'var(--slate)' }}>{s.timeZone}</td>
                  <td>
                    <BookablePill bookable={s.canCurrentUserBook} reason={s.notBookableReason} />
                    {s.isBookable && !s.canCurrentUserBook && <div style={{ ...muted, marginTop: 3 }}>blocked by its floor or building</div>}
                    <div style={{ ...muted, marginTop: 3 }}>{q.data.upcomingBookingCounts[s.id] ?? 0} upcoming</div>
                  </td>
                  <Actions>
                    <RowMenu items={[
                      ...when(can(P.Spaces.Edit), { label: 'Edit', onClick: () => modals.space(s) }),
                      ...when(can(P.Spaces.Edit), bookableItem('space', s.id, s.name, s.isBookable)),
                      ...when(can(P.Maintenance.Create), { label: 'Block time', onClick: () => modals.maintenance({ scopeType: 'Space', scopeId: s.id, label: s.name }) }),
                    ]} />
                  </Actions>
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

/* ─── Space types ─── */

export function SpaceTypesPage() {
  const q = useSpaceTypes()
  const { can } = useSession()
  const del = useDeleteSpaceType()
  const paging = useClientPaging(q.data ?? [])

  const remove = (t: SpaceType) => {
    if (t.spaceCount > 0) {
      toast('warn', `${t.name} is still in use`, `${plural(t.spaceCount, 'space')} use this type. Give them another type first.`, 'space_type.in_use')
      return
    }
    if (!window.confirm(`Delete the space type "${t.name}"?`)) return
    del.mutateAsync(t.id).then(
      () => toast('ok', 'Space type deleted', t.name),
      (e) => { const { code, message } = errorText(e); toast('err', 'Request rejected', message, code) })
  }

  return (
    <section>
      <RegistryCard title="Space types" sub="The kinds of space an admin can give a space. Renaming a type updates every space that uses it." addLabel="Add a space type" onAdd={can(P.SpaceTypes.Create) ? () => modals.spaceType() : undefined}>
        {q.isError ? <LoadError what="the space types" error={q.error} onRetry={() => q.refetch()} /> : !q.data ? <Loading /> : !q.data.length ? <p className="empty-note">No space types yet. Add one above.</p> : (
          <>
            <table className="grid">
              <thead>
                <tr><th>Type</th><th>Spaces using it</th><th style={{ textAlign: 'right' }}>Actions</th></tr>
              </thead>
              <tbody>
                {paging.rows.map((t) => (
                  <tr key={t.id}>
                    <td style={{ fontWeight: 600 }}>{t.name}</td>
                    <td>{plural(t.spaceCount, 'space')}</td>
                    <Actions>
                      <RowMenu items={[
                        ...when(can(P.SpaceTypes.Edit), { label: 'Edit', onClick: () => modals.spaceType(t) }),
                        ...when(can(P.SpaceTypes.Delete), { label: 'Delete', onClick: () => remove(t) }),
                      ]} />
                    </Actions>
                  </tr>
                ))}
              </tbody>
            </table>
            <Pagination page={paging.page} pageSize={paging.pageSize} total={paging.total} noun="space types" onPage={paging.setPage} onPageSize={paging.setPageSize} />
          </>
        )}
      </RegistryCard>
    </section>
  )
}
