import i18n from 'i18next'
import { errorText } from '../../api/client'
import { useCancelUpcoming, type EstateKind } from '../../api/hooks'
import type { CancelMessage, MaintenanceScopeType } from '../../api/types'
import { bulkCancelDefaults } from '../../components/cancelMessage'
import { toast } from '../../state/toastStore'

export const scopeTypeOf = (kind: EstateKind): MaintenanceScopeType =>
  kind === 'building' ? 'Building' : kind === 'floor' ? 'Floor' : 'Space'

/* After saving an item as not bookable: cancel its upcoming bookings if the admin asked to. message is the email
 * everyone on them gets; left out (not edited), the ready-made text is sent. */
export function useCancelUpcomingAfterSave() {
  const cancel = useCancelUpcoming()
  return async (kind: EstateKind, id: string, label: string, message?: CancelMessage | null) => {
    try {
      const r = await cancel.mutateAsync({ scopeType: scopeTypeOf(kind), scopeId: id, message: message ?? bulkCancelDefaults(label) })
      if (r.cancelledCount) toast('warn', i18n.t('bookable.toast.cancelled', { count: r.cancelledCount }), i18n.t('bookable.toast.cancelledIn', { name: label }))
    } catch (e) {
      const failed = errorText(e)
      toast('err', i18n.t('bookable.toast.notCancelled'), failed.message, failed.code)
    }
  }
}
