import { useEffect, useState, type ReactNode } from 'react'
import { useTranslation } from 'react-i18next'
import i18n from 'i18next'
import { errorText } from '../../api/client'
import { useBuildings, useDeleteSpaceType, useFloors, useSetBookable, useSpaceRegistry, useSpaceTypes, type EstateKind } from '../../api/hooks'
import type { SpaceType } from '../../api/types'
import { PageActions } from '../../app/pageActions'
import { useSession } from '../../app/session'
import { P } from '../../auth/permissions'
import { BookablePill, LoadError, Loading } from '../../components/bits'
import { Dropdown } from '../../components/pickers'
import { Pagination } from '../../components/Pagination'
import { RowMenu } from '../../components/RowMenu'
import { closedWeekdaysLabel } from '../../lib/closedDays'
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
    ? { label: i18n.t('estate.makeNotBookable'), onClick: () => modals.notBookable({ kind, id, label }) }
    : {
        label: i18n.t('estate.makeBookable'),
        onClick: () => setBookable.mutateAsync({ kind, id, isBookable: true }).then(
          () => toast('ok', i18n.t('estate.toast.bookable', { name: label }), i18n.t('estate.toast.bookableAgain')),
          (e) => { const { code, message } = errorText(e); toast('err', i18n.t('common.requestRejected'), message, code) }),
      }
}

/* A row-menu entry only for those allowed to use it. */
const when = <T,>(allowed: boolean, item: T): T[] => (allowed ? [item] : [])

/* The page's list. Its title is the top bar's; the add button sits there too (PageActions).
 * Without onAdd (no permission to create) there is no add button. */
function RegistryCard({ addLabel, onAdd, children }: { addLabel: string; onAdd?: () => void; children: ReactNode }) {
  return (
    <>
      {onAdd && <PageActions><button type="button" className="btn btn-primary" onClick={onAdd}>{addLabel}</button></PageActions>}
      <div className="card" style={{ overflow: 'hidden' }}>{children}</div>
    </>
  )
}

const muted = { fontSize: 11, color: 'var(--slate)' } as const
/* Space between an empty list's sentence and its buttons. */
const gap = { marginInlineStart: 10 } as const

function Actions({ children }: { children: ReactNode }) {
  return <td style={{ textAlign: 'end' }}><div style={{ display: 'inline-block' }}>{children}</div></td>
}

/* ─── Buildings ─── */

export function BuildingsPage() {
  const { t } = useTranslation()
  const q = useBuildings()
  const { can } = useSession()
  const bookableItem = useBookableMenuItem()
  const paging = useClientPaging(q.data ?? [])
  return (
    <section>
      <RegistryCard addLabel={t('estate.addBuilding')} onAdd={can(P.Buildings.Create) ? () => modals.building() : undefined}>
        {q.isError ? <LoadError what={t('load.buildings')} error={q.error} onRetry={() => q.refetch()} /> : !q.data ? <Loading /> : !q.data.length ? <p className="empty-note">{t('estate.noBuildings')}{can(P.Buildings.Create) && <button type="button" className="btn btn-primary btn-sm" style={gap} onClick={() => modals.building()}>{t('estate.addBuilding')}</button>}</p> : (
          <>
            <div className="table-scroll">
              <table className="grid">
                <thead>
                  <tr><th>{t('estate.col.building')}</th><th>{t('estate.col.timeZone')}</th><th>{t('estate.col.hoursUtc')}</th><th>{t('estate.col.bookingLength')}</th><th>{t('estate.col.floorsSpaces')}</th><th>{t('estate.col.bookable')}</th><th style={{ textAlign: 'end' }}>{t('common.actions')}</th></tr>
                </thead>
                <tbody>
                  {paging.rows.map((b) => (
                    <tr key={b.id}>
                      <td>
                        <div style={{ fontWeight: 600 }}><bdi>{b.name}</bdi></div>
                        <div style={muted}>{[closedWeekdaysLabel(b.closedWeekdays), t('count.holiday', { count: b.holidays.length })].filter(Boolean).join(' · ')}</div>
                      </td>
                      <td className="mono" style={{ fontSize: 11.5 }}>{b.timeZone}</td>
                      <td className="mono" style={{ fontSize: 12 }}>{pad(b.openHour)}:00–{pad(b.closeHour)}:00</td>
                      <td className="mono" style={{ fontSize: 12 }}>{t('estate.lengthRange', { min: b.minBookingMinutes, max: b.maxBookingHours })}</td>
                      <td>{t('count.floor', { count: b.floorCount })}<div style={muted}>{t('count.space', { count: b.spaceCount })}</div></td>
                      <td><BookablePill bookable={b.isBookable} /></td>
                      <Actions>
                        <RowMenu items={[
                          ...when(can(P.Buildings.Edit), { label: t('common.edit'), onClick: () => modals.building(b) }),
                          ...when(can(P.Buildings.Edit), bookableItem('building', b.id, b.name, b.isBookable)),
                          ...when(can(P.Maintenance.Create), { label: t('estate.blockTime'), onClick: () => modals.maintenance({ scopeType: 'Building', scopeId: b.id, label: b.name }) }),
                        ]} />
                      </Actions>
                    </tr>
                  ))}
                </tbody>
              </table>
            </div>
            <Pagination page={paging.page} pageSize={paging.pageSize} total={paging.total} noun="building" onPage={paging.setPage} onPageSize={paging.setPageSize} />
          </>
        )}
      </RegistryCard>
    </section>
  )
}

