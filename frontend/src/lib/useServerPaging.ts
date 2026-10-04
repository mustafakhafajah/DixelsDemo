import { useEffect, useState } from 'react'

export interface ServerPaging {
  page: number
  pageSize: number
  setPage: (page: number) => void
  setPageSize: (size: number) => void
}

/* Page and page size for a list the server pages (buildings, floors, spaces, space types, users):
 * the page asks the server for exactly this page. */
export function useServerPaging(initialPageSize = 25): ServerPaging {
  const [page, setPage] = useState(1)
  const [pageSize, setPageSize] = useState(initialPageSize)
  return { page, pageSize, setPage, setPageSize: (s) => { setPageSize(s); setPage(1) } }
}

/* Stay on a real page when the server's total shrinks (a filter, or the last row of the last page was removed). */
export function useStayOnRealPage(paging: ServerPaging, total: number | undefined) {
  const { page, pageSize, setPage } = paging
  useEffect(() => {
    if (total === undefined) return
    const pageCount = Math.max(1, Math.ceil(total / pageSize))
    if (page > pageCount) setPage(pageCount)
  }, [total, page, pageSize, setPage])
}
