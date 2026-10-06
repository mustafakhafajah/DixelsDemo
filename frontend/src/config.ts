/* Where the app finds the API and sign-in server. It comes from VITE_API_URL: frontend/.env in development,
 * .env.production for the shared server (see .env.example). It may carry a path (https://host/server), so request
 * paths are appended to it, never resolved against it; a trailing slash is dropped. */
const apiUrl = import.meta.env.VITE_API_URL
if (!apiUrl) throw new Error('VITE_API_URL is not set. Add it to frontend/.env (see frontend/.env.example).')

export const API_URL = apiUrl.replace(/\/+$/, '')
