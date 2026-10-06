import { type ReactNode } from 'react'
import { useTranslation } from 'react-i18next'
import i18n from 'i18next'
import { errorText } from '../../api/client'
import {
  useBuildings, useBuildingsPage, useDeleteSpaceType, useFloors, useFloorsPage, useSetBookable, useSpaceRegistry, useSpaceTypes,
  useSpaceTypesPage, type EstateKind,
} from '../../api/hooks'
import type { SpaceType } from '../../api/types'
import { PageActions } from '../../app/pageActions'
import { useSession } from '../../app/session'
import { P } from '../../auth/permissions'
import { BookablePill, LoadError, Loading } from '../../components/bits'
import { Dropdown } from '../../components/pickers'
import { EmptyState } from '../../components/EmptyState'
import { Pagination } from '../../components/Pagination'
import { RowMenu } from '../../components/RowMenu'
import { closedWeekdaysLabel } from '../../lib/closedDays'
import { pad } from '../../lib/dateUtils'
import { useServerPaging, useStayOnRealPage } from '../../lib/useServerPaging'
import { urlParam, useDropUnknown, useUrlSearchBox, useUrlState } from '../../lib/useUrlState'
import { modals } from '../../state/modalStore'
import { useFilteredNavCount } from '../../state/navCountStore'
import { toast } from '../../state/toastStore'
import { SpaceRegistryFilters, type SpaceRegistryFilterValues } from './SpaceRegistryFilters'

const EMPTY_SPACE_FILTERS: SpaceRegistryFilterValues = { buildingId: '', floorId: '', name: '', typeId: '' }

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

function Actions({ children }: { children: ReactNode }) {
  return <td style={{ textAlign: 'end' }}><div style={{ display: 'inline-block' }}>{children}</div></td>
}

/* ─── Buildings ─── */

