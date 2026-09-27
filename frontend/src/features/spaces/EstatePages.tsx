import { useEffect, useState, type ReactNode } from 'react'
import { errorText } from '../../api/client'
import { useBuildings, useDeleteSpaceType, useFloors, useSetBookable, useSpaceRegistry, useSpaceTypes, type EstateKind } from '../../api/hooks'
import type { SpaceType } from '../../api/types'
import { BookablePill, Loading, plural, shortId } from '../../components/bits'
import { Pagination } from '../../components/Pagination'
import { RowMenu } from '../../components/RowMenu'
import { pad } from '../../lib/dateUtils'
import { useClientPaging } from '../../lib/useClientPaging'
import { useDebouncedValue } from '../../lib/useDebouncedValue'
import { modals } from '../../state/modalStore'
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

function RegistryCard({ title, sub, addLabel, onAdd, children }: { title: string; sub: string; addLabel: string; onAdd: () => void; children: ReactNode }) {
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

const muted = { fontSize: 11, color: 'var(--slate)' } as const
const idCell = { fontSize: 12, color: 'var(--slate)' } as const

function Actions({ children }: { children: ReactNode }) {
  return <td style={{ textAlign: 'right' }}><div style={{ display: 'inline-block' }}>{children}</div></td>
}

/* ─── Buildings ─── */

export function BuildingsPage() {
  const q = useBuildings()
  const bookableItem = useBookableMenuItem()
  const paging = useClientPaging(q.data ?? [])
  return (
    <section>
      <RegistryCard title="Building registry" sub="Every building in the estate. Its time zone applies to every floor and space in it." addLabel="Add a building" onAdd={() => modals.building()}>
        {!q.data ? <Loading /> : !q.data.length ? <p className="empty-note">No buildings yet. Add one above.</p> : (
          <>
            <table className="grid">
              <thead>
                <tr><th>ID</th><th>Building</th><th>Time zone</th><th>Hours (UTC)</th><th>Booking length</th><th>Floors · Spaces</th><th>Bookable</th><th style={{ textAlign: 'right' }}>Actions</th></tr>
              </thead>
              <tbody>
                {paging.rows.map((b) => (
                  <tr key={b.id}>
                    <td className="mono" style={idCell}>{shortId(b.id, 'BD')}</td>
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
                        { label: 'Edit', onClick: () => modals.building(b) },
                        bookableItem('building', b.id, b.name, b.isBookable),
                        { label: 'Block time', onClick: () => modals.maintenance({ scopeType: 'Building', scopeId: b.id, label: b.name }) },
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
  const bookableItem = useBookableMenuItem()
  const paging = useClientPaging(q.data ?? [])
  const byId = Object.fromEntries((buildings.data ?? []).map((b) => [b.id, b]))
  return (
    <section>
      <RegistryCard title="Floor registry" sub="Every floor across every building. A floor uses its building's time zone." addLabel="Add a floor" onAdd={() => modals.floor()}>
        {!q.data ? <Loading /> : !q.data.length ? <p className="empty-note">No floors yet. Add one above.</p> : (
          <>
            <table className="grid">
              <thead>
                <tr><th>ID</th><th>Floor</th><th>Building</th><th>Time zone</th><th>Hours (UTC)</th><th>Booking length</th><th>Spaces</th><th>Bookable</th><th style={{ textAlign: 'right' }}>Actions</th></tr>
              </thead>
              <tbody>
                {paging.rows.map((f) => {
                  const b = byId[f.buildingId]
                  const overridden = [f.openHourOverride, f.closeHourOverride, f.minBookingMinutesOverride, f.maxBookingHoursOverride].some((v) => v != null)
                  return (
                    <tr key={f.id}>
                      <td className="mono" style={idCell}>{shortId(f.id, 'FL')}</td>
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
                          { label: 'Edit', onClick: () => modals.floor(f) },
                          bookableItem('floor', f.id, `${f.buildingName} · Floor ${f.name}`, f.isBookable),
                          { label: 'Block time', onClick: () => modals.maintenance({ scopeType: 'Floor', scopeId: f.id, label: `${f.buildingName} · Floor ${f.name}` }) },
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
  const bookableItem = useBookableMenuItem()
  const [filters, setFilters] = useState<SpaceRegistryFilterValues>(EMPTY_SPACE_FILTERS)
  const [page, setPage] = useState(1)
  const [pageSize, setPageSize] = useState(25)
  /* Only the typed fields are debounced; dropdowns apply at once. */
  const code = useDebouncedValue(filters.code.trim())
  const name = useDebouncedValue(filters.name.trim())

  const q = useSpaceRegistry({
    page, pageSize, code: code || undefined, name: name || undefined,
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

  return (
    <section>
      <div className="card" style={{ overflow: 'hidden' }}>
        <div className="card-head">
          <div><h2 className="card-title">Space registry</h2><p className="card-sub">One row is one physical unit. Inactive units cannot be booked. A space uses its building's time zone.</p></div>
          <button type="button" className="btn btn-accent btn-sm" onClick={() => modals.space()}>Add a space</button>
        </div>
        <SpaceRegistryFilters value={filters} onChange={changeFilters}
          buildings={buildings.data ?? []} floors={floors.data ?? []} types={types.data ?? []} />
        {!q.data ? <Loading /> : !q.data.items.length ? (
          <p className="empty-note">
            {filtered ? 'No spaces match these filters.' : 'No spaces yet. Add one above.'}
            {filtered && <button type="button" className="btn btn-sm" style={{ marginLeft: 10 }} onClick={() => changeFilters(EMPTY_SPACE_FILTERS)}>Clear filters</button>}
          </p>
        ) : (
          <table className="grid" style={{ opacity: q.isPlaceholderData ? 0.6 : 1 }}>
            <thead>
              <tr><th>ID</th><th>Space</th><th>Type</th><th>Location</th><th>Time zone</th><th>Bookable</th><th style={{ textAlign: 'right' }}>Actions</th></tr>
            </thead>
            <tbody>
              {q.data.items.map((s) => (
                <tr key={s.id}>
                  <td className="mono" style={idCell}>{shortId(s.id, 'SP')}</td>
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
                      { label: 'Edit', onClick: () => modals.space(s) },
                      bookableItem('space', s.id, s.name, s.isBookable),
                      { label: 'Block time', onClick: () => modals.maintenance({ scopeType: 'Space', scopeId: s.id, label: s.name }) },
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
      <RegistryCard title="Space types" sub="The kinds of space an admin can give a space. Renaming a type updates every space that uses it." addLabel="Add a space type" onAdd={() => modals.spaceType()}>
        {!q.data ? <Loading /> : !q.data.length ? <p className="empty-note">No space types yet. Add one above.</p> : (
          <>
            <table className="grid">
              <thead>
                <tr><th>ID</th><th>Type</th><th>Spaces using it</th><th style={{ textAlign: 'right' }}>Actions</th></tr>
              </thead>
              <tbody>
                {paging.rows.map((t) => (
                  <tr key={t.id}>
                    <td className="mono" style={idCell}>{shortId(t.id, 'ST')}</td>
                    <td style={{ fontWeight: 600 }}>{t.name}</td>
                    <td>{plural(t.spaceCount, 'space')}</td>
                    <Actions>
                      <RowMenu items={[
                        { label: 'Edit', onClick: () => modals.spaceType(t) },
                        { label: 'Delete', onClick: () => remove(t) },
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
