import type { AttendeeInput, Booking } from '../../api/types'

/* Someone on the invite list: a portal user, or an outside guest known only by email address. */
export type Invitee = { userId: string; name: string; email: string | null } | { userId: null; email: string }

export const inviteeKey = (p: Invitee) => (p.userId !== null ? p.userId : `mail:${p.email.toLowerCase()}`)

export const toAttendeeInput = (p: Invitee): AttendeeInput => (p.userId !== null ? { userId: p.userId } : { email: p.email })

/* The list a booking already has, as the owner edits it (guests' addresses only reach the owner and admins). */
export const inviteesOf = (b: Booking): Invitee[] => [
  ...b.attendees.map((a) => ({ userId: a.userId, name: a.name, email: a.email })),
  ...b.guests.map((email) => ({ userId: null, email })),
]

/* The same people, in any order. */
export const samePeople = (a: Invitee[], b: Invitee[]) =>
  a.map(inviteeKey).sort().join('|') === b.map(inviteeKey).sort().join('|')
