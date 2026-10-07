import { useState, type InputHTMLAttributes, type ReactNode } from 'react'
import { useTranslation } from 'react-i18next'
import { useAbpSettings } from '../../api/hooks'
import { displayName } from '../../api/types'
import { PageActions } from '../../app/pageActions'
import { LoadError, Loading } from '../../components/bits'
import { formatDate, parseUtc } from '../../lib/dateUtils'
import { roleLabel } from '../users/api'
import { ADDRESS_MAX, ADDRESS_PROPERTY, useMyProfile, useUpdateMyProfile, type MyProfile, type ProfileChanges } from './api'
import { InlineField } from './InlineField'
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

/* The server's name for each field, so a refusal it ties to one lands under it. */
const SERVER_FIELD: Record<keyof ProfileChanges, string> = {
  name: 'name', surname: 'surname', userName: 'userName', email: 'email', phoneNumber: 'phoneNumber', address: 'address',
}

/* Any value a field may be missing shows a quiet "Not provided" (or "Never" for a date) instead of a blank. */
function Missing({ text }: { text?: string }) {
  const { t } = useTranslation()
  return <span className="missing">{text ?? t('profile.notProvided')}</span>
}

function Text({ value }: { value: string | null | undefined }) {
  return value?.trim() ? <bdi>{value}</bdi> : <Missing />
}

function When({ iso, options }: { iso: string | null; options: Intl.DateTimeFormatOptions }) {
  const { t } = useTranslation()
  return iso ? <span>{formatDate(parseUtc(iso), options)}</span> : <Missing text={t('profile.never')} />
}

/* An email or phone number reads left to right in any language. */
function Contact({ value }: { value: string | null }) {
  if (!value?.trim()) return <Missing />
  return <bdi dir="ltr">{value}</bdi>
}

/* A group of rows under a strong rule, rather than a boxed card. */
function Section({ title, children }: { title: string; children: ReactNode }) {
  return (
    <section className="profile-section">
      <h2 className="profile-section-title">{title}</h2>
      <dl className="kv">{children}</dl>
    </section>
  )
}

function Field({ label, children }: { label: string; children: ReactNode }) {
  return <><dt>{label}</dt><dd>{children}</dd></>
}

/* Extra properties other than the address, which has its own row; a value that is empty is left out. */
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

