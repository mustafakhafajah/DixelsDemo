import { lifecycleOf, myResponse, type ScheduleItem } from '../../../api/types'
import { modals } from '../../../state/modalStore'

/* The colour class of a booking or block on the calendars. */
export function itemClass(i: ScheduleItem, myId: string): string {
  if (i.kind === 'maintenance') return 'cleaning'
  if (i.busy) return 'busy'
  const st = lifecycleOf(i)
  if (st === 'in_progress') return 'inprog'
  if (st === 'ended') return 'ended'
  if (i.ownerUserId === myId) return 'mine'
  /* Invited, shown as Teams does: solid once accepted, striped while unanswered or tentative, faded when declined. */
  const reply = myResponse(i, myId)
  if (!reply) return 'other'
  return reply === 'accepted' ? 'attending' : reply === 'declined' ? 'attending declined' : 'attending tentative'
}

/* A grey busy block is someone else's booking: there is nothing to open. */
export const opens = (i: ScheduleItem) => !(i.kind === 'booking' && i.busy)
export const openItem = (i: ScheduleItem) => {
  if (!opens(i)) return
  modals.detail(i.kind === 'booking' ? 'booking' : 'maintenance', i.id)
}
