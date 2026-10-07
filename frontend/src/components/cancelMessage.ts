import i18n from 'i18next'
import type { Booking } from '../api/types'
import { formatDate } from '../lib/dateUtils'
import type { CancelMessageValue } from './CancelMessageFields'

/* The ready-made cancellation email for one booking (or a series from it), in the language in use. */
export const bookingCancelDefaults = (b: Pick<Booking, 'spaceName' | 'start'>, name: string): CancelMessageValue => ({
  subject: i18n.t('cancelMessage.bookingSubject', {
    space: b.spaceName, date: formatDate(b.start, { weekday: 'short', day: 'numeric', month: 'short', year: 'numeric' }),
  }),
  message: i18n.t('cancelMessage.bookingMessage', { name }),
})

/* The ready-made email for bookings cancelled in bulk: a building, floor or space made not bookable, or (with
 * blocked) time blocked there, with its reason when there is one. */
export const bulkCancelDefaults = (scope: string, blocked?: { note: string }): CancelMessageValue => ({
  subject: i18n.t('cancelMessage.bulkSubject', { scope }),
  message: !blocked ? i18n.t('cancelMessage.notBookableMessage', { scope })
    : blocked.note.trim() ? i18n.t('cancelMessage.blockedMessage', { scope, note: blocked.note.trim() })
      : i18n.t('cancelMessage.blockedPlainMessage', { scope }),
})
