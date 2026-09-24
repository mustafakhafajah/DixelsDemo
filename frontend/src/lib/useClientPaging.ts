import { useEffect, useState } from 'react'

/* Paging for a list the page already has in full (buildings, floors, space types): slices it locally.
 * The space registry pages on the server instead, because it can hold thousands of rows. */
export function useClientPaging<T>(items: T[], initialPageSize = 25) {
  const [page, setPage] = useState(1)
  const [pageSize, setPageSize] = useState(initialPageSize)
  const total = items.length
  const pageCount = Math.max(1, Math.ceil(total / pageSize))

  /* Stay on a real page when the list shrinks (e.g. after a delete on the last page). */
  useEffect(() => { if (page > pageCount) setPage(pageCount) }, [page, pageCount])

  return {
    rows: items.slice((page - 1) * pageSize, page * pageSize),
    page,
    pageSize,
    total,
    setPage,
    setPageSize: (s: number) => { setPageSize(s); setPage(1) },
  }
}
