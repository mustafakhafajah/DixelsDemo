import i18n from 'i18next'
import { beforeAll, describe, expect, it } from 'vitest'
import en from '../../locales/en.json'
import type { FieldErrors } from '../../lib/useFieldErrors'
import type { ProfileChanges } from './api'
import { checkPassword, checkProfile, missingPasswordRules, PASSWORD_FIELDS, passwordPolicyFrom, PROFILE_FIELDS } from './validation'

/* The live checks must agree with the server (PortalProfileAppService, ASP.NET Identity), field by field.
 * Codes are compared, not wording, so the tests hold in every language. */

const codes = (errors: FieldErrors) =>
  Object.fromEntries(Object.entries(errors).filter(([, e]) => e).map(([id, e]) => [id, e!.code]))

const valid: ProfileChanges = {
  name: 'Ammar', surname: 'Safwan', userName: 'ammar.safwan', email: 'ammar.safwan@example.com', phoneNumber: '+962790000000', address: '',
}

/* ABP's defaults: 6 characters, a digit, a lowercase and an uppercase letter, and a symbol. */
const abpDefaults = passwordPolicyFrom({})

/* The app's real English texts, so messages are built the way users see them. */
beforeAll(() => i18n.init({ lng: 'en', resources: { en: { translation: en } } }))

describe('checkProfile', () => {
  it('accepts a complete profile, and an empty surname, phone and address', () => {
    expect(codes(checkProfile(valid))).toEqual({})
    expect(codes(checkProfile({ ...valid, surname: '', phoneNumber: '', address: '' }))).toEqual({})
  })

  it('requires a first name, also when it is only spaces', () => {
    expect(codes(checkProfile({ ...valid, name: '' }))).toEqual({ [PROFILE_FIELDS.name]: 'validation.missing_field' })
    expect(codes(checkProfile({ ...valid, name: '   ' }))).toEqual({ [PROFILE_FIELDS.name]: 'validation.missing_field' })
  })

  it('requires a user name made of the characters ASP.NET Identity allows', () => {
    expect(codes(checkProfile({ ...valid, userName: ' ' }))).toEqual({ [PROFILE_FIELDS.userName]: 'validation.missing_field' })
    expect(codes(checkProfile({ ...valid, userName: 'ammar safwan' }))).toEqual({ [PROFILE_FIELDS.userName]: 'validation.invalid_user_name' })
    expect(codes(checkProfile({ ...valid, userName: 'عمار' }))).toEqual({ [PROFILE_FIELDS.userName]: 'validation.invalid_user_name' })
    expect(codes(checkProfile({ ...valid, userName: 'a.b-c_d@e+f' }))).toEqual({})
  })

  it('requires an email that looks like one', () => {
    expect(codes(checkProfile({ ...valid, email: '' }))).toEqual({ [PROFILE_FIELDS.email]: 'validation.missing_field' })
    expect(codes(checkProfile({ ...valid, email: 'ammar@' }))).toEqual({ [PROFILE_FIELDS.email]: 'validation.invalid_email' })
    expect(codes(checkProfile({ ...valid, email: 'ammar example.com' }))).toEqual({ [PROFILE_FIELDS.email]: 'validation.invalid_email' })
  })

  it('takes a phone number as 6 to 15 digits, optionally after +, like the server', () => {
    for (const ok of ['+962790000000', '00962778448455', '123456']) expect(codes(checkProfile({ ...valid, phoneNumber: ok }))).toEqual({})
    for (const bad of ['abc', '12345', '+962 79 000', '0796-000-000', '1234567890123456'])
      expect(codes(checkProfile({ ...valid, phoneNumber: bad }))).toEqual({ [PROFILE_FIELDS.phoneNumber]: 'validation.invalid_phone' })
  })

  it('reports every problem at once', () => {
    expect(Object.keys(codes(checkProfile({ ...valid, name: '', email: 'x', phoneNumber: 'y' })))).toHaveLength(3)
  })
})

describe('passwordPolicyFrom', () => {
  it("falls back to ABP's defaults", () => {
    expect(abpDefaults).toEqual({
      requiredLength: 6, requiredUniqueChars: 1, requireDigit: true, requireLowercase: true, requireUppercase: true, requireNonAlphanumeric: true,
    })
  })

  it("reads ABP's settings, which arrive as strings", () => {
    expect(passwordPolicyFrom({
      'Abp.Identity.Password.RequiredLength': '10',
      'Abp.Identity.Password.RequireDigit': 'False',
      'Abp.Identity.Password.RequireNonAlphanumeric': 'false',
    })).toMatchObject({ requiredLength: 10, requireDigit: false, requireNonAlphanumeric: false, requireUppercase: true })
  })
})

describe('missingPasswordRules', () => {
  it('lists every rule a password breaks', () => {
    expect(missingPasswordRules('abc', abpDefaults)).toHaveLength(4) // length, digit, uppercase, symbol
    expect(missingPasswordRules('Abc12!', abpDefaults)).toEqual([])
  })

  it('counts any non-letter, non-digit as a symbol, and only ASCII as digits and cases', () => {
    expect(missingPasswordRules('Abc123 ', abpDefaults)).toEqual([])
    expect(missingPasswordRules('Abc123ب', abpDefaults)).toHaveLength(1) // an Arabic letter is a letter, not a symbol
  })

  it('follows a looser policy', () => {
    const loose = passwordPolicyFrom({
      'Abp.Identity.Password.RequireDigit': 'False', 'Abp.Identity.Password.RequireUppercase': 'False',
      'Abp.Identity.Password.RequireNonAlphanumeric': 'False', 'Abp.Identity.Password.RequiredLength': '4',
    })
    expect(missingPasswordRules('abcd', loose)).toEqual([])
  })
})

describe('checkPassword', () => {
  const good = { current: '1q2w3E*', next: 'N3w-pass!', repeat: 'N3w-pass!' }

  it('accepts a good change', () => {
    expect(codes(checkPassword(good, abpDefaults, true))).toEqual({})
  })

  it('puts each problem under its own field', () => {
    expect(codes(checkPassword({ current: '', next: '', repeat: '' }, abpDefaults, true))).toEqual({
      [PASSWORD_FIELDS.current]: 'validation.missing_field',
      [PASSWORD_FIELDS.next]: 'validation.missing_field',
      [PASSWORD_FIELDS.repeat]: 'validation.missing_field',
    })
    expect(codes(checkPassword({ ...good, next: 'weak', repeat: 'weak' }, abpDefaults, true))).toEqual({ [PASSWORD_FIELDS.next]: 'validation.password_rules' })
    /* Every missing rule in one sentence under the new password. */
    expect(checkPassword({ ...good, next: 'weak', repeat: 'weak' }, abpDefaults, true)[PASSWORD_FIELDS.next]?.message)
      .toBe('The password needs at least 6 characters, a digit (0–9), an uppercase letter (A–Z) and a symbol, such as ! or #.')
    expect(codes(checkPassword({ ...good, repeat: 'N3w-pass?' }, abpDefaults, true))).toEqual({ [PASSWORD_FIELDS.repeat]: 'validation.passwords_differ' })
    expect(codes(checkPassword({ current: 'N3w-pass!', next: 'N3w-pass!', repeat: 'N3w-pass!' }, abpDefaults, true))).toEqual({ [PASSWORD_FIELDS.next]: 'validation.same_password' })
  })

  it('needs no current password from someone who has none yet', () => {
    expect(codes(checkPassword({ ...good, current: '' }, abpDefaults, false))).toEqual({})
  })
})
