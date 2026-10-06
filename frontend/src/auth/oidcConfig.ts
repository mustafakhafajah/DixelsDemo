import { UserManager, type UserManagerSettings } from 'oidc-client-ts'
import { API_URL } from '../config'

const oidcConfig: UserManagerSettings = {
  authority: API_URL,
  client_id: 'Portal_App',
  /* BASE_URL is where the app lives: "/" in development, "/portal/" on the shared server. */
  redirect_uri: `${window.location.origin}${import.meta.env.BASE_URL}callback`,
  /* No trailing slash: the sign-in server stores this address without one and only accepts an exact match. */
  post_logout_redirect_uri: `${window.location.origin}${import.meta.env.BASE_URL}`.replace(/\/$/, ''),
  response_type: 'code',
  /* offline_access: the server also hands out a refresh token, so the access token is renewed in the background
   * (shortly before it runs out) without leaving the page. */
  scope: 'openid profile email roles Portal offline_access',
  automaticSilentRenew: true,
}

/* The one sign-in client, shared by the AuthProvider and the request code (src/auth/renewal.ts). */
export const userManager = new UserManager(oidcConfig)
