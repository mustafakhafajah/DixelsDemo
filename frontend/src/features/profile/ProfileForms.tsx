import { useState, type InputHTMLAttributes } from 'react'
import { useTranslation } from 'react-i18next'
import { useAbpSettings } from '../../api/hooks'
import { Field, RequiredNote } from '../../components/bits'
import { Modal } from '../../components/Sheet'
import { useLiveValidation } from '../../lib/useLiveValidation'
import { toast } from '../../state/toastStore'
import { useChangeMyPassword, type MyProfile } from './api'
import { checkPassword, PASSWORD_FIELDS, passwordPolicyFrom } from './validation'

/* The server names a field by its request property; these are the form's ids for them. */
const PASSWORD_SERVER_FIELDS: Record<string, string> = { currentPassword: PASSWORD_FIELDS.current, newPassword: PASSWORD_FIELDS.next }

function Footer({ onClose, onSave, busy }: { onClose: () => void; onSave: () => void; busy: boolean }) {
  const { t } = useTranslation()
  return (
    <>
      <button type="button" className="btn" onClick={onClose}>{t('common.discard')}</button>
      <button type="button" className="btn btn-primary" disabled={busy} onClick={onSave}>{t('common.saveChanges')}</button>
    </>
  )
}

type Validation = ReturnType<typeof useLiveValidation>

/* An input wired to live validation: typing marks it touched (so its problem shows at once) and clears any
 * message the server sent for it; Enter saves. */
function LiveInput({ id, value, onValue, validation, onEnter, ...rest }: {
  id: string; value: string; onValue: (v: string) => void; validation: Validation; onEnter: () => void
} & Omit<InputHTMLAttributes<HTMLInputElement>, 'id' | 'value' | 'onChange'>) {
  const error = validation.errorOf(id)
  return (
    <input id={id} className="inp" value={value} aria-invalid={!!error} aria-describedby={`${id}-error`}
      onChange={(e) => { onValue(e.target.value); validation.touch(id) }}
      onKeyDown={(e) => e.key === 'Enter' && onEnter()} {...rest} />
  )
}

export function ChangePasswordModal({ me, onClose }: { me: MyProfile; onClose: () => void }) {
  const { t } = useTranslation()
  const change = useChangeMyPassword()
  const policy = passwordPolicyFrom(useAbpSettings().data ?? {})
  /* A user who has no password yet (rare: made without one) only sets a new one. */
  const needsCurrent = me.profile.hasPassword
  const [form, setForm] = useState({ current: '', next: '', repeat: '' })
  const validation = useLiveValidation(checkPassword(form, policy, needsCurrent))

  const submit = () => {
    if (!validation.trySubmit()) return
    change.mutateAsync({ currentPassword: needsCurrent ? form.current : undefined, newPassword: form.next }).then(() => {
      toast('ok', t('profile.changePassword.changed'), t('profile.changePassword.changedMessage'))
      onClose()
    }, (e) => validation.fromServer(e, PASSWORD_SERVER_FIELDS))
  }

  const password = (key: keyof typeof form, label: string, autoComplete: string, autoFocus = false) => (
    <Field id={PASSWORD_FIELDS[key]} label={label} required error={validation.errorOf(PASSWORD_FIELDS[key])}>
      <LiveInput id={PASSWORD_FIELDS[key]} type="password" dir="ltr" value={form[key]} onValue={(v) => setForm((f) => ({ ...f, [key]: v }))}
        validation={validation} onEnter={submit} autoComplete={autoComplete} autoFocus={autoFocus} />
    </Field>
  )

  return (
    <Modal width={420} title={t('profile.changePassword.title')} subtitle={t('profile.changePassword.subtitle')} onClose={onClose}
      footer={<Footer onClose={onClose} onSave={submit} busy={change.isPending} />}>
      <RequiredNote />
      {needsCurrent && password('current', t('profile.changePassword.current'), 'current-password', true)}
      {password('next', t('profile.changePassword.new'), 'new-password', !needsCurrent)}
      {password('repeat', t('profile.changePassword.repeat'), 'new-password')}
    </Modal>
  )
}
