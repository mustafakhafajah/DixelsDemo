import { useEffect, type ReactNode } from 'react'

interface SheetProps {
  title: ReactNode
  subtitle?: ReactNode
  onClose: () => void
  children: ReactNode
  footer?: ReactNode
  titleClassName?: string
}

function useEscape(onClose: () => void) {
  useEffect(() => {
    const onKey = (e: KeyboardEvent) => e.key === 'Escape' && onClose()
    window.addEventListener('keydown', onKey)
    return () => window.removeEventListener('keydown', onKey)
  }, [onClose])
}

function SheetHead({ title, subtitle, onClose, titleClassName }: Omit<SheetProps, 'children' | 'footer'>) {
  return (
    <div className="sheet-head">
      <div>
        <h2 className={titleClassName} style={{ fontSize: 15.5 }}>{title}</h2>
        {subtitle && <p style={{ fontSize: 12, color: 'var(--slate)', margin: '3px 0 0' }}>{subtitle}</p>}
      </div>
      <button type="button" className="iconbtn" onClick={onClose} aria-label="Close">×</button>
    </div>
  )
}

/* Backdrop closes only when the click lands on the overlay itself, as in the mock. */
export function Modal({ width, ...props }: SheetProps & { width?: number }) {
  useEscape(props.onClose)
  return (
    <div className="overlay" onClick={(e) => e.target === e.currentTarget && props.onClose()}>
      <div className="modal" role="dialog" aria-modal="true" style={width ? { width } : undefined}>
        <SheetHead {...props} />
        <div className="sheet-body">{props.children}</div>
        {props.footer && <div className="sheet-foot">{props.footer}</div>}
      </div>
    </div>
  )
}

export function Drawer(props: SheetProps) {
  useEscape(props.onClose)
  return (
    <div className="overlay" onClick={(e) => e.target === e.currentTarget && props.onClose()}>
      <div className="drawer" role="dialog" aria-modal="true">
        <SheetHead {...props} />
        <div style={{ padding: '16px 18px' }}>{props.children}</div>
      </div>
    </div>
  )
}
