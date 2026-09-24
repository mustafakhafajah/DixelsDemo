import { useCancelBooking, useCancelBookingSeries, useCancelMaintenance, useEndBookingEarly } from '../../api/hooks'
import { errorText } from '../../api/client'
import type { Booking } from '../../api/types'
import { hm, stamp } from '../../lib/dateUtils'
import { toast } from '../../state/toastStore'

export function useBookingActions() {
  const cancel = useCancelBooking()
  const cancelSeries = useCancelBookingSeries()
  const endEarly = useEndBookingEarly()
  const cancelMaint = useCancelMaintenance()

  const fail = (title: string) => (e: unknown) => {
    const { code, message } = errorText(e)
    toast('err', title, message, code)
  }

  return {
    busy: cancel.isPending || cancelSeries.isPending || endEarly.isPending || cancelMaint.isPending,
    cancel: (b: Booking) =>
      cancel.mutateAsync(b.id).then(() => toast('ok', 'Booking cancelled', `${b.spaceName} · the slot is free again.`), fail('Cancel failed')),
    cancelSeriesFrom: (b: Booking) =>
      cancelSeries.mutateAsync(b.id).then(
        (r) => toast('ok', `${r.cancelledCount} occurrences cancelled`, `Series from ${stamp(b.start)} onward.`),
        fail('Cancel failed')),
    endEarly: (b: Booking) =>
      endEarly.mutateAsync(b.id).then(
        () => toast('ok', 'Booking ended', `Ended at ${hm(new Date())} UTC. The rest of the window is free.`),
        fail('Could not end booking')),
    cancelMaintenance: (id: string) =>
      cancelMaint.mutateAsync(id).then(() => toast('ok', 'Time unblocked', 'It can be booked again.'), fail('Request rejected')),
  }
}