function ProfileView({ me }: { me: MyProfile }) {
  const { t } = useTranslation()
  const update = useUpdateMyProfile()
  const settings = useAbpSettings().data ?? {}
  const [changingPassword, setChangingPassword] = useState(false)
  /* One field is edited at a time; opening another leaves the first unchanged. */
  const [editing, setEditing] = useState<keyof ProfileChanges | null>(null)
  const { profile } = me
  const current = changesOf(me)
  const name = displayName(profile.name, profile.surname, profile.userName)
  const details = otherDetails(profile.extraProperties)
  const userNameLocked = settings[USER_NAME_UPDATE]?.toLowerCase() === 'false'

  /* A row the user can change in place: the same checks as the server, as they type, and the server's own word on save. */
  const editable = (key: keyof ProfileChanges, label: string, display: ReactNode, input: Omit<InputHTMLAttributes<HTMLInputElement>, 'id' | 'value' | 'onChange' | 'onKeyDown'>, hint?: string) => (
    <InlineField id={PROFILE_FIELDS[key]} label={label} value={current[key]} display={display}
      editing={editing === key} onEdit={() => setEditing(key)} onClose={() => setEditing(null)}
      check={(draft) => checkProfile({ ...current, [key]: draft })[PROFILE_FIELDS[key]]}
      onSave={(draft) => update.mutateAsync({ current: me, changes: { ...current, [key]: draft } })}
      serverField={SERVER_FIELD[key]} hint={hint} input={input} />
  )

  /* Newest first. */
  const activity: [string, string | null, Intl.DateTimeFormatOptions][] = [
    [t('profile.lastSignIn'), me.lastSignInTime, MOMENT],
    [t('profile.passwordChanged'), me.lastPasswordChangeTime, MOMENT],
    [t('profile.memberSince'), me.creationTime, DAY],
  ]

  return (
    <>
      <PageActions>
        {/* Someone who signs in through another provider has no password here to change. */}
        {!profile.isExternal && <button type="button" className="btn" onClick={() => setChangingPassword(true)}>{t('profile.changePassword.title')}</button>}
      </PageActions>
      {changingPassword && <ChangePasswordModal me={me} onClose={() => setChangingPassword(false)} />}

      <div className="profile-layout">
        {/* Who you are at a glance, laid out like a member's passport, with what happened lately beneath it. */}
        <aside className="profile-side">
          <div className="card passport">
            <div className="passport-top">
              <span className="passport-label mono">{t('profile.passport.label')}</span>
              <bdi className="passport-label mono" dir="ltr">@{profile.userName}</bdi>
            </div>
            <div className="passport-id">
              <div className="passport-photo">
                <MyAvatar name={name} className="avatar" />
                <ProfilePhoto me={me} />
              </div>
              <div className="passport-name">
                <p className="profile-name"><bdi>{name}</bdi></p>
                <p className="profile-sub mono"><bdi dir="ltr">{profile.email}</bdi></p>
              </div>
              <div className="passport-tags">
                {me.roles.map((r) => <span key={r} className="passport-tag strong mono">{roleLabel(r)}</span>)}
              </div>
            </div>
          </div>

          <section className="card profile-activity">
            <h2 className="profile-section-title plain">{t('profile.activity')}</h2>
            <ol className="timeline">
              {activity.map(([label, iso, options], i) => (
                <li key={label} className={i === 0 && iso ? 'latest' : undefined}>
                  <span className="timeline-title">{label}</span>
                  <span className="timeline-date mono"><When iso={iso} options={options} /></span>
                </li>
              ))}
            </ol>
          </section>
        </aside>

        <div className="profile-details">
          <header className="profile-intro">
            <h2 className="profile-intro-title">{t('profile.passport.heading', { name })}</h2>
            <p className="profile-intro-sub">{t('profile.passport.intro')}</p>
          </header>

          <Section title={t('profile.contact')}>
            <Field label={t('profile.email')}>
              <Contact value={profile.email} />
              <span className="profile-hint">{t('profile.edit.setByAdministrator')}</span>
            </Field>
            {editable('phoneNumber', t('profile.phone'), <Contact value={profile.phoneNumber} />,
              { type: 'tel', maxLength: PHONE_MAX, dir: 'ltr', autoComplete: 'tel', placeholder: '+962790000000' })}
            {editable('address', t('profile.address'), <Text value={current.address} />, { maxLength: ADDRESS_MAX, autoComplete: 'street-address' })}
          </Section>

          <Section title={t('profile.account')}>
            {userNameLocked
              ? <Field label={t('profile.userName')}><bdi dir="ltr">{profile.userName}</bdi><span className="profile-hint">{t('profile.edit.setByAdministrator')}</span></Field>
              : editable('userName', t('profile.userName'), <bdi dir="ltr">{profile.userName}</bdi>, { maxLength: USER_NAME_MAX, dir: 'ltr', autoComplete: 'username' })}
            {editable('name', t('profile.firstName'), <Text value={profile.name} />, { maxLength: NAME_MAX, autoComplete: 'given-name' })}
            {editable('surname', t('profile.surname'), <Text value={profile.surname} />, { maxLength: NAME_MAX, autoComplete: 'family-name' })}
            <Field label={t('profile.roles')}>
              {me.roles.length
                ? me.roles.map((r) => <span key={r} className="pill pill-ended"><span className="dot" />{roleLabel(r)}</span>)
                : <Missing text={t('nav.noRole')} />}
            </Field>
              <Field label={t('profile.signInMethod')}>{profile.isExternal ? t('profile.external') : t('profile.password')}</Field>
          </Section>

          {details.length > 0 && (
            <Section title={t('profile.additional')}>
              {details.map(([label, value]) => <Field key={label} label={label}><bdi>{value}</bdi></Field>)}
            </Section>
          )}
        </div>
      </div>
    </>
  )
}

/* The signed-in user's own record. Anyone signed in may open it and change their own details where they are
 * shown, and their password; email and roles are only shown, since an administrator sets them. */
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
