import { useTranslation } from 'react-i18next'
import { intlLocale } from '../i18n/languages'
import { Dropdown } from './pickers'

export const PAGE_SIZES = [10, 25, 50, 100] as const

/* What a list counts, as a key under "count" in the locale files (e.g. count.space_one / _other). */
export type PagerNoun = 'building' | 'floor' | 'space' | 'spaceType' | 'user'

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
  noun: PagerNoun
  onPage: (page: number) => void
  onPageSize: (size: number) => void
}) {
  const { t } = useTranslation()
  const pageCount = Math.max(1, Math.ceil(total / pageSize))
  const first = total === 0 ? 0 : (page - 1) * pageSize + 1
  const last = Math.min(total, page * pageSize)
  const atStart = page <= 1
  const atEnd = page >= pageCount
  const num = (n: number) => n.toLocaleString(intlLocale())
  /* The count comes with its noun in the right plural form, e.g. "120 spaces" / "No spaces". */
  const counted = t(`count.${noun}`, { count: total })

  return (
    <div className="pager">
      <span className="pager-summary">
        {total === 0 ? counted : t('pagination.showing', { first, last, total: counted })}
      </span>
      <div className="pager-pages" role="navigation" aria-label={t('pagination.pages')}>
        {/* « ‹ › » are mirrored characters: in right-to-left text the browser turns them round by itself. */}
        <button type="button" className="iconbtn" disabled={atStart} onClick={() => onPage(1)} aria-label={t('pagination.first')} title={t('pagination.first')}>«</button>
        <button type="button" className="iconbtn" disabled={atStart} onClick={() => onPage(page - 1)} aria-label={t('pagination.previous')} title={t('pagination.previous')}>‹</button>
        {pageWindow(page, pageCount).map((p, i) => p === 'gap'
          ? <span key={`gap-${i}`} className="pager-gap">…</span>
          : (
            <button key={p} type="button" className={`pager-num${p === page ? ' active' : ''}`}
              aria-current={p === page ? 'page' : undefined} onClick={() => onPage(p)}>{num(p)}</button>
          ))}
        <button type="button" className="iconbtn" disabled={atEnd} onClick={() => onPage(page + 1)} aria-label={t('pagination.next')} title={t('pagination.next')}>›</button>
        <button type="button" className="iconbtn" disabled={atEnd} onClick={() => onPage(pageCount)} aria-label={t('pagination.last')} title={t('pagination.last')}>»</button>
      </div>
      <div className="pager-size">
        {t('pagination.rows')}
        <Dropdown size="sm" className="tw:w-[74px]" aria-label={t('pagination.rowsPerPage')} value={String(pageSize)}
          options={PAGE_SIZES.map((s) => ({ value: String(s), label: num(s) }))}
          onChange={(v) => onPageSize(Number(v))} />
      </div>
    </div>
  )
}
