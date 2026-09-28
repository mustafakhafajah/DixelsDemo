import { useState, type KeyboardEvent, type ReactNode } from 'react'
import { ErrorLine, RequiredMark } from '../../components/bits'
import { Dropdown } from '../../components/pickers'
import { Modal } from '../../components/Sheet'
import { useFieldErrors, type FieldError } from '../../lib/useFieldErrors'
import { toast } from '../../state/toastStore'
import { useCreateUser, type UserRole } from './api'

const missing = (message: string): FieldError => ({ code: 'validation.missing_field', message })
const invalid = (message: string): FieldError => ({ code: 'validation.invalid', message })

export const ROLE_OPTIONS: { value: UserRole; label: string }[] = [
  { value: 'admin', label: 'Admin' },
  { value: 'employee', label: 'Employee' },
]

const PASSWORD_HINT = 'At least 6 characters with upper, lower and a digit'
/* The same rule the server's identity options apply, so the message shows before sending. */
const passwordOk = (p: string) => p.length >= 6 && /[a-z]/.test(p) && /[A-Z]/.test(p) && /\d/.test(p)

function Field({ id, label, required, error, hint, children }: { id: string; label: string; required?: boolean; error?: FieldError; hint?: string; children: ReactNode }) {
  return (
    <div>
      <label className={`lbl${required ? ' req' : ''}`} htmlFor={id}>{label}{required && <RequiredMark />}</label>
      {children}
      {hint && !error && <p style={{ fontSize: 11.5, color: 'var(--slate)', margin: '5px 0 0' }}>{hint}</p>}
      <ErrorLine id={`${id}-error`} error={error} />
    </div>
  )
}

const SERVER_FIELDS = { name: 'uf-name', email: 'uf-email', userName: 'uf-username', password: 'uf-password', role: 'uf-role' }

export function UserFormModal({ onClose }: { onClose: () => void }) {
  const create = useCreateUser()
  const fields = useFieldErrors()
  const [name, setName] = useState('')
  const [email, setEmail] = useState('')
  const [userName, setUserName] = useState('')
  const [password, setPassword] = useState('')
  const [role, setRole] = useState<UserRole>('employee')

  const submit = () => {
    if (fields.show({
      'uf-name': name.trim() ? undefined : missing('Give the user a name.'),
      'uf-email': !email.trim() ? missing('Enter an email address.') : /^[^\s@]+@[^\s@]+\.[^\s@]+$/.test(email.trim()) ? undefined : invalid('Enter a valid email address.'),
      'uf-password': !password ? missing('Set a password.') : passwordOk(password) ? undefined : invalid(`${PASSWORD_HINT}.`),
    })) return
    create.mutateAsync({ name: name.trim(), email: email.trim(), userName: userName.trim() || undefined, password, role }).then((u) => {
      toast('ok', 'User added', `${u.name} can now sign in as ${u.userName}.`)
      onClose()
    }, (e) => fields.fromServer(e, SERVER_FIELDS))
  }

  const onEnter = (e: KeyboardEvent) => { if (e.key === 'Enter') submit() }

  return (
    <Modal width={460} title="Add user" subtitle="A new portal account. They sign in with the username or email and this password."
      onClose={onClose} footer={
        <>
          <button type="button" className="btn" onClick={onClose}>Discard</button>
          <button type="button" className="btn btn-primary" disabled={create.isPending} onClick={submit}>Add user</button>
        </>
      }>
      <p className="req-note"><RequiredMark /> Required field</p>
      <Field id="uf-name" label="Name" required error={fields.errors['uf-name']}>
        <input id="uf-name" className="inp" placeholder="e.g. Sara Haddad" value={name} autoFocus maxLength={128}
          onChange={(e) => { setName(e.target.value); fields.clear('uf-name') }} onKeyDown={onEnter} />
      </Field>
      <Field id="uf-email" label="Email" required error={fields.errors['uf-email']}>
        <input id="uf-email" type="email" className="inp" placeholder="name@company.com" value={email} maxLength={256} autoComplete="off"
          onChange={(e) => { setEmail(e.target.value); fields.clear('uf-email') }} onKeyDown={onEnter} />
      </Field>
      <Field id="uf-username" label="Username" error={fields.errors['uf-username']}>
        <input id="uf-username" className="inp mono" placeholder="Same as email" value={userName} maxLength={256} autoComplete="off"
          onChange={(e) => { setUserName(e.target.value); fields.clear('uf-username') }} onKeyDown={onEnter} />
      </Field>
      <Field id="uf-password" label="Password" required hint={PASSWORD_HINT} error={fields.errors['uf-password']}>
        <input id="uf-password" type="password" className="inp" value={password} autoComplete="new-password"
          onChange={(e) => { setPassword(e.target.value); fields.clear('uf-password') }} onKeyDown={onEnter} />
      </Field>
      <Field id="uf-role" label="Role" required error={fields.errors['uf-role']}>
        <Dropdown id="uf-role" value={role} onChange={(v) => { setRole(v as UserRole); fields.clear('uf-role') }} options={ROLE_OPTIONS} />
      </Field>
    </Modal>
  )
}
