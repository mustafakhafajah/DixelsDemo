import { useState, type InputHTMLAttributes, type ReactNode } from 'react'
import { useTranslation } from 'react-i18next'
import { AtSignIcon, CalendarIcon, KeyRoundIcon, LockKeyholeIcon, LogInIcon, MailIcon, ShieldCheckIcon, ShieldIcon, UserPlusIcon, type LucideIcon } from 'lucide-react'
import { useAbpSettings } from '../../api/hooks'
import { displayName } from '../../api/types'
import { Field, LoadError, Loading } from '../../components/bits'
import { intlLocale } from '../../i18n/languages'
import { formatDate, parseUtc, relative } from '../../lib/dateUtils'
import { useLiveValidation } from '../../lib/useLiveValidation'
import { toast } from '../../state/toastStore'
import { roleLabel } from '../users/api'
import { ADDRESS_MAX, ADDRESS_PROPERTY, useMyProfile, useUpdateMyProfile, type MyProfile, type ProfileChanges } from './api'
import { MyAvatar } from './MyAvatar'
import { ProfilePhoto } from './ProfilePhoto'
import { ChangePasswordModal } from './ProfileForms'
import { checkProfile, PROFILE_FIELDS } from './validation'
import './profile.css'

const DAY: Intl.DateTimeFormatOptions = { day: 'numeric', month: 'long', year: 'numeric' }
/* "4 October 2026, 14:05", on the viewer's own clock like every time in the app. */
const MOMENT: Intl.DateTimeFormatOptions = { ...DAY, hour: '2-digit', minute: '2-digit', hour12: false }

/* ABP's limits (IdentityUserConsts), so a field stops where the server would refuse. */
const USER_NAME_MAX = 256
const NAME_MAX = 64
const PHONE_MAX = 16

/* ABP's switch for whether people may change their own user name (shown to clients; on unless turned off). */
const USER_NAME_UPDATE = 'Abp.Identity.User.IsUserNameUpdateEnabled'

/* The server's name for each field, mapped to the form's id for it, so a refusal it ties to one lands under it. */
const SERVER_FIELDS: Record<string, string> = {
  name: PROFILE_FIELDS.name, surname: PROFILE_FIELDS.surname, userName: PROFILE_FIELDS.userName,
  email: PROFILE_FIELDS.email, phoneNumber: PROFILE_FIELDS.phoneNumber, address: PROFILE_FIELDS.address,
}

/* Any value a field may be missing shows a quiet "Not provided" (or "Never" for a date) instead of a blank. */
function Missing({ text }: { text?: string }) {
  const { t } = useTranslation()
  return <span className="missing">{text ?? t('profile.notProvided')}</span>
}

/* An email or phone number reads left to right in any language. */
function Contact({ value }: { value: string | null }) {
  if (!value?.trim()) return <Missing />
  return <bdi dir="ltr">{value}</bdi>
}

/* A panel with a title (and an optional icon and line under it); aside sits at the far end of the head. */
function Panel({ title, sub, icon: Icon, aside, divided = true, children }: {
  title: string; sub?: ReactNode; icon?: LucideIcon; aside?: ReactNode; divided?: boolean; children: ReactNode
}) {
  return (
    <section className="card profile-card">
      <header className={`profile-card-head${divided ? ' divided' : ''}`}>
        {Icon && <span className="profile-icon-box"><Icon aria-hidden="true" /></span>}
        <div className="profile-card-copy">
          <h2 className="profile-card-title">{title}</h2>
          {sub && <p className="profile-card-sub">{sub}</p>}
        </div>
        {aside}
      </header>
      {children}
    </section>
  )
}

/* Extra properties other than the address, which has its own field; a value that is empty is left out. */
function otherDetails(extra: MyProfile['profile']['extraProperties']): [string, string][] {
  return Object.entries(extra ?? {})
    .filter(([name, value]) => name !== ADDRESS_PROPERTY && value !== null && value !== undefined && String(value).trim() !== '')
    .map(([name, value]) => [name, String(value)])
}

/* What the user may change, as it is now. */
function changesOf(me: MyProfile): ProfileChanges {
  const address = me.profile.extraProperties?.[ADDRESS_PROPERTY]
  return {
    userName: me.profile.userName,
    email: me.profile.email,
    name: me.profile.name ?? '',
    surname: me.profile.surname ?? '',
    phoneNumber: me.profile.phoneNumber ?? '',
    address: typeof address === 'string' ? address : '',
  }
}

/* How much of what the user fills in themselves is there (0 to 1): their names, phone, address and picture. */
function completion(me: MyProfile): number {
  const c = changesOf(me)
  const parts = [c.name, c.surname, c.phoneNumber, c.address].map((v) => !!v.trim()).concat(!!me.pictureVersion)
  return parts.filter(Boolean).length / parts.length
}