/* ─── Floors ─── */

export function FloorsPage() {
  const { t } = useTranslation()
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
      <RegistryCard addLabel={t('estate.addFloor')} onAdd={can(P.Floors.Create) ? () => modals.floor() : undefined}>
        <div className="filter-bar">
          <div style={{ width: 220 }}>
            <label className="lbl" htmlFor="ff-building">{t('common.building')}</label>
            <Dropdown id="ff-building" value={buildingId} onChange={(v) => { setBuildingId(v); paging.setPage(1) }}
              options={[{ value: '', label: t('common.allBuildings') }, ...(buildings.data ?? []).map((b) => ({ value: b.id, label: b.name }))]} />
          </div>
          <button type="button" className="btn btn-sm" disabled={!buildingId} onClick={() => setBuildingId('')}>{t('common.clearFilters')}</button>
        </div>
        {q.isError ? <LoadError what={t('load.floors')} error={q.error} onRetry={() => q.refetch()} /> : !q.data ? <Loading /> : !q.data.length ? <p className="empty-note">{t('estate.noFloors')}{can(P.Floors.Create) && <button type="button" className="btn btn-primary btn-sm" style={gap} onClick={() => modals.floor()}>{t('estate.addFloor')}</button>}</p> : !shown.length ? (
          <p className="empty-note">
            {t('estate.buildingHasNoFloors')}
            <button type="button" className="btn btn-sm" style={gap} onClick={() => setBuildingId('')}>{t('common.clearFilters')}</button>
            {can(P.Floors.Create) && <button type="button" className="btn btn-primary btn-sm" style={gap} onClick={() => modals.floor()}>{t('estate.addFloor')}</button>}
          </p>
        ) : (
          <>
            <div className="table-scroll">
              <table className="grid">
                <thead>
                  <tr><th>{t('estate.col.floor')}</th><th>{t('estate.col.building')}</th><th>{t('estate.col.timeZone')}</th><th>{t('estate.col.hoursUtc')}</th><th>{t('estate.col.bookingLength')}</th><th>{t('estate.col.spaces')}</th><th>{t('estate.col.bookable')}</th><th style={{ textAlign: 'end' }}>{t('common.actions')}</th></tr>
                </thead>
                <tbody>
                  {paging.rows.map((f) => {
                    const b = byId[f.buildingId]
                    const overridden = [f.openHourOverride, f.closeHourOverride, f.minBookingMinutesOverride, f.maxBookingHoursOverride].some((v) => v != null)
                    const label = t('common.floorIn', { building: f.buildingName, floor: f.name })
                    return (
                      <tr key={f.id}>
                        <td style={{ fontWeight: 600 }}>{t('common.floorName', { name: f.name })}</td>
                        <td><bdi>{f.buildingName}</bdi></td>
                        <td className="mono" style={{ fontSize: 11.5 }}>{b?.timeZone ?? '—'}</td>
                        <td className="mono" style={{ fontSize: 12 }}>
                          {b ? `${pad(f.openHourOverride ?? b.openHour)}:00–${pad(f.closeHourOverride ?? b.closeHour)}:00` : '—'}
                          <div style={{ ...muted, fontFamily: 'inherit' }}>{overridden ? t('estate.ownRules') : t('estate.fromBuilding')}</div>
                        </td>
                        <td className="mono" style={{ fontSize: 12 }}>
                          {b ? t('estate.lengthRange', { min: f.minBookingMinutesOverride ?? b.minBookingMinutes, max: f.maxBookingHoursOverride ?? b.maxBookingHours }) : '—'}
                        </td>
                        <td>{t('count.space', { count: f.spaceCount })}</td>
                        <td>
                          <BookablePill bookable={f.isBookable && !!b?.isBookable} />
                          {f.isBookable && b && !b.isBookable && <div style={muted}>{t('estate.notBookableName', { name: b.name })}</div>}
                        </td>
                        <Actions>
                          <RowMenu items={[
                            ...when(can(P.Floors.Edit), { label: t('common.edit'), onClick: () => modals.floor(f) }),
                            ...when(can(P.Floors.Edit), bookableItem('floor', f.id, label, f.isBookable)),
                            ...when(can(P.Maintenance.Create), { label: t('estate.blockTime'), onClick: () => modals.maintenance({ scopeType: 'Floor', scopeId: f.id, label }) }),
                          ]} />
                        </Actions>
                      </tr>
                    )
                  })}
                </tbody>
              </table>
            </div>
            <Pagination page={paging.page} pageSize={paging.pageSize} total={paging.total} noun="floor" onPage={paging.setPage} onPageSize={paging.setPageSize} />
          </>
        )}
      </RegistryCard>
    </section>
  )
}

