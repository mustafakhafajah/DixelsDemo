import { useEffect, useRef, useState } from 'react'
import { useSearchParams } from 'react-router-dom'
import { useDebouncedValue } from './useDebouncedValue'

/* Page filters live in the address (?building=…&page=2), so a copied link opens exactly the same view.
 * Each value has a param name, the value it falls back to, and how it reads from / writes to the address.
 * A value at its fallback is left out, so an untouched page keeps a clean address. */
export interface UrlParam<T> {
  name: string
  fallback: T
  /* undefined = not usable (missing, malformed or out of range): the fallback is used instead. */
  parse: (raw: string[]) => T | undefined
  format: (value: T) => string[]
}

const DAY = /^\d{4}-\d{2}-\d{2}$/

export const urlParam = {
  text: (name: string, fallback = ''): UrlParam<string> =>
    ({ name, fallback, parse: (r) => r[0], format: (v) => (v ? [v] : []) }),
  int: (name: string, fallback: number, min = 0, max = Number.MAX_SAFE_INTEGER): UrlParam<number> => ({
    name, fallback,
    parse: (r) => { const n = Number(r[0]); return r[0] && Number.isInteger(n) && n >= min && n <= max ? n : undefined },
    format: (v) => [String(v)],
  }),
  /* A repeated key: ?type=a&type=b. */
  list: (name: string): UrlParam<string[]> =>
    ({ name, fallback: [], parse: (r) => r.filter(Boolean), format: (v) => v }),
  flag: (name: string): UrlParam<boolean> =>
    ({ name, fallback: false, parse: (r) => (r[0] === '1' ? true : r[0] === '0' ? false : undefined), format: (v) => [v ? '1' : '0'] }),
  oneOf: <T extends string>(name: string, values: readonly T[], fallback: T): UrlParam<T> =>
    ({ name, fallback, parse: (r) => values.find((v) => v === r[0]), format: (v) => [v] }),
  /* A day key, "2026-10-04". */
  day: (name: string, fallback: string): UrlParam<string> => ({
    name, fallback,
    parse: (r) => (r[0] && DAY.test(r[0]) && !Number.isNaN(Date.parse(r[0])) ? r[0] : undefined),
    format: (v) => (v ? [v] : []),
  }),
}

/* Any UrlParam, whatever its value type. */
interface AnyUrlParam { name: string; fallback: unknown; parse(raw: string[]): unknown; format(value: never): string[] }
type Spec = Record<string, AnyUrlParam>
export type UrlValues<S extends Spec> = { [K in keyof S]: S[K] extends UrlParam<infer T> ? T : never }

const same = (a: string[], b: string[]) => a.length === b.length && a.every((x, i) => x === b[i])

/* The spec's values read from the address, and a setter that writes only the given ones back (replacing the
 * history entry, so typing and paging don't pile up Back steps). Several setters may run in one event:
 * each starts from the address as it is right now. */
export function useUrlState<S extends Spec>(spec: S): [UrlValues<S>, (patch: Partial<UrlValues<S>>) => void] {
  const [params, setParams] = useSearchParams()
  const values = Object.fromEntries(Object.entries(spec).map(([key, p]) => {
    const param = p as UrlParam<unknown>
    return [key, param.parse(params.getAll(param.name)) ?? param.fallback]
  })) as UrlValues<S>

  const set = (patch: Partial<UrlValues<S>>) => {
    const next = new URLSearchParams(window.location.search)
    Object.entries(patch).forEach(([key, value]) => {
      const param = spec[key] as UrlParam<unknown> | undefined
      if (!param) return
      next.delete(param.name)
      const written = param.format(value)
      if (!same(written, param.format(param.fallback))) written.forEach((v) => next.append(param.name, v))
    })
    if (next.toString() !== window.location.search.replace(/^\?/, '')) setParams(next, { replace: true })
  }

  return [values, set]
}

/* A typed search box kept in the address: the box answers every key, the address follows once typing pauses.
 * When the address changes from elsewhere (Clear filters, Back) the box shows that instead. */
export function useUrlSearchBox(urlValue: string, write: (value: string) => void): [string, (value: string) => void] {
  const [text, setText] = useState(urlValue)
  const [shown, setShown] = useState(urlValue)
  const typed = useDebouncedValue(text).trim()
  if (urlValue !== shown) {
    setShown(urlValue)
    if (urlValue !== typed) setText(urlValue)
  }
  /* Only a new pause in typing writes; an address change from elsewhere is never written back. */
  const lastTyped = useRef(typed)
  useEffect(() => {
    if (typed === lastTyped.current) return
    lastTyped.current = typed
    if (typed !== urlValue) write(typed)
  }, [typed, urlValue, write])
  return [text, setText]
}

/* A shared link can carry an id this viewer can't use (deleted, or not theirs to pick).
 * Once the choices are known, such a value is dropped so the page falls back to its default. */
export function useDropUnknown(value: string, known: readonly string[] | undefined, drop: () => void) {
  const unknown = !!value && !!known && !known.includes(value)
  useEffect(() => {
    if (unknown) drop()
  }, [unknown, drop])
}
