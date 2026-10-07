import { BookingModal } from '../features/bookings/BookingModal'
import { DayDrawer } from '../features/bookings/DayDrawer'
import { DetailDrawer } from '../features/bookings/DetailDrawer'
import { BuildingFormModal, FloorFormModal, SpaceFormModal, SpaceTypeFormModal } from '../features/spaces/EstateForms'
import { NotBookableModal } from '../features/spaces/Bookable'
import { MaintenanceFormModal } from '../features/spaces/MaintenanceFormModal'
import { P } from '../auth/permissions'
import { useModalStore } from '../state/modalStore'
import { useSession } from './session'

export function Overlays() {
  const overlay = useModalStore((s) => s.overlay)
  const { can } = useSession()
  if (!overlay) return null
  /* A form whose permission was taken away while it was open simply goes away. */
  const needs: Partial<Record<typeof overlay.kind, string>> = {
    booking: overlay.kind === 'booking' && overlay.editing ? P.Bookings.Edit : P.Bookings.Create,
    building: overlay.kind === 'building' && overlay.editing ? P.Buildings.Edit : P.Buildings.Create,
    floor: overlay.kind === 'floor' && overlay.editing ? P.Floors.Edit : P.Floors.Create,
    space: overlay.kind === 'space' && overlay.editing ? P.Spaces.Edit : P.Spaces.Create,
    spaceType: overlay.kind === 'spaceType' && overlay.editing ? P.SpaceTypes.Edit : P.SpaceTypes.Create,
    maintenance: P.Maintenance.Create,
  }
  const need = needs[overlay.kind]
  if (need && !can(need)) return null
  switch (overlay.kind) {
    case 'booking':
      return <BookingModal key={overlay.editing?.id ?? 'new'} prefill={overlay.prefill} editing={overlay.editing} />
    case 'detail':
      return <DetailDrawer key={overlay.id} entity={overlay.entity} id={overlay.id} refuse={overlay.refuse} />
    case 'day':
      return <DayDrawer dayKeyValue={overlay.key} scheduleId={overlay.scheduleId} />
    case 'building':
      return <BuildingFormModal editing={overlay.editing} />
    case 'floor':
      return <FloorFormModal editing={overlay.editing} />
    case 'space':
      return <SpaceFormModal editing={overlay.editing} />
    case 'spaceType':
      return <SpaceTypeFormModal editing={overlay.editing} />
    case 'maintenance':
      return <MaintenanceFormModal target={overlay.target} />
    case 'notBookable':
      return <NotBookableModal target={overlay.target} />
  }
}