/* ─── Spaces (paged on the server) ─── */

export function SpacesPage() {
  const { t } = useTranslation()
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
      {can(P.Spaces.Create) && <PageActions><button type="button" className="btn btn-primary" onClick={() => modals.space()}>{t('estate.addSpace')}</button></PageActions>}
      <div className="card" style={{ overflow: 'hidden' }}>
        <SpaceRegistryFilters value={filters} onChange={changeFilters}
          buildings={buildings.data ?? []} floors={floors.data ?? []} types={types.data ?? []} />
        {q.isError ? <LoadError what={t('load.spaces')} error={q.error} onRetry={() => q.refetch()} /> : !q.data ? <Loading /> : !q.data.items.length ? (
          <p className="empty-note">
            {filtered ? t('estate.noSpacesMatch') : t('estate.noSpaces')}
            {filtered && <button type="button" className="btn btn-sm" style={gap} onClick={() => changeFilters(EMPTY_SPACE_FILTERS)}>{t('common.clearFilters')}</button>}
            {can(P.Spaces.Create) && <button type="button" className="btn btn-primary btn-sm" style={gap} onClick={() => modals.space()}>{t('estate.addSpace')}</button>}
          </p>
        ) : (
          <div className="table-scroll">
            <table className="grid" style={{ opacity: q.isPlaceholderData ? 0.6 : 1 }}>
              <thead>
                <tr><th>{t('estate.col.space')}</th><th>{t('estate.col.type')}</th><th>{t('estate.col.location')}</th><th>{t('estate.col.timeZone')}</th><th>{t('estate.col.bookable')}</th><th style={{ textAlign: 'end' }}>{t('common.actions')}</th></tr>
              </thead>
              <tbody>
                {q.data.items.map((s) => (
                  <tr key={s.id}>
                    <td><div style={{ fontWeight: 600 }}><bdi>{s.name}</bdi></div><div style={muted}>{s.note ? <bdi>{s.note}</bdi> : t('estate.noDescription')}</div></td>
                    <td><bdi>{s.typeName}</bdi></td>
                    <td><bdi>{s.buildingName}</bdi><div style={muted}>{t('common.floorName', { name: s.floorName })}</div></td>
                    <td className="mono" style={{ fontSize: 11.5, color: 'var(--slate)' }}>{s.timeZone}</td>
                    <td>
                      <BookablePill bookable={s.canCurrentUserBook} reason={s.notBookableReason} />
                      {s.isBookable && !s.canCurrentUserBook && <div style={{ ...muted, marginTop: 3 }}>{t('estate.blockedByParent')}</div>}
                      <div style={{ ...muted, marginTop: 3 }}>{t('estate.upcoming', { count: q.data.upcomingBookingCounts[s.id] ?? 0 })}</div>
                    </td>
                    <Actions>
                      <RowMenu items={[
                        ...when(can(P.Spaces.Edit), { label: t('common.edit'), onClick: () => modals.space(s) }),
                        ...when(can(P.Spaces.Edit), bookableItem('space', s.id, s.name, s.isBookable)),
                        ...when(can(P.Maintenance.Create), { label: t('estate.blockTime'), onClick: () => modals.maintenance({ scopeType: 'Space', scopeId: s.id, label: s.name }) }),
                      ]} />
                    </Actions>
                  </tr>
                ))}
              </tbody>
            </table>
          </div>
        )}
        {q.data && total > 0 && (
          <Pagination page={page} pageSize={pageSize} total={total} noun="space" onPage={setPage} onPageSize={changePageSize} />
        )}
      </div>
    </section>
  )
}

