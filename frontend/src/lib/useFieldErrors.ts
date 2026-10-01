import { useState } from 'react'
import i18n from 'i18next'
import { ApiError, errorText } from '../api/client'
import { toast } from '../state/toastStore'

export type FieldError = { code: string; message: string }
/* Form field id -> the message shown right under that field. */
export type FieldErrors = Record<string, FieldError | undefined>

/* Errors shown under the field they belong to. A form checks every field at once, so all problems show
 * together; a server error lands under the field it names; one with no field of this form is a toast. */
export function useFieldErrors() {
  const [errors, setErrors] = useState<FieldErrors>({})
  /* Goes up on every Save that finds a problem, even the same one again, so a field can bring itself into view. */
  const [attempt, setAttempt] = useState(0)

  /* Editing a field takes its message away. */
  const clear = (...ids: string[]) => setErrors((prev) => (ids.some((id) => prev[id])
    ? Object.fromEntries(Object.entries(prev).filter(([k]) => !ids.includes(k)))
    : prev))

  /* Replaces every message; true when there is at least one (so the form should not be sent). */
  const show = (next: FieldErrors) => {
    const found = Object.fromEntries(Object.entries(next).filter(([, v]) => v)) as FieldErrors
    setErrors(found)
    const failed = Object.keys(found).length > 0
    if (failed) setAttempt((n) => n + 1)
    return failed
  }

  /* serverFields maps the server's field names (e.g. "name", "closeHourOverride") to this form's field ids. */
  const fromServer = (e: unknown, serverFields: Record<string, string>, toastTitle = i18n.t('common.requestRejected')) => {
    const placed: FieldErrors = {}
    if (e instanceof ApiError) {
      e.fieldErrors.forEach((f) => {
        const id = serverFields[f.field]
        if (id && !placed[id]) placed[id] = { code: f.code, message: f.message }
      })
    }
    if (Object.keys(placed).length) {
      setErrors(placed)
      setAttempt((n) => n + 1)
      return
    }
    const { code, message } = errorText(e)
    toast('err', toastTitle, message, code)
  }

  return { errors, attempt, clear, show, fromServer }
}
