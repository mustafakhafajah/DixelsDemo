import { useState } from 'react'
import { useFieldErrors, type FieldError, type FieldErrors } from './useFieldErrors'

/* Messages that follow the user's typing. live: the problems with the form as it is now, worked out on every
 * render. A field shows its problem once the user has typed in it, and every field does after a Save; a message
 * the server sent stays under its field until that field is changed. */
export function useLiveValidation(live: FieldErrors) {
  const server = useFieldErrors()
  const [touched, setTouched] = useState<ReadonlySet<string>>(new Set())
  const [submitted, setSubmitted] = useState(false)

  /* Call from a field's onChange. */
  const touch = (id: string) => {
    setTouched((prev) => (prev.has(id) ? prev : new Set(prev).add(id)))
    server.clear(id)
  }

  const errorOf = (id: string): FieldError | undefined => server.errors[id] ?? (submitted || touched.has(id) ? live[id] : undefined)

  /* Call on Save: shows every problem, and says whether the form may be sent. */
  const trySubmit = () => {
    setSubmitted(true)
    return !Object.values(live).some(Boolean)
  }

  return { errorOf, touch, trySubmit, fromServer: server.fromServer }
}
