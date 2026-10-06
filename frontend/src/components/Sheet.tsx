import { useEffect, useId, useRef, useState, type ReactNode, type RefObject } from 'react'
import { useTranslation } from 'react-i18next'

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
    /* An open dropdown or calendar handles Escape first (and marks it handled); only then close the sheet. */
    const onKey = (e: KeyboardEvent) => e.key === 'Escape' && !e.defaultPrevented && onClose()
    window.addEventListener('keydown', onKey)
    return () => window.removeEventListener('keydown', onKey)
  }, [onClose])
}

const FOCUSABLE = 'a[href], button:not([disabled]), input:not([disabled]):not([type="hidden"]), select:not([disabled]), textarea:not([disabled]), [tabindex]:not([tabindex="-1"])'
/* Dropdowns are buttons (role="combobox") and date fields are buttons that open a calendar. */
const FIELDS = 'input:not([disabled]):not([type="hidden"]), select:not([disabled]), textarea:not([disabled]), [role="combobox"]:not([disabled]), button[aria-haspopup]:not([disabled])'

/* Reachable with Tab and on screen. Leaves out the hidden native <select> a dropdown keeps for forms: it is
 * aria-hidden with tabindex -1, and focusing it lost the focus to the page as soon as the dropdown redrew it. */
const usable = (el: HTMLElement) => el.getClientRects().length > 0 && el.tabIndex >= 0 && !el.closest('[aria-hidden="true"]')

/* Keyboard focus belongs to the open sheet: on opening it moves to the first field (or the sheet itself, so its
 * title is read out), Tab and Shift+Tab go round inside it, and on closing it goes back to what opened it.
 * Pickers drawn outside the page (dropdowns, calendars) keep their own focus while they are open. */
function useDialogFocus(ref: RefObject<HTMLDivElement | null>) {
  /* Read while rendering, before a field's autoFocus takes the focus away from the button that opened the sheet. */
  const [opener] = useState(() => document.activeElement as HTMLElement | null)
  useEffect(() => {
    const dialog = ref.current
    if (!dialog) return
    const page = document.getElementById('root')
    /* Opened from something that is gone now (e.g. another sheet): return to whatever has the focus instead. */
    let back = opener?.isConnected ? opener : document.activeElement as HTMLElement | null
    if (!dialog.contains(document.activeElement)) {
      const field = [...dialog.querySelectorAll<HTMLElement>(FIELDS)].find(usable)
      ;(field ?? dialog).focus()
    }
    const onKey = (e: KeyboardEvent) => {
      if (e.key !== 'Tab') return
      const items = [...dialog.querySelectorAll<HTMLElement>(FOCUSABLE)].filter(usable)
      if (!items.length) { e.preventDefault(); return }
      const first = items[0]
      const last = items[items.length - 1]
      const active = document.activeElement
      if (e.shiftKey && (active === first || active === dialog)) { e.preventDefault(); last.focus() }
      else if (!e.shiftKey && active === last) { e.preventDefault(); first.focus() }
    }
    /* Something on the page behind took the focus (e.g. a closing menu handing it back to its ⋯ button): that is
     * where to return on closing, and the focus comes back into the sheet. */
    const onFocusIn = (e: FocusEvent) => {
      const target = e.target as HTMLElement
      if (dialog.contains(target) || !page?.contains(target)) return
      back = target
      dialog.focus()
    }
    dialog.addEventListener('keydown', onKey)
    document.addEventListener('focusin', onFocusIn)
    return () => {
      dialog.removeEventListener('keydown', onKey)
      document.removeEventListener('focusin', onFocusIn)
      if (back?.isConnected) back.focus()
    }
  }, [ref, opener])
}

function SheetHead({ title, subtitle, onClose, titleClassName, titleId }: Omit<SheetProps, 'children' | 'footer'> & { titleId: string }) {
  const { t } = useTranslation()
  return (
    <div className="sheet-head">
      <div>
        <h2 id={titleId} className={titleClassName} style={{ fontSize: 15.5 }}>{title}</h2>
        {subtitle && <p style={{ fontSize: 12, color: 'var(--slate)', margin: '3px 0 0' }}>{subtitle}</p>}
      </div>
      <button type="button" className="iconbtn" onClick={onClose} aria-label={t('common.close')}>×</button>
    </div>
  )
}

/* Backdrop closes only when the click lands on the overlay itself, as in the mock. */
export function Modal({ width, ...props }: SheetProps & { width?: number }) {
  const ref = useRef<HTMLDivElement>(null)
  const titleId = useId()
  useEscape(props.onClose)
  useDialogFocus(ref)
  return (
    <div className="overlay" onClick={(e) => e.target === e.currentTarget && props.onClose()}>
      <div ref={ref} className="modal" role="dialog" aria-modal="true" aria-labelledby={titleId} tabIndex={-1} style={width ? { width } : undefined}>
        <SheetHead {...props} titleId={titleId} />
        <div className="sheet-body">{props.children}</div>
        {props.footer && <div className="sheet-foot">{props.footer}</div>}
      </div>
    </div>
  )
}

export function Drawer(props: SheetProps) {
  const ref = useRef<HTMLDivElement>(null)
  const titleId = useId()
  useEscape(props.onClose)
  useDialogFocus(ref)
  return (
    <div className="overlay" onClick={(e) => e.target === e.currentTarget && props.onClose()}>
      <div ref={ref} className="drawer" role="dialog" aria-modal="true" aria-labelledby={titleId} tabIndex={-1}>
        <SheetHead {...props} titleId={titleId} />
        <div style={{ padding: '16px 18px' }}>{props.children}</div>
      </div>
    </div>
  )
}
