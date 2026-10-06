import { useTranslation } from 'react-i18next'
import { useBuildings, useFloors, useSpaces, useUsers } from '../../../api/hooks'
import { displayName, type Space } from '../../../api/types'
import { DatePicker, Dropdown } from '../../../components/pickers'
import { useSession } from '../../../app/session'
import { P } from '../../../auth/permissions'
import { dayAt, formatDate, formatDateRange, todayKey } from '../../../lib/dateUtils'
import type { ScheduleId } from '../../../state/modalStore'
import { useDropUnknown } from '../../../lib/useUrlState'
import { EVERYONE, useScheduleConfig, type ScheduleConfig, type ScheduleMode } from '../../../state/scheduleStore'

/* "Wed 30 Sept 2026" for a day, "1–30 Sept 2026" or "28 Sept – 4 Oct 2026" for a range: Intl leaves out
 * whatever the two ends share, in the chosen language's own order. */
function periodLabel(cfg: ScheduleConfig): string {
  const a = dayAt(cfg.from)
  const b = dayAt(cfg.to)
  if (cfg.mode === 'day') return formatDate(a, { weekday: 'short', day: 'numeric', month: 'short', year: 'numeric' })
  return formatDateRange(a, b, { day: 'numeric', month: 'short', year: 'numeric' })
}

