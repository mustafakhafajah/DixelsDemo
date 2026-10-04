import { useEffect, useRef, useState } from 'react'
import { useTranslation } from 'react-i18next'

export interface RowMenuItem {
  label: string
  onClick: () => void
}

export function RowMenu({ items }: { items: RowMenuItem[] }) {
  const { t } = useTranslation()
  const [open, setOpen] = useState(false)
  const ref = useRef<HTMLDivElement>(null)

  useEffect(() => {
    if (!open) return
    const onDoc = (e: MouseEvent) => {
      if (ref.current && !ref.current.contains(e.target as Node)) setOpen(false)
    }
    document.addEventListener('mousedown', onDoc)
    return () => document.removeEventListener('mousedown', onDoc)
  }, [open])

  /* Nothing the user may do on this row: no ⋯ button at all. */
  if (!items.length) return null

  return (
    <div className="row-menu" ref={ref}>
      <button type="button" className="iconbtn" onClick={() => setOpen((o) => !o)} aria-haspopup="true" aria-expanded={open} title={t('common.actions')} aria-label={t('common.actions')}>
        ⋯
      </button>
      {open && (
        <div className="row-menu-list">
          {items.map((it) => (
            <button key={it.label} type="button" className="row-menu-item" onClick={() => { setOpen(false); it.onClick() }}>
              {it.label}
            </button>
          ))}
        </div>
      )}
    </div>
  )
}
