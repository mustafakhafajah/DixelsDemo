import { useCallback } from 'react'
import { useAuth } from 'react-oidc-context'

const API_URL = import.meta.env.VITE_API_URL ?? 'https://localhost:44393'

export class ApiError extends Error {
  status: number
  code: string
  data: Record<string, unknown>

  constructor(status: number, code: string, message: string, data: Record<string, unknown> = {}) {
    super(message)
    this.status = status
    this.code = code
    this.data = data
  }
}

type Query = Record<string, string | number | boolean | null | undefined>

export function buildUrl(path: string, query?: Query): string {
  const url = new URL(path, API_URL)
  Object.entries(query ?? {}).forEach(([k, v]) => {
    if (v !== undefined && v !== null && v !== '') url.searchParams.set(k, String(v))
  })
  return url.toString()
}

interface AbpErrorBody {
  error?: {
    code?: string | null
    message?: string
    data?: Record<string, unknown>
    validationErrors?: { message: string }[] | null
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
    throw new ApiError(res.status, code, validation || e?.message || res.statusText, e?.data ?? {})
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

export function errorText(e: unknown): { code: string; message: string } {
  if (e instanceof ApiError) return { code: e.code, message: e.message }
  if (e instanceof Error) return { code: 'network.error', message: e.message }
  return { code: 'unknown', message: 'Something went wrong.' }
}