/* Who you are at a glance: picture, name and roles, how to reach you and when you were last here. */
function ProfileHeader({ me, name }: { me: MyProfile; name: string }) {
  const { t } = useTranslation()
  return (
    <section className="card profile-header">
      <MyAvatar name={name} className="avatar profile-avatar" />
      <div className="profile-identity">
        <div className="profile-name-row">
          <h2 className="profile-name"><bdi>{name}</bdi></h2>
          {me.roles.map((r) => <span key={r} className="profile-role">{roleLabel(r)}</span>)}
        </div>
        <ul className="profile-meta">
          <li><MailIcon aria-hidden="true" /><bdi dir="ltr">{me.profile.email}</bdi></li>
          <li><CalendarIcon aria-hidden="true" />{t('profile.header.memberSince', { date: formatDate(parseUtc(me.creationTime), DAY) })}</li>
          {me.lastSignInTime && (
            <li><LogInIcon aria-hidden="true" />{t('profile.header.lastSignIn', { date: formatDate(parseUtc(me.lastSignInTime), MOMENT) })}</li>
          )}
        </ul>
      </div>
      <ProfilePhoto me={me} />
    </section>
  )
}

type FieldInput = Omit<InputHTMLAttributes<HTMLInputElement>, 'id' | 'value' | 'onChange'>

/* The details the user changes themselves, as one form: the same checks as the server as they type, all saved
 * together, and the server's own word on save under the field it names. */
function PersonalInformation({ me, userNameEditable, onCancel }: { me: MyProfile; userNameEditable: boolean; onCancel: () => void }) {
  const { t } = useTranslation()
  const update = useUpdateMyProfile()
  const saved = changesOf(me)
  const [draft, setDraft] = useState(saved)
  const shown: (keyof ProfileChanges)[] = ['name', 'surname', ...(userNameEditable ? ['userName' as const] : []), 'phoneNumber', 'address']
  /* Only the fields in this form can hold it back; the email is the administrator's. */
  const problems = checkProfile(draft)
  const validation = useLiveValidation(Object.fromEntries(shown.map((k) => [PROFILE_FIELDS[k], problems[PROFILE_FIELDS[k]]])))
  const dirty = shown.some((k) => draft[k] !== saved[k])
  const done = completion(me)

  const save = () => {
    if (!dirty || update.isPending || !validation.trySubmit()) return
    update.mutateAsync({ current: me, changes: draft })
      .then(() => toast('ok', t('profile.edit.saved')), (e) => validation.fromServer(e, SERVER_FIELDS))
  }

  const field = (key: keyof ProfileChanges, label: string, input: FieldInput, hint?: string, wide = false) => {
    const id = PROFILE_FIELDS[key]
    const error = validation.errorOf(id)
    return (
      <div className={wide ? 'wide' : undefined}>
        <Field id={id} label={label} error={error}>
          <input id={id} className="inp" value={draft[key]} disabled={update.isPending} aria-invalid={!!error} aria-describedby={`${id}-error`}
            onChange={(e) => { const v = e.target.value; setDraft((d) => ({ ...d, [key]: v })); validation.touch(id) }} {...input} />
          {hint && !error && <p className="profile-hint">{hint}</p>}
        </Field>
      </div>
    )
  }

  return (
    <Panel title={t('profile.personal.title')} sub={t('profile.personal.subtitle')}
      aside={(
        <div className="profile-completion">
          <div className="profile-completion-row">
            <span>{t('profile.completion')}</span>
            <strong>{new Intl.NumberFormat(intlLocale(), { style: 'percent' }).format(done)}</strong>
          </div>
          <div className="profile-track" role="progressbar" aria-label={t('profile.completion')} aria-valuenow={Math.round(done * 100)} aria-valuemin={0} aria-valuemax={100}>
            <span style={{ width: `${done * 100}%` }} />
          </div>
        </div>
      )}>
      <form noValidate onSubmit={(e) => { e.preventDefault(); save() }}>
        <div className="profile-form-body">
          {field('name', t('profile.firstName'), { maxLength: NAME_MAX, autoComplete: 'given-name' })}
          {field('surname', t('profile.surname'), { maxLength: NAME_MAX, autoComplete: 'family-name' })}
          {userNameEditable && field('userName', t('profile.userName'), { maxLength: USER_NAME_MAX, dir: 'ltr', autoComplete: 'username' })}
          {field('phoneNumber', t('profile.phone'), { type: 'tel', maxLength: PHONE_MAX, dir: 'ltr', autoComplete: 'tel', placeholder: '+962790000000' }, t('profile.edit.phoneHint'))}
          {/* With the username there are five fields, so the address takes a whole row. */}
          {field('address', t('profile.address'), { maxLength: ADDRESS_MAX, autoComplete: 'street-address' }, undefined, userNameEditable)}
        </div>
        <footer className="profile-form-foot">
          <button type="button" className="btn" disabled={!dirty || update.isPending} onClick={onCancel}>{t('common.cancel')}</button>
          <button type="submit" className="btn btn-primary" disabled={!dirty || update.isPending}>{t('common.saveChanges')}</button>
        </footer>
      </form>
    </Panel>
  )
}

