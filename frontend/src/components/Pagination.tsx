export const PAGE_SIZES = [10, 25, 50, 100] as const

/* Page numbers to show: always the first and last, plus the current page and its neighbours. */
export function pageWindow(page: number, pageCount: number): (number | 'gap')[] {
  const pages = new Set([1, pageCount, page - 1, page, page + 1].filter((p) => p >= 1 && p <= pageCount))
  const sorted = [...pages].sort((a, b) => a - b)
  const out: (number | 'gap')[] = []
  sorted.forEach((p, i) => {
    if (i > 0 && p - sorted[i - 1] > 1) out.push('gap')
    out.push(p)
  })
  return out
}

export function Pagination({ page, pageSize, total, noun, onPage, onPageSize }: {
  page: number
  pageSize: number
  total: number
  /* Plural label for the count, e.g. "spaces". */
  noun: string
  onPage: (page: number) => void
  onPageSize: (size: number) => void
}) {
  const pageCount = Math.max(1, Math.ceil(total / pageSize))
  const first = total === 0 ? 0 : (page - 1) * pageSize + 1
  const last = Math.min(total, page * pageSize)
  const atStart = page <= 1
  const atEnd = page >= pageCount

  return (
    <div className="pager">
      <span className="pager-summary">
        {total === 0 ? `No ${noun}` : `Showing ${first.toLocaleString()}–${last.toLocaleString()} of ${total.toLocaleString()} ${noun}`}
      </span>
      <div className="pager-pages" role="navigation" aria-label="Pages">
        <button type="button" className="iconbtn" disabled={atStart} onClick={() => onPage(1)} aria-label="First page" title="First page">«</button>
        <button type="button" className="iconbtn" disabled={atStart} onClick={() => onPage(page - 1)} aria-label="Previous page" title="Previous page">‹</button>
        {pageWindow(page, pageCount).map((p, i) => p === 'gap'
          ? <span key={`gap-${i}`} className="pager-gap">…</span>
          : (
            <button key={p} type="button" className={`pager-num${p === page ? ' active' : ''}`}
              aria-current={p === page ? 'page' : undefined} onClick={() => onPage(p)}>{p}</button>
          ))}
        <button type="button" className="iconbtn" disabled={atEnd} onClick={() => onPage(page + 1)} aria-label="Next page" title="Next page">›</button>
        <button type="button" className="iconbtn" disabled={atEnd} onClick={() => onPage(pageCount)} aria-label="Last page" title="Last page">»</button>
      </div>
      <label className="pager-size">
        Rows
        <select className="inp" value={pageSize} onChange={(e) => onPageSize(Number(e.target.value))}>
          {PAGE_SIZES.map((s) => <option key={s} value={s}>{s}</option>)}
        </select>
      </label>
    </div>
  )
}
