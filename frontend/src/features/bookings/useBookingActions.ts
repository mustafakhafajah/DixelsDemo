import { useTranslation } from 'react-i18next'
import { useCancelBooking, useCancelBookingSeries, useCancelMaintenance, useEndBookingEarly, useRespondToBooking } from '../../api/hooks'
import { errorText } from '../../api/client'
import type { Booking, CancelMessage, Reply } from '../../api/types'
import { hm, stamp } from '../../lib/dateUtils'
import { toast } from '../../state/toastStore'

export function useBookingActions() {
  const { t } = useTranslation()
  const cancel = useCancelBooking()
  const cancelSeries = useCancelBookingSeries()
  const endEarly = useEndBookingEarly()
  const cancelMaint = useCancelMaintenance()
  const respond = useRespondToBooking()

  const fail = (title: string) => (e: unknown) => {
    const { code, message } = errorText(e)
    toast('err', title, message, code)
  }
  /* Resolves to true when it worked, so a confirm step knows whether to close. */
  const done = <T>(p: Promise<T>, ok: (r: T) => void, failTitle: string) =>
    p.then((r) => { ok(r); return true }, (e: unknown) => { fail(failTitle)(e); return false })

  return {
    busy: cancel.isPending || cancelSeries.isPending || endEarly.isPending || cancelMaint.isPending || respond.isPending,
    /* An invited person's answer, like Teams' Accept / Tentative / Decline: for this booking, or with wholeSeries
     * for its later dates too. The owner gets an email either way. */
    respond: (b: Booking, response: Reply, wholeSeries = false) =>
      done(respond.mutateAsync({ id: b.id, response, wholeSeries }),
        () => toast('ok', t(`actions.replied.${response}`),
          t(wholeSeries ? 'actions.repliedSeriesMessage' : 'actions.repliedMessage', { space: b.spaceName, owner: b.ownerName })),
        t('actions.replyFailed')),
    cancel: (b: Booking, message?: CancelMessage) =>
      done(cancel.mutateAsync({ id: b.id, message }),
        () => toast('ok', t('actions.cancelled'), t('actions.cancelledMessage', { space: b.spaceName })), t('actions.cancelFailed')),
    cancelSeriesFrom: (b: Booking, message?: CancelMessage) =>
      done(cancelSeries.mutateAsync({ id: b.id, seriesId: b.seriesId, start: b.start, message }),
        (r) => toast('ok', t('actions.seriesCancelled', { count: r.cancelledCount }), t('actions.seriesCancelledMessage', { from: stamp(b.start) })),
        t('actions.cancelFailed')),
    endEarly: (b: Booking) =>
      endEarly.mutateAsync(b.id).then(
        () => toast('ok', t('actions.ended'), t('actions.endedMessage', { time: hm(new Date()) })),
        fail(t('actions.endFailed'))),
    cancelMaintenance: (id: string) =>
      cancelMaint.mutateAsync(id).then(() => toast('ok', t('actions.unblocked'), t('actions.unblockedMessage')), fail(t('common.requestRejected'))),
  }
}
