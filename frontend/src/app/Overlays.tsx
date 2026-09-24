import { BookingModal } from '../features/bookings/BookingModal'
import { DayDrawer } from '../features/bookings/DayDrawer'
import { DetailDrawer } from '../features/bookings/DetailDrawer'
import { BuildingFormModal, FloorFormModal, SpaceFormModal } from '../features/spaces/EstateForms'
import { MaintenanceFormModal } from '../features/spaces/MaintenanceFormModal'
import { useModalStore } from '../state/modalStore'

export function Overlays() {
  const overlay = useModalStore((s) => s.overlay)
  if (!overlay) return null
  switch (overlay.kind) {
    case 'booking':
      return <BookingModal key={overlay.editing?.id ?? 'new'} prefill={overlay.prefill} editing={overlay.editing} />
    case 'detail':
      return <DetailDrawer key={overlay.id} entity={overlay.entity} id={overlay.id} />
    case 'day':
      return <DayDrawer dayKeyValue={overlay.key} scheduleId={overlay.scheduleId} />
    case 'building':
      return <BuildingFormModal editing={overlay.editing} />
    case 'floor':
      return <FloorFormModal editing={overlay.editing} />
    case 'space':
      return <SpaceFormModal editing={overlay.editing} />
    case 'maintenance':
      return <MaintenanceFormModal target={overlay.target} />
  }
}
