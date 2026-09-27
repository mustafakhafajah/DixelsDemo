import { create } from 'zustand'
import type { Booking, Building, Floor, MaintenanceScopeType, Space, SpaceType } from '../api/types'

export interface BookingPrefill {
  spaceId?: string
  start?: Date
  end?: Date
}

export type ScheduleId = 'my'

/* What the 'Make not bookable' dialog acts on. */
export interface BookableTarget {
  kind: 'building' | 'floor' | 'space'
  id: string
  label: string
}

export interface MaintenanceTarget {
  scopeType: MaintenanceScopeType
  scopeId: string
  label: string
}

/* Which overlay is open and with what context. Opening one closes the others, like the mock's
 * sequential closeX();openY() calls. */
type Overlay =
  | { kind: 'booking'; prefill: BookingPrefill; editing: Booking | null }
  | { kind: 'detail'; entity: 'booking' | 'maintenance'; id: string }
  | { kind: 'day'; key: string; scheduleId: ScheduleId }
  | { kind: 'building'; editing: Building | null }
  | { kind: 'floor'; editing: Floor | null }
  | { kind: 'space'; editing: Space | null }
  | { kind: 'spaceType'; editing: SpaceType | null }
  | { kind: 'maintenance'; target: MaintenanceTarget }
  | { kind: 'notBookable'; target: BookableTarget }

interface ModalState {
  overlay: Overlay | null
  open: (o: Overlay) => void
  close: () => void
}

export const useModalStore = create<ModalState>((set) => ({
  overlay: null,
  open: (overlay) => set({ overlay }),
  close: () => set({ overlay: null }),
}))

export const modals = {
  booking: (prefill: BookingPrefill = {}, editing: Booking | null = null) =>
    useModalStore.getState().open({ kind: 'booking', prefill, editing }),
  reschedule: (b: Booking) =>
    useModalStore.getState().open({ kind: 'booking', prefill: { spaceId: b.spaceId, start: b.start, end: b.end }, editing: b }),
  detail: (entity: 'booking' | 'maintenance', id: string) => useModalStore.getState().open({ kind: 'detail', entity, id }),
  day: (key: string, scheduleId: ScheduleId) => useModalStore.getState().open({ kind: 'day', key, scheduleId }),
  building: (editing: Building | null = null) => useModalStore.getState().open({ kind: 'building', editing }),
  floor: (editing: Floor | null = null) => useModalStore.getState().open({ kind: 'floor', editing }),
  space: (editing: Space | null = null) => useModalStore.getState().open({ kind: 'space', editing }),
  spaceType: (editing: SpaceType | null = null) => useModalStore.getState().open({ kind: 'spaceType', editing }),
  maintenance: (target: MaintenanceTarget) => useModalStore.getState().open({ kind: 'maintenance', target }),
  notBookable: (target: BookableTarget) => useModalStore.getState().open({ kind: 'notBookable', target }),
  close: () => useModalStore.getState().close(),
}