/* One read-only row: an icon and a label, the value at the far end. */
function Row({ icon: Icon, label, children }: { icon?: LucideIcon; label: string; children: ReactNode }) {
  return (
    <div className="profile-row">
      <dt>{Icon && <Icon aria-hidden="true" />}{label}</dt>
      <dd>{children}</dd>
    </div>
  )
}

function ProfileView({ me }: { me: MyProfile }) {
  const { t } = useTranslation()
  const settings = useAbpSettings().data ?? {}
  const [changingPassword, setChangingPassword] = useState(false)
  /* Cancel starts the form afresh from what is saved. */
  const [resets, setResets] = useState(0)
  const { profile } = me
  const name = displayName(profile.name, profile.surname, profile.userName)
  const details = otherDetails(profile.extraProperties)
  const userNameLocked = settings[USER_NAME_UPDATE]?.toLowerCase() === 'false'

  /* Newest first. */
  const activity: [LucideIcon, string, string | null, Intl.DateTimeFormatOptions][] = [
    [LogInIcon, t('profile.lastSignIn'), me.lastSignInTime, MOMENT],
    [KeyRoundIcon, t('profile.passwordChanged'), me.lastPasswordChangeTime, MOMENT],
    [UserPlusIcon, t('profile.memberSince'), me.creationTime, DAY],
  ]

  const passwordNote = profile.isExternal ? t('profile.security.external')
    : me.lastPasswordChangeTime ? t('profile.security.lastChanged', { when: relative(parseUtc(me.lastPasswordChangeTime)) })
    : t('profile.security.neverChanged')

  return (
    <>
      {changingPassword && <ChangePasswordModal me={me} onClose={() => setChangingPassword(false)} />}

      <ProfileHeader me={me} name={name} />

      <div className="profile-grid">
        <div className="profile-main">
          {/* A save brings a new concurrency stamp, so the form starts again from what the server now holds. */}
          <PersonalInformation key={`${profile.concurrencyStamp}-${resets}`} me={me} userNameEditable={!userNameLocked}
            onCancel={() => setResets((n) => n + 1)} />

          <Panel title={t('profile.activity')}>
            <ol className="profile-activity">
              {activity.map(([Icon, label, iso, options]) => (
                <li key={label}>
                  <span className="profile-icon-box round"><Icon aria-hidden="true" /></span>
                  <div className="profile-activity-copy">
                    <span className="profile-activity-title">{label}</span>
                    <span className="profile-activity-date">{iso ? formatDate(parseUtc(iso), options) : <Missing text={t('profile.never')} />}</span>
                  </div>
                  {iso && <span className="profile-activity-when">{relative(parseUtc(iso))}</span>}
                </li>
              ))}
            </ol>
          </Panel>
        </div>

        <aside className="profile-aside">
          <Panel title={t('profile.managed.title')} sub={t('profile.managed.subtitle')} icon={LockKeyholeIcon} divided={false}>
            <dl className="profile-rows">
              <Row icon={MailIcon} label={t('profile.email')}><Contact value={profile.email} /></Row>
              {userNameLocked && <Row icon={AtSignIcon} label={t('profile.userName')}><bdi dir="ltr">{profile.userName}</bdi></Row>}
              <Row icon={ShieldIcon} label={t('profile.roles')}>
                {me.roles.length ? me.roles.map((r) => <span key={r} className="profile-role">{roleLabel(r)}</span>) : <Missing text={t('nav.noRole')} />}
              </Row>
              <Row icon={KeyRoundIcon} label={t('profile.signInMethod')}>{profile.isExternal ? t('profile.external') : t('profile.password')}</Row>
            </dl>
          </Panel>

          <Panel title={t('profile.password')} sub={passwordNote} icon={ShieldCheckIcon} divided={false}>
            {/* Someone who signs in through another provider has no password here to change. */}
            {!profile.isExternal && (
              <div className="profile-card-body">
                <button type="button" className="btn profile-wide-btn" onClick={() => setChangingPassword(true)}>
                  <KeyRoundIcon aria-hidden="true" />{t('profile.changePassword.title')}
                </button>
              </div>
            )}
          </Panel>

          {details.length > 0 && (
            <Panel title={t('profile.additional')} divided={false}>
              <dl className="profile-rows">
                {details.map(([label, value]) => <Row key={label} label={label}><bdi>{value}</bdi></Row>)}
              </dl>
            </Panel>
          )}
        </aside>
      </div>
    </>
  )
}

/* The signed-in user's own record. Anyone signed in may open it and change their own details and password;
 * email and roles are only shown, since an administrator sets them. */
export function MyProfilePage() {
  const { t } = useTranslation()
  const q = useMyProfile()
  return (
    <section className="my-profile">
      {q.isError
        ? <div className="card"><LoadError what={t('load.profile')} error={q.error} onRetry={() => q.refetch()} /></div>
        : !q.data ? <div className="card"><Loading /></div> : <ProfileView me={q.data} />}
    </section>
  )
}
