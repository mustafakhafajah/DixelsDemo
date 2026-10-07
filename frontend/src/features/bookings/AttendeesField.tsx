import { useId, useState, type KeyboardEvent } from 'react'
import { useTranslation } from 'react-i18next'
import { MailIcon, XIcon } from 'lucide-react'
import { useBookingPeople } from '../../api/hooks'
import type { BookingPerson } from '../../api/types'
import { isEmail } from '../../lib/email'
import { initials } from '../../lib/initials'
import { useDebouncedValue } from '../../lib/useDebouncedValue'
import { inviteeKey, type Invitee } from './invitees'

type Suggestion = { kind: 'user'; person: BookingPerson } | { kind: 'guest'; email: string }

/* Type a name to pick a colleague, or a whole email address to invite anyone else. Picked people show as chips
 * under the box. capacity: the room's seats (0 = not set); the person booking counts as one. */
export function AttendeesField({ id, value, onChange, capacity, describedBy }: {
  id: string
  value: Invitee[]
  onChange: (next: Invitee[]) => void
  capacity: number
  describedBy?: string
}) {
  const { t } = useTranslation()
  const listId = useId()
  const [text, setText] = useState('')
  const [active, setActive] = useState(0)
  const typed = text.trim()
  const searching = typed.length > 0
  const query = useDebouncedValue(typed, 250)
  const peopleQ = useBookingPeople(query, searching && query.length > 0)

  const picked = new Set(value.map(inviteeKey))
  const takenEmail = (e: string | null) => !!e && value.some((v) => v.email?.toLowerCase() === e.toLowerCase())
  const matches = searching ? (peopleQ.data ?? []).filter((p) => !picked.has(p.id)).slice(0, 6) : []
  const typedEmail = isEmail(typed) ? typed.toLowerCase() : null
  const offerEmail = !!typedEmail && !takenEmail(typedEmail) && !matches.some((p) => p.email?.toLowerCase() === typedEmail)
  const suggestions: Suggestion[] = [
    ...matches.map((person) => ({ kind: 'user' as const, person })),
    ...(offerEmail ? [{ kind: 'guest' as const, email: typedEmail! }] : []),
  ]
  const current = Math.min(active, Math.max(suggestions.length - 1, 0))

  const add = (s: Suggestion) => {
    onChange([...value, s.kind === 'user'
      ? { userId: s.person.id, name: s.person.name, email: s.person.email }
      : { userId: null, email: s.email }])
    setText('')
    setActive(0)
  }
  const remove = (key: string) => onChange(value.filter((p) => inviteeKey(p) !== key))

  const onKeyDown = (e: KeyboardEvent<HTMLInputElement>) => {
    if (e.key === 'ArrowDown' && suggestions.length) { e.preventDefault(); setActive((current + 1) % suggestions.length) }
    else if (e.key === 'ArrowUp' && suggestions.length) { e.preventDefault(); setActive((current - 1 + suggestions.length) % suggestions.length) }
    else if (e.key === 'Enter' && searching) { e.preventDefault(); if (suggestions[current]) add(suggestions[current]) }
    /* Escape clears the box first; the form only closes on a second Escape. */
    else if (e.key === 'Escape' && searching) { e.preventDefault(); setText('') }
    else if (e.key === 'Backspace' && !text && value.length) remove(inviteeKey(value[value.length - 1]))
  }

  const empty = peopleQ.isFetching || query !== typed ? t('booking.attendees.searching')
    : typedEmail && takenEmail(typedEmail) ? t('booking.attendees.alreadyAdded')
      : t('booking.attendees.noMatch')

  return (
    <div className="people-field">
      <input id={id} className="inp" type="text" role="combobox" autoComplete="off" dir="auto"
        aria-autocomplete="list" aria-expanded={searching} aria-controls={listId} aria-describedby={describedBy}
        aria-activedescendant={searching && suggestions.length ? `${listId}-${current}` : undefined}
        placeholder={t('booking.attendees.placeholder')} value={text}
        onChange={(e) => { setText(e.target.value); setActive(0) }} onKeyDown={onKeyDown} />
      {searching && (
        <ul id={listId} role="listbox" className="people-suggest" aria-label={t('booking.attendees.suggestions')}>
          {suggestions.map((s, i) => (
            <li key={s.kind === 'user' ? s.person.id : `mail:${s.email}`} id={`${listId}-${i}`} role="option"
              aria-selected={i === current} className={i === current ? 'active' : undefined}
              onMouseDown={(e) => e.preventDefault()} onMouseEnter={() => setActive(i)} onClick={() => add(s)}>
              {s.kind === 'user' ? (
                <>
                  <span className="avatar person-avatar" aria-hidden="true">{initials(s.person.name)}</span>
                  <span className="person-who">
                    <bdi>{s.person.name}</bdi>
                    {s.person.email && <small><bdi dir="ltr">{s.person.email}</bdi></small>}
                  </span>
                </>
              ) : (
                <>
                  <span className="avatar person-avatar guest" aria-hidden="true"><MailIcon size={13} /></span>
                  <span className="person-who">
                    <span>{t('booking.attendees.inviteByEmail')}</span>
                    <small><bdi dir="ltr">{s.email}</bdi></small>
                  </span>
                </>
              )}
            </li>
          ))}
          {!suggestions.length && <li className="people-empty" aria-disabled="true">{empty}</li>}
        </ul>
      )}
      {value.length > 0 && (
        <ul className="people-chips" aria-label={t('booking.attendees.invited')}>
          {value.map((p) => {
            const label = p.userId ? p.name : p.email
            return (
              <li key={inviteeKey(p)} className="person-chip">
                <span className="avatar person-avatar" aria-hidden="true">{p.userId ? initials(p.name) : <MailIcon size={12} />}</span>
                {p.userId ? <bdi>{p.name}</bdi> : <bdi dir="ltr">{p.email}</bdi>}
                {!p.userId && <span className="tag">{t('booking.attendees.guest')}</span>}
                <button type="button" className="person-remove" aria-label={t('booking.attendees.remove', { name: label })}
                  onClick={() => remove(inviteeKey(p))}>
                  <XIcon size={13} aria-hidden="true" />
                </button>
              </li>
            )
          })}
        </ul>
      )}
      <p className="card-sub" style={{ margin: '6px 0 0' }}>
        {capacity > 0 ? `${t('booking.attendees.seats', { count: value.length + 1, capacity })} · ` : ''}
        {t('booking.attendees.hint')}
      </p>
    </div>
  )
}