export function BuildingsPage() {
  const { t } = useTranslation()
  const paging = useServerPaging()
  const q = useBuildingsPage({ page: paging.page, pageSize: paging.pageSize })
  useStayOnRealPage(paging, q.data?.totalCount)
  const { can } = useSession()
  const bookableItem = useBookableMenuItem()
  return (
    <section>
      <RegistryCard addLabel={t('estate.addBuilding')} onAdd={can(P.Buildings.Create) ? () => modals.building() : undefined}>
        {q.isError ? <LoadError what={t('load.buildings')} error={q.error} onRetry={() => q.refetch()} /> : !q.data ? <Loading /> : !q.data.totalCount ? (
          <EmptyState art="building" title={t('empty.buildings.title')} text={t('empty.buildings.text')}
            action={can(P.Buildings.Create) ? { label: t('estate.addBuilding'), onClick: () => modals.building() } : undefined} />
        ) : (
          <>
            <div className="table-scroll">
              <table className="grid" style={{ opacity: q.isPlaceholderData ? 0.6 : 1 }}>
                <thead>
                  <tr><th>{t('estate.col.building')}</th><th>{t('estate.col.timeZone')}</th><th>{t('estate.col.hoursUtc')}</th><th>{t('estate.col.bookingLength')}</th><th>{t('estate.col.floorsSpaces')}</th><th>{t('estate.col.bookable')}</th><th style={{ textAlign: 'end' }}>{t('common.actions')}</th></tr>
                </thead>
                <tbody>
                  {q.data.items.map((b) => (
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
            <Pagination page={paging.page} pageSize={paging.pageSize} total={q.data.totalCount} noun="building" onPage={paging.setPage} onPageSize={paging.setPageSize} />
          </>
        )}
      </RegistryCard>
    </section>
  )
}

/* ─── Floors ─── */

export function FloorsPage() {
  const { t } = useTranslation()
  const buildings = useBuildings()
  const { can } = useSession()
  const bookableItem = useBookableMenuItem()
  /* The building filter and the page are both sent to the server, so each change is a new request.
   * Both are in the address (?building=…&page=…), so a copied link shows the same list. */
  const [{ buildingId }, setUrl] = useUrlState({ buildingId: urlParam.text('building') })
  const paging = useServerPaging()
  const q = useFloorsPage({ page: paging.page, pageSize: paging.pageSize, buildingId: buildingId || undefined })
  useStayOnRealPage(paging, q.data?.totalCount)
  useFilteredNavCount('floors', buildingId && q.data ? q.data.totalCount : undefined)
  const pickBuilding = (v: string) => { setUrl({ buildingId: v }); paging.setPage(1) }
  useDropUnknown(buildingId, buildings.data?.map((b) => b.id), () => pickBuilding(''))
  const byId = Object.fromEntries((buildings.data ?? []).map((b) => [b.id, b]))
  return (
    <section>
      <RegistryCard addLabel={t('estate.addFloor')} onAdd={can(P.Floors.Create) ? () => modals.floor() : undefined}>
        <div className="filter-bar">
          <div style={{ width: 220 }}>
            <label className="lbl" htmlFor="ff-building">{t('common.building')}</label>
            <Dropdown id="ff-building" value={buildingId} onChange={pickBuilding}
              options={[{ value: '', label: t('common.allBuildings') }, ...(buildings.data ?? []).map((b) => ({ value: b.id, label: b.name }))]} />
          </div>
        </div>
        {q.isError ? <LoadError what={t('load.floors')} error={q.error} onRetry={() => q.refetch()} /> : !q.data ? <Loading /> : !q.data.totalCount && !buildingId ? (
          <EmptyState art="floor" title={t('empty.floors.title')} text={t('empty.floors.text')}
            action={can(P.Floors.Create) ? { label: t('estate.addFloor'), onClick: () => modals.floor() } : undefined} />
        ) : !q.data.totalCount ? (
          <EmptyState art="floor" title={t('empty.buildingFloors.title', { building: byId[buildingId]?.name ?? '' })}
            text={t('empty.buildingFloors.text')} action={{ label: t('common.clearFilters'), onClick: () => pickBuilding('') }} />
        ) : (
          <>
            <div className="table-scroll">
              <table className="grid" style={{ opacity: q.isPlaceholderData ? 0.6 : 1 }}>
                <thead>
                  <tr><th>{t('estate.col.floor')}</th><th>{t('estate.col.building')}</th><th>{t('estate.col.timeZone')}</th><th>{t('estate.col.hoursUtc')}</th><th>{t('estate.col.bookingLength')}</th><th>{t('estate.col.spaces')}</th><th>{t('estate.col.bookable')}</th><th style={{ textAlign: 'end' }}>{t('common.actions')}</th></tr>
                </thead>
                <tbody>
                  {q.data.items.map((f) => {
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
            <Pagination page={paging.page} pageSize={paging.pageSize} total={q.data.totalCount} noun="floor" onPage={paging.setPage} onPageSize={paging.setPageSize} />
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
  const types = useSpaceTypes()
  const { can } = useSession()
  const bookableItem = useBookableMenuItem()
  /* Every filter and the page live in the address (?q=…&building=…&floor=…&type=…&page=…), so a copied link
   * shows the same list. The typed name reaches the address (and the server) once typing pauses; dropdowns at once. */
  const [url, setUrl] = useUrlState({
    name: urlParam.text('q'), buildingId: urlParam.text('building'), floorId: urlParam.text('floor'), typeId: urlParam.text('type'),
  })
  const paging = useServerPaging()
  const { page, pageSize, setPage } = paging
  const [nameText, setNameText] = useUrlSearchBox(url.name, (name) => { setUrl({ name }); setPage(1) })
  const filters: SpaceRegistryFilterValues = { ...url, name: nameText }
  /* The floor list is asked for the chosen building. */
  const floors = useFloors(url.buildingId, !!url.buildingId)

  const q = useSpaceRegistry({
    page, pageSize, name: url.name || undefined,
    buildingId: url.buildingId || undefined, floorId: (url.buildingId && url.floorId) || undefined, typeId: url.typeId || undefined,
  })
  const total = q.data?.totalCount ?? 0
  useStayOnRealPage(paging, q.data?.totalCount)

  const changeFilters = (v: SpaceRegistryFilterValues) => {
    setNameText(v.name)
    if (v.buildingId !== url.buildingId || v.floorId !== url.floorId || v.typeId !== url.typeId || (!v.name && url.name)) {
      setUrl({ buildingId: v.buildingId, floorId: v.floorId, typeId: v.typeId, ...(v.name ? {} : { name: '' }) })
      setPage(1)
    }
  }
  /* A shared link with a building, floor or type this viewer does not have falls back to "all". */
  useDropUnknown(url.buildingId, buildings.data?.map((b) => b.id), () => setUrl({ buildingId: '', floorId: '' }))
  useDropUnknown(url.floorId, url.buildingId ? floors.data?.map((f) => f.id) : [], () => setUrl({ floorId: '' }))
  useDropUnknown(url.typeId, types.data?.map((t) => t.id), () => setUrl({ typeId: '' }))
  const changePageSize = paging.setPageSize
  const filtered = Object.values(filters).some((v) => v !== '')
  useFilteredNavCount('spaces', filtered && q.data ? total : undefined)

  return (
    <section>
      {can(P.Spaces.Create) && <PageActions><button type="button" className="btn btn-primary" onClick={() => modals.space()}>{t('estate.addSpace')}</button></PageActions>}
      <div className="card" style={{ overflow: 'hidden' }}>
        <SpaceRegistryFilters value={filters} onChange={changeFilters}
          buildings={buildings.data ?? []} floors={floors.data ?? []} types={types.data ?? []} />
        {q.isError ? <LoadError what={t('load.spaces')} error={q.error} onRetry={() => q.refetch()} /> : !q.data ? <Loading /> : !q.data.items.length ? (
          filtered
            ? <EmptyState art="space" title={t('empty.spacesFiltered.title')} text={t('empty.spacesFiltered.text')}
                action={{ label: t('common.clearFilters'), onClick: () => changeFilters(EMPTY_SPACE_FILTERS) }} />
            : <EmptyState art="space" title={t('empty.spaces.title')} text={t('empty.spaces.text')}
                action={can(P.Spaces.Create) ? { label: t('estate.addSpace'), onClick: () => modals.space() } : undefined} />
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
                      <div style={{ ...muted, marginTop: 3 }}>{t('estate.upcoming', { count: s.upcomingBookingCount })}</div>
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
  const paging = useServerPaging()
  const q = useSpaceTypesPage({ page: paging.page, pageSize: paging.pageSize })
  useStayOnRealPage(paging, q.data?.totalCount)
  const { can } = useSession()
  const del = useDeleteSpaceType()

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
        {q.isError ? <LoadError what={t('load.spaceTypes')} error={q.error} onRetry={() => q.refetch()} /> : !q.data ? <Loading /> : !q.data.totalCount ? (
          <EmptyState art="spaceType" title={t('empty.spaceTypes.title')} text={t('empty.spaceTypes.text')}
            action={can(P.SpaceTypes.Create) ? { label: t('estate.addSpaceType'), onClick: () => modals.spaceType() } : undefined} />
        ) : (
          <>
            <div className="table-scroll">
              <table className="grid" style={{ opacity: q.isPlaceholderData ? 0.6 : 1 }}>
                <thead>
                  <tr><th>{t('estate.col.type')}</th><th>{t('estate.col.spacesUsingIt')}</th><th style={{ textAlign: 'end' }}>{t('common.actions')}</th></tr>
                </thead>
                <tbody>
                  {q.data.items.map((st) => (
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
            <Pagination page={paging.page} pageSize={paging.pageSize} total={q.data.totalCount} noun="spaceType" onPage={paging.setPage} onPageSize={paging.setPageSize} />
          </>
        )}
      </RegistryCard>
    </section>
  )
}
