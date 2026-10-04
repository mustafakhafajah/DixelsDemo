import { useEffect } from 'react'
import { PAGE_SIZES } from '../components/Pagination'
import { urlParam, useUrlState, type UrlParam } from './useUrlState'

export interface ServerPaging {
  page: number
  pageSize: number
  setPage: (page: number) => void
  setPageSize: (size: number) => void
}

/* Only the sizes the pager offers; anything else falls back. */
const sizeParam = (fallback: number): UrlParam<number> => ({
  name: 'size', fallback,
  parse: (r) => PAGE_SIZES.find((s) => String(s) === r[0]),
  format: (v) => [String(v)],
})

/* Page and page size for a list the server pages (buildings, floors, spaces, space types, users):
 * the page asks the server for exactly this page. Both live in the address (?page=2&size=50). */
export function useServerPaging(initialPageSize = 25): ServerPaging {
  const [{ page, size }, set] = useUrlState({ page: urlParam.int('page', 1, 1), size: sizeParam(initialPageSize) })
  return { page, pageSize: size, setPage: (p) => set({ page: p }), setPageSize: (s) => set({ size: s, page: 1 }) }
}

/* Stay on a real page when the server's total shrinks (a filter, a shared link past the last page,
 * or the last row of the last page was removed). */
export function useStayOnRealPage(paging: ServerPaging, total: number | undefined) {
  const { page, pageSize, setPage } = paging
  useEffect(() => {
    if (total === undefined) return
    const pageCount = Math.max(1, Math.ceil(total / pageSize))
    if (page > pageCount) setPage(pageCount)
  }, [total, page, pageSize, setPage])
}
