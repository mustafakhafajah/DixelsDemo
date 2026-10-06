import i18n from 'i18next'
import { errorText } from '../../api/client'
import { useCancelUpcoming, type EstateKind } from '../../api/hooks'
import type { MaintenanceScopeType } from '../../api/types'
import { toast } from '../../state/toastStore'

export const scopeTypeOf = (kind: EstateKind): MaintenanceScopeType =>
  kind === 'building' ? 'Building' : kind === 'floor' ? 'Floor' : 'Space'

/* After saving an item as not bookable: cancel its upcoming bookings if the admin asked to. */
export function useCancelUpcomingAfterSave() {
  const cancel = useCancelUpcoming()
  return async (kind: EstateKind, id: string, label: string) => {
    try {
      const r = await cancel.mutateAsync({ scopeType: scopeTypeOf(kind), scopeId: id })
      if (r.cancelledCount) toast('warn', i18n.t('bookable.toast.cancelled', { count: r.cancelledCount }), i18n.t('bookable.toast.cancelledIn', { name: label }))
    } catch (e) {
      const { code, message } = errorText(e)
      toast('err', i18n.t('bookable.toast.notCancelled'), message, code)
    }
  }
}
