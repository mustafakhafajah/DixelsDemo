import { useState, type InputHTMLAttributes } from 'react'
import { useTranslation } from 'react-i18next'
import { useAbpSettings } from '../../api/hooks'
import { Field, RequiredNote } from '../../components/bits'
import { Modal } from '../../components/Sheet'
import { useLiveValidation } from '../../lib/useLiveValidation'
import { toast } from '../../state/toastStore'
import { ADDRESS_MAX, ADDRESS_PROPERTY, useChangeMyPassword, useUpdateMyProfile, type MyProfile, type ProfileChanges } from './api'
import { checkPassword, checkProfile, PASSWORD_FIELDS, passwordPolicyFrom, PROFILE_FIELDS } from './validation'

/* ABP's limits (IdentityUserConsts), so a field stops where the server would refuse. */
const USER_NAME_MAX = 256
const EMAIL_MAX = 256
const NAME_MAX = 64
const PHONE_MAX = 16

/* ABP's switches for whether users may change their own user name and email (shown to clients). */
const USER_NAME_UPDATE = 'Abp.Identity.User.IsUserNameUpdateEnabled'
const EMAIL_UPDATE = 'Abp.Identity.User.IsEmailUpdateEnabled'
const isOn = (value: string | undefined) => value === undefined || value.toLowerCase() === 'true'

/* The server names a field by its request property; these are the form's ids for them. */
const PROFILE_SERVER_FIELDS: Record<string, string> = {
  name: PROFILE_FIELDS.name, surname: PROFILE_FIELDS.surname, userName: PROFILE_FIELDS.userName,
  email: PROFILE_FIELDS.email, phoneNumber: PROFILE_FIELDS.phoneNumber, address: PROFILE_FIELDS.address,
}
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

/* Name, user name, email, phone and address. Roles and tenant are not here: ABP's profile update has no way
 * to change them, and an administrator sets them. */
export function EditProfileModal({ me, onClose }: { me: MyProfile; onClose: () => void }) {
  const { t } = useTranslation()
  const save = useUpdateMyProfile()
  const settings = useAbpSettings().data ?? {}
  const { profile } = me
  const address = profile.extraProperties?.[ADDRESS_PROPERTY]
  const [initial] = useState<ProfileChanges>({
    userName: profile.userName,
    email: profile.email,
    name: profile.name ?? '',
    surname: profile.surname ?? '',
    phoneNumber: profile.phoneNumber ?? '',
    address: typeof address === 'string' ? address : '',
  })
  const [form, setForm] = useState(initial)
  const validation = useLiveValidation(checkProfile(form))
  const userNameLocked = !isOn(settings[USER_NAME_UPDATE])
  const emailLocked = !isOn(settings[EMAIL_UPDATE])

  const submit = () => {
    if (!validation.trySubmit()) return
    /* Nothing changed: nothing to send. */
    if (JSON.stringify(form) === JSON.stringify(initial)) return onClose()
    save.mutateAsync({ current: me, changes: form }).then(() => {
      toast('ok', t('profile.edit.saved'), t('profile.edit.savedMessage'))
      onClose()
    }, (e) => validation.fromServer(e, PROFILE_SERVER_FIELDS))
  }

  const input = (key: keyof ProfileChanges, extra: Omit<InputHTMLAttributes<HTMLInputElement>, 'id' | 'value' | 'onChange'>) => (
    <LiveInput id={PROFILE_FIELDS[key]} value={form[key]} onValue={(v) => setForm((f) => ({ ...f, [key]: v }))}
      validation={validation} onEnter={submit} {...extra} />
  )
  const field = (key: keyof ProfileChanges, label: string, required: boolean, extra: Omit<InputHTMLAttributes<HTMLInputElement>, 'id' | 'value' | 'onChange'>, hint?: string) => (
    <Field id={PROFILE_FIELDS[key]} label={label} required={required} error={validation.errorOf(PROFILE_FIELDS[key])}>
      {input(key, extra)}
      {hint && <p className="profile-hint">{hint}</p>}
    </Field>
  )

  return (
    <Modal width={480} title={t('profile.edit.title')} subtitle={t('profile.edit.subtitle')} onClose={onClose}
      footer={<Footer onClose={onClose} onSave={submit} busy={save.isPending} />}>
      <RequiredNote />
      <div className="form-cols" style={{ display: 'grid', gridTemplateColumns: '1fr 1fr', gap: 12 }}>
        {field('name', t('profile.firstName'), true, { maxLength: NAME_MAX, autoComplete: 'given-name', autoFocus: true })}
        {field('surname', t('profile.surname'), false, { maxLength: NAME_MAX, autoComplete: 'family-name' })}
      </div>
      {field('userName', t('profile.userName'), true, { maxLength: USER_NAME_MAX, dir: 'ltr', autoComplete: 'username', readOnly: userNameLocked },
        userNameLocked ? t('profile.edit.setByAdministrator') : undefined)}
      {field('email', t('profile.email'), true, { type: 'email', maxLength: EMAIL_MAX, dir: 'ltr', autoComplete: 'email', readOnly: emailLocked },
        emailLocked ? t('profile.edit.setByAdministrator') : undefined)}
      {field('phoneNumber', t('profile.phone'), false, { type: 'tel', maxLength: PHONE_MAX, dir: 'ltr', autoComplete: 'tel', placeholder: '+962790000000' })}
      {field('address', t('profile.address'), false, { maxLength: ADDRESS_MAX, autoComplete: 'street-address' })}
      <p className="profile-hint">{t('profile.edit.reverifyNote')}</p>
    </Modal>
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
