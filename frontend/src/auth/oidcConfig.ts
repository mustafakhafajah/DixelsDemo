import type { UserManagerSettings } from 'oidc-client-ts'

export const oidcConfig: UserManagerSettings = {
  authority: import.meta.env.VITE_API_URL ?? 'https://localhost:44393',
  client_id: 'Portal_App',
  /* BASE_URL is where the app lives: "/" in development, "/portal/" on the shared server. */
  redirect_uri: `${window.location.origin}${import.meta.env.BASE_URL}callback`,
  /* No trailing slash: the sign-in server stores this address without one and only accepts an exact match. */
  post_logout_redirect_uri: `${window.location.origin}${import.meta.env.BASE_URL}`.replace(/\/$/, ''),
  response_type: 'code',
  scope: 'openid profile email roles Portal',
  automaticSilentRenew: false,
}