/* ─── Space types ─── */

export function SpaceTypesPage() {
  const { t } = useTranslation()
  const q = useSpaceTypes()
  const { can } = useSession()
  const del = useDeleteSpaceType()
  const paging = useClientPaging(q.data ?? [])

  const remove = (st: SpaceType) => {
    if (st.spaceCount > 0) {
      toast('warn', t('estate.toast.typeInUse', { name: st.name }), t('estate.toast.typeInUseMessage', { count: st.spaceCount }), 'space_type.in_use')
      return
    }
    if (!window.confirm(t('estate.confirmDeleteType', { name: st.name }))) return
    del.mutateAsync(st.id).then(
      () => toast('ok', t('estate.toast.typeDeleted'), st.name),
      (e) => { const { code, message } = errorText(e); toast('err', t('common.requestRejected'), message, code) })
  }

  return (
    <section>
      <RegistryCard addLabel={t('estate.addSpaceType')} onAdd={can(P.SpaceTypes.Create) ? () => modals.spaceType() : undefined}>
        {q.isError ? <LoadError what={t('load.spaceTypes')} error={q.error} onRetry={() => q.refetch()} /> : !q.data ? <Loading /> : !q.data.length ? <p className="empty-note">{t('estate.noSpaceTypes')}{can(P.SpaceTypes.Create) && <button type="button" className="btn btn-primary btn-sm" style={gap} onClick={() => modals.spaceType()}>{t('estate.addSpaceType')}</button>}</p> : (
          <>
            <div className="table-scroll">
              <table className="grid">
                <thead>
                  <tr><th>{t('estate.col.type')}</th><th>{t('estate.col.spacesUsingIt')}</th><th style={{ textAlign: 'end' }}>{t('common.actions')}</th></tr>
                </thead>
                <tbody>
                  {paging.rows.map((st) => (
                    <tr key={st.id}>
                      <td style={{ fontWeight: 600 }}><bdi>{st.name}</bdi></td>
                      <td>{t('count.space', { count: st.spaceCount })}</td>
                      <Actions>
                        <RowMenu items={[
                          ...when(can(P.SpaceTypes.Edit), { label: t('common.edit'), onClick: () => modals.spaceType(st) }),
                          ...when(can(P.SpaceTypes.Delete), { label: t('common.delete'), onClick: () => remove(st) }),
                        ]} />
                      </Actions>
                    </tr>
                  ))}
                </tbody>
              </table>
            </div>
            <Pagination page={paging.page} pageSize={paging.pageSize} total={paging.total} noun="spaceType" onPage={paging.setPage} onPageSize={paging.setPageSize} />
          </>
        )}
      </RegistryCard>
    </section>
  )
}