export function ScheduleToolbar({ id, spaces }: { id: ScheduleId; spaces: Space[] }) {
  const { t } = useTranslation()
  const [cfg, store] = useScheduleConfig(id)
  const session = useSession()
  /* The full toolbar (space, person, From / To) is for whoever may see everyone's bookings. */
  const seesAll = session.can(P.Bookings.ViewAll)
  /* The person list needs ABP's "see users" permission too; without it the filter offers Everyone and You. */
  const listsUsers = seesAll && session.can(P.Users.Default)
  const users = useUsers(listsUsers)
  const buildingsQ = useBuildings()
  const buildings = buildingsQ.data ?? []
  /* Floor stays locked until a building is picked, then the server is asked for that building's floors;
   * Space likewise waits for a floor and lists only that floor's spaces. */
  const floorsQ = useFloors(cfg.buildingId, !!cfg.buildingId)
  const floors = floorsQ.data ?? []
  const spacesQ = useSpaces({ floorId: cfg.floorId }, seesAll && !!cfg.floorId)
  const shownSpaces = spacesQ.data ?? []
  /* A shared link with a building, floor, space or person this viewer can't pick falls back to the default. */
  useDropUnknown(cfg.buildingId, buildingsQ.data?.map((b) => b.id), () => store.patch({ buildingId: '', floorId: '', spaceId: 'all' }))
  useDropUnknown(cfg.floorId, floorsQ.data?.map((f) => f.id), () => store.patch({ floorId: '', spaceId: 'all' }))
  useDropUnknown(cfg.spaceId === 'all' ? '' : cfg.spaceId, seesAll ? spacesQ.data?.map((s) => s.id) : undefined, () => store.patch({ spaceId: 'all' }))
  useDropUnknown(cfg.userId === EVERYONE ? '' : cfg.userId,
    seesAll ? (listsUsers ? users.data?.map((u) => u.id) : [session.userId]) : undefined, () => store.patch({ userId: EVERYONE }))
  const inScope = (s: Space, buildingId: string, floorId: string) =>
    (!buildingId || s.buildingId === buildingId) && (!floorId || s.floorId === floorId)
  const keepSpace = (buildingId: string, floorId: string) =>
    cfg.spaceId === 'all' || spaces.some((s) => s.id === cfg.spaceId && inScope(s, buildingId, floorId)) ? cfg.spaceId : 'all'
  /* Space stays locked until a floor is picked, so clearing the building or floor also clears the space. */
  const pickBuilding = (buildingId: string) => store.patch({ buildingId, floorId: '', spaceId: 'all' })
  const pickFloor = (floorId: string) => store.patch({ floorId, spaceId: floorId ? keepSpace(cfg.buildingId, floorId) : 'all' })

  const building = (
    <div style={{ minWidth: 200 }}>
      <label className="lbl" htmlFor={`${id}-building`}>{t('common.building')}</label>
      <Dropdown id={`${id}-building`} value={cfg.buildingId} onChange={pickBuilding}
        options={[{ value: '', label: t('common.allBuildings') }, ...buildings.map((b) => ({ value: b.id, label: b.name }))]} />
    </div>
  )
  const floor = (
    <div style={{ minWidth: 170 }}>
      <label className="lbl" htmlFor={`${id}-floor`}>{t('common.floor')}</label>
      <Dropdown id={`${id}-floor`} value={cfg.floorId} onChange={pickFloor} disabled={!cfg.buildingId} placeholder={t('common.chooseBuildingFirst')}
        options={cfg.buildingId ? [{ value: '', label: t('common.allFloors') }, ...floors.map((f) => ({ value: f.id, label: t('common.floorName', { name: f.name }) }))] : []} />
    </div>
  )

  const views = (
    <div className="seg" role="group" aria-label={t('schedule.viewLabel')}>
      {(['month', 'week', 'day'] as ScheduleMode[]).map((m) => (
        <button key={m} type="button" className={cfg.mode === m ? 'active' : ''} onClick={() => store.setMode(m)}>
          {t(`schedule.view.${m}`)}
        </button>
      ))}
    </div>
  )
  const period = (
    <div className="sched-period">
      <button type="button" className="iconbtn" onClick={() => store.shift(-1)} aria-label={t('schedule.previousPeriod')} title={t('schedule.previousPeriod')}>‹</button>
      <span className="mono period-label">{periodLabel(cfg)}</span>
      <button type="button" className="iconbtn" onClick={() => store.shift(1)} aria-label={t('schedule.nextPeriod')} title={t('schedule.nextPeriod')}>›</button>
      <button type="button" className="btn btn-sm" onClick={() => store.setPeriod(todayKey())}>{t('common.today')}</button>
    </div>
  )

  /* Employees: the building and floor on top; Month / Week / Day on the left and the period on the far right under it. */
  if (!seesAll) {
    return (
      <div className="sched-toolbar">
        <div className="sched-row sched-row-left">{building}{floor}</div>
        <div className="sched-row">{views}{period}</div>
      </div>
    )
  }

  /* Admins: Building, Floor, Space and User on top; under them Month / Week / Day on the left, the period in the middle and the From / To range on the far right. */
  return (
    <div className="sched-toolbar">
      <div className="sched-row sched-row-top">
        {building}
        {floor}
        <div style={{ minWidth: 230 }}>
          <label className="lbl" htmlFor="my-space">{t('common.space')}</label>
          <Dropdown id="my-space" value={cfg.floorId ? cfg.spaceId : ''} onChange={(v) => store.patch({ spaceId: v })}
            disabled={!cfg.floorId} placeholder={t('common.chooseFloorFirst')}
            options={cfg.floorId ? [{ value: 'all', label: t('schedule.allSpacesOnFloor') }, ...shownSpaces.map((s) => ({ value: s.id, label: s.name }))] : []} />
        </div>
        <div>
          <label className="lbl" htmlFor="my-user">{t('schedule.user')}</label>
          <Dropdown id="my-user" style={{ width: 170 }} value={cfg.userId}
            onChange={(v) => store.patch({ userId: v })}
            options={[
              { value: EVERYONE, label: t('schedule.everyone') },
              ...(users.data
                ? users.data.map((u) => ({ value: u.id, label: u.id === session.userId ? t('common.you') : displayName(u.name, u.surname, u.userName) }))
                : [{ value: session.userId, label: t('common.you') }]),
            ]} />
        </div>
      </div>
      <div className="sched-row sched-row-spread">
        {views}
        {period}
        <div className="sched-range">
          <div>
            <label className="lbl" htmlFor={`${id}-from`}>{t('common.from')}</label>
            <DatePicker id={`${id}-from`} style={{ width: 180 }} value={cfg.from}
              onChange={(v) => store.onRangeInput(v, cfg.to)} />
          </div>
          <div>
            <label className="lbl" htmlFor={`${id}-to`}>{t('common.to')}</label>
            <DatePicker id={`${id}-to`} style={{ width: 180 }} value={cfg.to} min={cfg.from}
              onChange={(v) => store.onRangeInput(cfg.from, v)} />
          </div>
        </div>
      </div>
    </div>
  )
}
