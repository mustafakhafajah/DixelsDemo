import i18n from 'i18next'
import type { FieldError, FieldErrors } from '../../lib/useFieldErrors'
import { intlLocale } from '../../i18n/languages'
import type { ProfileChanges } from './api'

/* The same rules the server applies (PortalProfileAppService and ASP.NET Identity), checked as the user types so
 * each problem shows under its field at once. The server still checks everything. */

/* PortalUserConsts.PhoneNumberPattern: 6 to 15 digits, optionally after a "+". */
const PHONE = /^\+?[0-9]{6,15}$/
/* ASP.NET Identity's default AllowedUserNameCharacters, which ABP keeps. */
const USER_NAME = /^[A-Za-z0-9\-._@+]+$/
/* Only a shape check; the server has the final word. */
const EMAIL = /^[^\s@]+@[^\s@]+\.[^\s@]+$/

const problem = (code: string, message: string): FieldError => ({ code, message })

/* Field ids of the Edit profile form. */
export const PROFILE_FIELDS = {
  name: 'pf-name', surname: 'pf-surname', userName: 'pf-username', email: 'pf-email', phoneNumber: 'pf-phone', address: 'pf-address',
} as const

export function checkProfile(form: ProfileChanges): FieldErrors {
  const userName = form.userName.trim()
  const email = form.email.trim()
  const phone = form.phoneNumber.trim()
  return {
    [PROFILE_FIELDS.name]: form.name.trim() ? undefined : problem('validation.missing_field', i18n.t('profile.edit.firstNameMissing')),
    [PROFILE_FIELDS.userName]: !userName ? problem('validation.missing_field', i18n.t('profile.edit.userNameMissing'))
      : USER_NAME.test(userName) ? undefined : problem('validation.invalid_user_name', i18n.t('profile.edit.userNameInvalid')),
    [PROFILE_FIELDS.email]: !email ? problem('validation.missing_field', i18n.t('profile.edit.emailMissing'))
      : EMAIL.test(email) ? undefined : problem('validation.invalid_email', i18n.t('profile.edit.emailInvalid')),
    [PROFILE_FIELDS.phoneNumber]: !phone || PHONE.test(phone) ? undefined : problem('validation.invalid_phone', i18n.t('profile.edit.phoneInvalid')),
  }
}

/* ABP's password settings (Abp.Identity.Password.*), which it shows to clients. */
export interface PasswordPolicy {
  requiredLength: number
  requiredUniqueChars: number
  requireDigit: boolean
  requireLowercase: boolean
  requireUppercase: boolean
  requireNonAlphanumeric: boolean
}

const SETTING = 'Abp.Identity.Password.'
/* ABP's own defaults, used until its settings have loaded. */
const DEFAULT_POLICY: PasswordPolicy = {
  requiredLength: 6, requiredUniqueChars: 1, requireDigit: true, requireLowercase: true, requireUppercase: true, requireNonAlphanumeric: true,
}

export function passwordPolicyFrom(settings: Record<string, string>): PasswordPolicy {
  const num = (name: string, fallback: number) => {
    const n = Number(settings[SETTING + name])
    return Number.isFinite(n) && settings[SETTING + name] !== undefined ? n : fallback
  }
  const flag = (name: string, fallback: boolean) => (settings[SETTING + name] === undefined ? fallback : settings[SETTING + name].toLowerCase() === 'true')
  return {
    requiredLength: num('RequiredLength', DEFAULT_POLICY.requiredLength),
    requiredUniqueChars: num('RequiredUniqueChars', DEFAULT_POLICY.requiredUniqueChars),
    requireDigit: flag('RequireDigit', DEFAULT_POLICY.requireDigit),
    requireLowercase: flag('RequireLowercase', DEFAULT_POLICY.requireLowercase),
    requireUppercase: flag('RequireUppercase', DEFAULT_POLICY.requireUppercase),
    requireNonAlphanumeric: flag('RequireNonAlphanumeric', DEFAULT_POLICY.requireNonAlphanumeric),
  }
}

/* What the password still needs, as ASP.NET Identity's PasswordValidator judges it: digits and letters are ASCII,
 * and a "symbol" is anything that is not a letter or digit in any script. */
export function missingPasswordRules(password: string, policy: PasswordPolicy): string[] {
  const needs: string[] = []
  if (password.length < policy.requiredLength) needs.push(i18n.t('profile.changePassword.rule.length', { count: policy.requiredLength }))
  if (policy.requireDigit && !/[0-9]/.test(password)) needs.push(i18n.t('profile.changePassword.rule.digit'))
  if (policy.requireLowercase && !/[a-z]/.test(password)) needs.push(i18n.t('profile.changePassword.rule.lowercase'))
  if (policy.requireUppercase && !/[A-Z]/.test(password)) needs.push(i18n.t('profile.changePassword.rule.uppercase'))
  if (policy.requireNonAlphanumeric && !/[^\p{L}\p{N}]/u.test(password)) needs.push(i18n.t('profile.changePassword.rule.symbol'))
  if (new Set(password).size < policy.requiredUniqueChars) needs.push(i18n.t('profile.changePassword.rule.unique', { count: policy.requiredUniqueChars }))
  return needs
}

/* Field ids of the Change password form. */
export const PASSWORD_FIELDS = { current: 'pw-current', next: 'pw-new', repeat: 'pw-repeat' } as const

export function checkPassword(input: { current: string; next: string; repeat: string }, policy: PasswordPolicy, needsCurrent: boolean): FieldErrors {
  const needs = input.next ? missingPasswordRules(input.next, policy) : []
  const list = new Intl.ListFormat(intlLocale(), { type: 'conjunction' }).format(needs)
  return {
    [PASSWORD_FIELDS.current]: needsCurrent && !input.current ? problem('validation.missing_field', i18n.t('profile.changePassword.currentMissing')) : undefined,
    [PASSWORD_FIELDS.next]: !input.next ? problem('validation.missing_field', i18n.t('profile.changePassword.newMissing'))
      : needs.length ? problem('validation.password_rules', i18n.t('profile.changePassword.needs', { list }))
      : needsCurrent && input.next === input.current ? problem('validation.same_password', i18n.t('profile.changePassword.sameAsCurrent'))
      : undefined,
    [PASSWORD_FIELDS.repeat]: !input.repeat ? problem('validation.missing_field', i18n.t('profile.changePassword.repeatMissing'))
      : input.repeat !== input.next ? problem('validation.passwords_differ', i18n.t('profile.changePassword.mismatch'))
      : undefined,
  }
}
