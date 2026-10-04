import { useState, type ReactNode } from 'react'
import { useTranslation } from 'react-i18next'
import { displayName } from '../../api/types'
import { PageActions } from '../../app/pageActions'
import { initials, LoadError, Loading } from '../../components/bits'
import { formatDate, parseUtc } from '../../lib/dateUtils'
import { roleLabel } from '../users/api'
import { ADDRESS_PROPERTY, useMyProfile, type MyProfile } from './api'
import { ChangePasswordModal, EditProfileModal } from './ProfileForms'
import './profile.css'

const DAY: Intl.DateTimeFormatOptions = { day: 'numeric', month: 'long', year: 'numeric' }
/* "4 October 2026, 14:05 UTC": the app shows every time in UTC, so it says so. */
const MOMENT: Intl.DateTimeFormatOptions = { ...DAY, hour: '2-digit', minute: '2-digit', hour12: false, timeZoneName: 'short' }

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

function VerifiedPill({ verified }: { verified: boolean }) {
  const { t } = useTranslation()
  return verified
    ? <span className="pill pill-confirmed"><span className="dot" />{t('profile.verified')}</span>
    : <span className="pill pill-cancelled"><span className="dot" />{t('profile.notVerified')}</span>
}

/* An email or phone number reads left to right in any language; with its verified pill beside it. */
function Contact({ value, verified }: { value: string | null; verified: boolean }) {
  if (!value?.trim()) return <Missing />
  return <><bdi dir="ltr">{value}</bdi><VerifiedPill verified={verified} /></>
}

function Section({ title, children }: { title: string; children: ReactNode }) {
  return (
    <div className="card">
      <div className="card-head"><h2 className="card-title">{title}</h2></div>
      <dl className="kv">{children}</dl>
    </div>
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

type Editing = 'profile' | 'password' | null

function ProfileView({ me }: { me: MyProfile }) {
  const { t } = useTranslation()
  const [editing, setEditing] = useState<Editing>(null)
  const close = () => setEditing(null)
  const { profile } = me
  const name = displayName(profile.name, profile.surname, profile.userName)
  const address = profile.extraProperties?.[ADDRESS_PROPERTY]
  const details = otherDetails(profile.extraProperties)

  return (
    <>
      <PageActions>
        {/* Someone who signs in through another provider has no password here to change. */}
        {!profile.isExternal && <button type="button" className="btn" onClick={() => setEditing('password')}>{t('profile.changePassword.title')}</button>}
        <button type="button" className="btn btn-primary" onClick={() => setEditing('profile')}>{t('profile.edit.title')}</button>
      </PageActions>
      {editing === 'profile' && <EditProfileModal me={me} onClose={close} />}
      {editing === 'password' && <ChangePasswordModal me={me} onClose={close} />}

      <div className="card profile-head">
        <div className="avatar" aria-hidden="true">{initials(name)}</div>
        <div style={{ minWidth: 0 }}>
          <p className="profile-name"><bdi>{name}</bdi></p>
          <p className="profile-sub"><bdi dir="ltr">@{profile.userName}</bdi></p>
        </div>
      </div>

      <Section title={t('profile.contact')}>
        <Field label={t('profile.email')}><Contact value={profile.email} verified={me.emailConfirmed} /></Field>
        <Field label={t('profile.phone')}><Contact value={profile.phoneNumber} verified={me.phoneNumberConfirmed} /></Field>
        <Field label={t('profile.address')}><Text value={typeof address === 'string' ? address : null} /></Field>
      </Section>

      <Section title={t('profile.account')}>
        <Field label={t('profile.userName')}><bdi dir="ltr">{profile.userName}</bdi></Field>
        <Field label={t('profile.firstName')}><Text value={profile.name} /></Field>
        <Field label={t('profile.surname')}><Text value={profile.surname} /></Field>
        <Field label={t('profile.roles')}>
          {me.roles.length
            ? me.roles.map((r) => <span key={r} className="pill pill-ended"><span className="dot" />{roleLabel(r)}</span>)
            : <Missing text={t('nav.noRole')} />}
        </Field>
        <Field label={t('profile.tenant')}>{me.tenantName ? <bdi>{me.tenantName}</bdi> : t('profile.host')}</Field>
        <Field label={t('profile.signInMethod')}>{profile.isExternal ? t('profile.external') : t('profile.password')}</Field>
        <Field label={t('profile.twoFactor')}>{me.twoFactorEnabled ? t('profile.on') : t('profile.off')}</Field>
      </Section>

      <Section title={t('profile.activity')}>
        <Field label={t('profile.memberSince')}><When iso={me.creationTime} options={DAY} /></Field>
        <Field label={t('profile.lastSignIn')}><When iso={me.lastSignInTime} options={MOMENT} /></Field>
        <Field label={t('profile.passwordChanged')}><When iso={me.lastPasswordChangeTime} options={MOMENT} /></Field>
      </Section>

      {details.length > 0 && (
        <Section title={t('profile.additional')}>
          {details.map(([label, value]) => <Field key={label} label={label}><bdi>{value}</bdi></Field>)}
        </Section>
      )}
    </>
  )
}

/* The signed-in user's own record. Anyone signed in may open it and change their own details and password;
 * roles and tenant are only shown, since an administrator sets them. */
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
