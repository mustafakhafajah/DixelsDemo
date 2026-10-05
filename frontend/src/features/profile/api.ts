import { useMutation, useQuery } from '@tanstack/react-query'
import { useApi, useApiBlob } from '../../api/client'
import { blobToDataUrl } from '../../lib/image'
import { PERMISSION_KEYS, useEnabled, useInvalidate } from '../../api/hooks'

/* The signed-in user's own record from /api/app/my-profile: ABP's own profile, plus what ABP's profile leaves
 * out (confirmations, roles, tenant and dates). Changes go to ABP's own my-profile endpoints, which only ever
 * touch the caller and have no way to change roles or tenant. */

/* The extra property the server adds to ABP's user for the address (PortalUserConsts.AddressPropertyName). */
export const ADDRESS_PROPERTY = 'Address'
/* PortalUserConsts.MaxAddressLength. */
export const ADDRESS_MAX = 256

export interface MyProfile {
  /* ABP's ProfileDto. */
  profile: {
    userName: string
    email: string
    name: string | null
    surname: string | null
    phoneNumber: string | null
    /* Signs in through another provider rather than with a password set here. */
    isExternal: boolean
    hasPassword: boolean
    /* ABP extra properties by name, e.g. { Address: '12 Rainbow Street' }. */
    extraProperties: Record<string, unknown> | null
    /* Sent back with a change, so one made elsewhere in the meantime is not overwritten. */
    concurrencyStamp: string
  }
  emailConfirmed: boolean
  phoneNumberConfirmed: boolean
  twoFactorEnabled: boolean
  roles: string[]
  /* Null for a user of the host. */
  tenantName: string | null
  /* UTC instants as ISO strings; null when it never happened. */
  creationTime: string
  lastSignInTime: string | null
  lastPasswordChangeTime: string | null
  /* Changes whenever the profile picture does; null when there is none. */
  pictureVersion: string | null
}

/* What the user may change about themselves. */
export interface ProfileChanges {
  userName: string
  email: string
  name: string
  surname: string
  phoneNumber: string
  address: string
}

const MY_PROFILE_KEY = ['my-profile']

export function useMyProfile() {
  const api = useApi()
  return useQuery({
    queryKey: MY_PROFILE_KEY,
    queryFn: () => api<MyProfile>('GET', '/api/app/my-profile'),
    enabled: useEnabled(),
  })
}

const orNull = (s: string) => s.trim() || null

/* ABP's PUT /api/account/my-profile. Other extra properties are sent back unchanged. The name in the
 * sidebar comes from ABP's application configuration, so that is refreshed too. */
export function useUpdateMyProfile() {
  const api = useApi()
  const invalidate = useInvalidate()
  return useMutation({
    mutationFn: ({ current, changes }: { current: MyProfile; changes: ProfileChanges }) =>
      api('PUT', '/api/account/my-profile', {
        userName: changes.userName.trim(),
        email: changes.email.trim(),
        name: orNull(changes.name),
        surname: orNull(changes.surname),
        phoneNumber: orNull(changes.phoneNumber),
        concurrencyStamp: current.profile.concurrencyStamp,
        extraProperties: { ...current.profile.extraProperties, [ADDRESS_PROPERTY]: orNull(changes.address) },
      }),
    onSuccess: () => invalidate([MY_PROFILE_KEY, ...PERMISSION_KEYS]),
  })
}

const PICTURE_PATH = '/api/app/my-profile/picture'

/* The picture, fetched with the access token, as an address an <img> can show (null when there is none). Keyed by
 * its version, so a new picture is fetched at once and an unchanged one is not fetched again. */
export function useMyPicture(version: string | null | undefined) {
  const blob = useApiBlob()
  return useQuery({
    queryKey: [...MY_PROFILE_KEY, 'picture', version],
    queryFn: async () => {
      const picture = await blob(PICTURE_PATH)
      return picture ? blobToDataUrl(picture) : null
    },
    enabled: useEnabled() && !!version,
    staleTime: Infinity,
  })
}

/* The form field the server reads the picture from (ProfilePictureController). */
const PICTURE_FIELD = 'picture'

export function useSetMyPicture() {
  const api = useApi()
  const invalidate = useInvalidate()
  return useMutation({
    mutationFn: (picture: Blob) => {
      const form = new FormData()
      form.append(PICTURE_FIELD, picture, 'profile-picture.jpg')
      return api('PUT', PICTURE_PATH, form)
    },
    onSuccess: () => invalidate([MY_PROFILE_KEY]),
  })
}

export function useRemoveMyPicture() {
  const api = useApi()
  const invalidate = useInvalidate()
  return useMutation({
    mutationFn: () => api('DELETE', PICTURE_PATH),
    onSuccess: () => invalidate([MY_PROFILE_KEY]),
  })
}

/* ABP's POST /api/account/my-profile/change-password; currentPassword is left out when the user has none yet. */
export function useChangeMyPassword() {
  const api = useApi()
  const invalidate = useInvalidate()
  return useMutation({
    mutationFn: (input: { currentPassword?: string; newPassword: string }) =>
      api('POST', '/api/account/my-profile/change-password', input),
    onSuccess: () => invalidate([MY_PROFILE_KEY]),
  })
}
