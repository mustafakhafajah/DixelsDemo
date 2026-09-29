import { useCallback } from 'react'
import { useAuth } from 'react-oidc-context'

const API_URL = import.meta.env.VITE_API_URL ?? 'https://localhost:44393'

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
  const url = new URL(path, API_URL)
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

export async function apiRequest<T>(token: string | undefined, method: string, path: string, body?: unknown, query?: Query): Promise<T> {
  const res = await fetch(buildUrl(path, query), {
    method,
    headers: {
      Accept: 'application/json',
      ...(body !== undefined ? { 'Content-Type': 'application/json' } : {}),
      ...(token ? { Authorization: `Bearer ${token}` } : {}),
    },
    body: body !== undefined ? JSON.stringify(body) : undefined,
  })

  if (!res.ok) {
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
    throw new ApiError(res.status, code, message, data, fieldErrors)
  }

  if (res.status === 204) return undefined as T
  const text = await res.text()
  return (text ? JSON.parse(text) : undefined) as T
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

export function errorText(e: unknown): { code: string; message: string; field?: string } {
  if (e instanceof ApiError) return { code: e.code, message: e.message, field: e.fieldErrors[0]?.field }
  if (e instanceof Error) return { code: 'network.error', message: e.message }
  return { code: 'unknown', message: 'Something went wrong.' }
}
