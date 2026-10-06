import { useCallback } from 'react'
import { useAuth } from 'react-oidc-context'
import i18n from 'i18next'
import { renewSession, signInAgain } from '../auth/renewal'
import { API_URL } from '../config'

/* One message the server tied to a request field, e.g. { field: 'name', message: 'Give the space a name.' }. */
export interface ServerFieldError { field: string; code: string; message: string }

export class ApiError extends Error {
  status: number
  code: string
  data: Record<string, unknown>
  /* Messages the server tied to a field: its rule errors say which field in data.field, and ABP's own
   * checks ([Required], [Range]…) list the members they are about. Empty when no field is known. */
  fieldErrors: ServerFieldError[]

  constructor(status: number, code: string, message: string, data: Record<string, unknown> = {}, fieldErrors: ServerFieldError[] = []) {
    super(message)
    this.status = status
    this.code = code
    this.data = data
    this.fieldErrors = fieldErrors
  }
}

/* 'Name' / '$.name' / 'input.OpenHour' -> 'name' / 'openHour', matching the request's camelCase keys. */
const fieldKey = (member: string) => {
  const last = member.replace(/^\$\./, '').split('.').pop() ?? member
  return last.charAt(0).toLowerCase() + last.slice(1)
}

type QueryValue = string | number | boolean | null | undefined
type Query = Record<string, QueryValue | string[]>

/* A list is sent as a repeated key (TypeIds=a&TypeIds=b), which is how ASP.NET binds List<T>. */
export function buildUrl(path: string, query?: Query): string {
  const url = new URL(API_URL + (path.startsWith('/') ? path : `/${path}`))
  Object.entries(query ?? {}).forEach(([k, v]) => {
    if (Array.isArray(v)) v.forEach((item) => url.searchParams.append(k, item))
    else if (v !== undefined && v !== null && v !== '') url.searchParams.set(k, String(v))
  })
  return url.toString()
}

interface AbpErrorBody {
  error?: {
    code?: string | null
    message?: string
    data?: Record<string, unknown>
    validationErrors?: { message: string; members?: string[] | null }[] | null
  }
}

async function toApiError(res: Response): Promise<ApiError> {
  const parsed = (await res.json().catch(() => null)) as AbpErrorBody | null
  const e = parsed?.error
  const validation = e?.validationErrors?.map((v) => v.message).join(' ')
  const code = e?.code || (res.status === 400 ? 'validation.invalid_request' : res.status === 401 ? 'auth.unauthorized'
    : res.status === 403 ? 'access.forbidden' : `http.${res.status}`)
  const message = validation || e?.message || res.statusText
  const data = e?.data ?? {}
  const fieldErrors: ServerFieldError[] = typeof data.field === 'string'
    ? [{ field: data.field, code, message }]
    : (e?.validationErrors ?? []).filter((v) => v.members?.length).map((v) => ({ field: fieldKey(v.members![0]), code, message: v.message }))
  return new ApiError(res.status, code, message, data, fieldErrors)
}

/* One request; throws ApiError when the server refuses. A FormData body (a file upload) is sent as it is, and the
 * browser sets its multipart type; any other body goes as JSON. accept: what answer is wanted (JSON, or a file).
 * A signed-in request refused with 401 (the access token ran out) renews the session once and is sent again;
 * when that fails too, the user is sent to sign in and comes back to the same page afterwards. */
async function send(token: string | undefined, method: string, path: string, body?: unknown, query?: Query, accept = 'application/json'): Promise<Response> {
  const res = await sendOnce(token, method, path, body, query, accept)
  if (res.status === 401 && token) {
    const renewed = await renewSession()
    const retry = renewed ? await sendOnce(renewed, method, path, body, query, accept) : res
    if (retry.status === 401) signInAgain()
    if (!retry.ok) throw await toApiError(retry)
    return retry
  }
  if (!res.ok) throw await toApiError(res)
  return res
}

function sendOnce(token: string | undefined, method: string, path: string, body: unknown, query: Query | undefined, accept: string): Promise<Response> {
  const form = body instanceof FormData
  return fetch(buildUrl(path, query), {
    method,
    /* The access token is the only credential. When the portal and the server share one address, the browser
     * would also send the server's sign-in cookie, and the server then demands an anti-forgery token. */
    credentials: 'omit',
    headers: {
      Accept: accept,
      /* The server answers in this language: names, notes and error messages. */
      'Accept-Language': i18n.language,
      ...(body !== undefined && !form ? { 'Content-Type': 'application/json' } : {}),
      ...(token ? { Authorization: `Bearer ${token}` } : {}),
    },
    body: body === undefined ? undefined : form ? body : JSON.stringify(body),
  })
}

export async function apiRequest<T>(token: string | undefined, method: string, path: string, body?: unknown, query?: Query): Promise<T> {
  const res = await send(token, method, path, body, query)
  if (res.status === 204) return undefined as T
  const text = await res.text()
  return (text ? JSON.parse(text) : undefined) as T
}

/* A file the server sends back, e.g. a picture; null when there is none (204, or an empty body). */
export async function apiBlob(token: string | undefined, path: string): Promise<Blob | null> {
  const res = await send(token, 'GET', path, undefined, undefined, '*/*')
  if (res.status === 204) return null
  const blob = await res.blob()
  return blob.size > 0 ? blob : null
}

/* Request function bound to the signed-in user's access token. */
export function useApi() {
  const auth = useAuth()
  const token = auth.user?.access_token
  return useCallback(
    <T,>(method: string, path: string, body?: unknown, query?: Query) => apiRequest<T>(token, method, path, body, query),
    [token],
  )
}

/* Fetches a file with the signed-in user's access token (an <img src> could not send it). */
export function useApiBlob() {
  const auth = useAuth()
  const token = auth.user?.access_token
  return useCallback((path: string) => apiBlob(token, path), [token])
}

export function errorText(e: unknown): { code: string; message: string; field?: string } {
  if (e instanceof ApiError) return { code: e.code, message: e.message, field: e.fieldErrors[0]?.field }
  if (e instanceof Error) return { code: 'network.error', message: e.message }
  return { code: 'unknown', message: i18n.t('common.somethingWrong') }
}
