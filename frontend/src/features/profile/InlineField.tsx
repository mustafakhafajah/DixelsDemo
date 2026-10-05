import { useState, type InputHTMLAttributes, type ReactNode } from 'react'
import { useTranslation } from 'react-i18next'
import { CheckIcon, PencilIcon, XIcon } from 'lucide-react'
import { ApiError, errorText } from '../../api/client'
import { ErrorLine } from '../../components/bits'
import type { FieldError } from '../../lib/useFieldErrors'
import { toast } from '../../state/toastStore'

type InputProps = Omit<InputHTMLAttributes<HTMLInputElement>, 'id' | 'value' | 'onChange' | 'onKeyDown'>

/* One row of the profile that can be changed where it is shown: the value with a pencil, and in its place, when
 * editing, an input with Save and Cancel. Enter saves, Escape cancels. The problem with what is typed shows under
 * it as the user types (check), and so does a refusal the server ties to this field (serverField). */
export function InlineField({ id, label, value, display, editing, onEdit, onClose, check, onSave, serverField, hint, input }: {
  id: string
  label: string
  value: string
  /* The value as shown when not editing. */
  display: ReactNode
  editing: boolean
  onEdit: () => void
  onClose: () => void
  check: (draft: string) => FieldError | undefined
  onSave: (draft: string) => Promise<unknown>
  /* The server's name for this field, e.g. "phoneNumber". */
  serverField: string
  /* Shown under the input while editing. */
  hint?: string
  input?: InputProps
}) {
  const { t } = useTranslation()
  if (!editing) {
    return (
      <>
        <dt>{label}</dt>
        <dd>
          {display}
          <button type="button" className="iconbtn inline-edit-btn" onClick={onEdit} aria-label={t('profile.edit.editField', { field: label })}
            title={t('profile.edit.editField', { field: label })}>
            <PencilIcon aria-hidden="true" />
          </button>
        </dd>
      </>
    )
  }
  return <InlineEditor id={id} label={label} value={value} onClose={onClose} check={check} onSave={onSave} serverField={serverField} hint={hint} input={input} />
}

function InlineEditor({ id, label, value, onClose, check, onSave, serverField, hint, input }: {
  id: string; label: string; value: string; onClose: () => void
  check: (draft: string) => FieldError | undefined; onSave: (draft: string) => Promise<unknown>; serverField: string; hint?: string; input?: InputProps
}) {
  const { t } = useTranslation()
  const [draft, setDraft] = useState(value)
  const [touched, setTouched] = useState(false)
  const [serverError, setServerError] = useState<FieldError>()
  const [saving, setSaving] = useState(false)
  const problem = check(draft)
  const error = serverError ?? (touched ? problem : undefined)

  const save = () => {
    setTouched(true)
    if (problem || saving) return
    /* Nothing changed: nothing to send. */
    if (draft === value) return onClose()
    setSaving(true)
    onSave(draft).then(() => {
      toast('ok', t('profile.edit.fieldSaved', { field: label }))
      onClose()
    }, (e) => {
      setSaving(false)
      /* A refusal about this field goes under it; anything else (e.g. someone changed the profile meanwhile) is a notice. */
      const mine = e instanceof ApiError ? e.fieldErrors.find((f) => f.field === serverField) : undefined
      if (mine) setServerError({ code: mine.code, message: mine.message })
      else { const { code, message } = errorText(e); toast('err', t('common.requestRejected'), message, code) }
    })
  }

  return (
    <>
      <dt><label htmlFor={id}>{label}</label></dt>
      <dd className="inline-editing">
        <div className="inline-edit-row">
          <input id={id} className="inp" value={draft} autoFocus disabled={saving} aria-invalid={!!error} aria-describedby={`${id}-error`}
            onChange={(e) => { setDraft(e.target.value); setTouched(true); setServerError(undefined) }}
            onKeyDown={(e) => {
              if (e.key === 'Enter') save()
              /* Escape here only closes this editor, not anything around it. */
              if (e.key === 'Escape') { e.preventDefault(); e.stopPropagation(); onClose() }
            }}
            {...input} />
          <button type="button" className="btn btn-primary btn-sm inline-edit-action" onClick={save} disabled={saving} aria-label={t('profile.edit.save')} title={t('profile.edit.save')}>
            <CheckIcon aria-hidden="true" />
          </button>
          <button type="button" className="btn btn-sm inline-edit-action" onClick={onClose} disabled={saving} aria-label={t('common.cancel')} title={t('common.cancel')}>
            <XIcon aria-hidden="true" />
          </button>
        </div>
        <ErrorLine id={`${id}-error`} error={error} />
        {hint && !error && <p className="profile-hint">{hint}</p>}
      </dd>
    </>
  )
}
