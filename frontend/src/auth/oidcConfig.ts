import type { UserManagerSettings } from 'oidc-client-ts'

export const oidcConfig: UserManagerSettings = {
  authority: import.meta.env.VITE_API_URL ?? 'https://localhost:44393',
  client_id: 'Portal_App',
  redirect_uri: `${window.location.origin}/callback`,
  post_logout_redirect_uri: `${window.location.origin}/`,
  response_type: 'code',
  scope: 'openid profile email roles Portal',
  automaticSilentRenew: false,
}
