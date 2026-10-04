import { useTranslation } from 'react-i18next'
import { useCancelBooking, useCancelBookingSeries, useCancelMaintenance, useEndBookingEarly } from '../../api/hooks'
import { errorText } from '../../api/client'
import type { Booking } from '../../api/types'
import { hm, stamp } from '../../lib/dateUtils'
import { toast } from '../../state/toastStore'

export function useBookingActions() {
  const { t } = useTranslation()
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
      cancel.mutateAsync(b.id).then(() => toast('ok', t('actions.cancelled'), t('actions.cancelledMessage', { space: b.spaceName })), fail(t('actions.cancelFailed'))),
    cancelSeriesFrom: (b: Booking) =>
      cancelSeries.mutateAsync(b.id).then(
        (r) => toast('ok', t('actions.seriesCancelled', { count: r.cancelledCount }), t('actions.seriesCancelledMessage', { from: stamp(b.start) })),
        fail(t('actions.cancelFailed'))),
    endEarly: (b: Booking) =>
      endEarly.mutateAsync(b.id).then(
        () => toast('ok', t('actions.ended'), t('actions.endedMessage', { time: hm(new Date()) })),
        fail(t('actions.endFailed'))),
    cancelMaintenance: (id: string) =>
      cancelMaint.mutateAsync(id).then(() => toast('ok', t('actions.unblocked'), t('actions.unblockedMessage')), fail(t('common.requestRejected'))),
  }
}
