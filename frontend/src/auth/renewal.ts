import { userManager } from './oidcConfig'

let renewing: Promise<string | undefined> | null = null

/* A new access token without leaving the page (with the refresh token). One renewal at a time: every request
 * refused meanwhile waits for the same one. Undefined when the session could not be renewed.
 * Called on the client itself, so the app is not swapped for the "Please wait…" screen while it runs. */
export function renewSession(): Promise<string | undefined> {
  renewing ??= userManager.signinSilent()
    .then((user) => user?.access_token, () => undefined)
    .finally(() => { renewing = null })
  return renewing
}

let leaving = false

/* The session is over: off to the sign-in page, and afterwards back to the page the user was on (with its
 * filters), the same way the Sign in button remembers it. Only once, however many requests were refused. */
export function signInAgain() {
  if (leaving) return
  leaving = true
  const base = import.meta.env.BASE_URL.replace(/\/$/, '')
  const { pathname, search } = window.location
  const path = base && pathname.startsWith(base) ? pathname.slice(base.length) : pathname
  userManager.signinRedirect({ state: { returnTo: path + search } }).catch(() => { leaving = false })
}
