import { createContext, useContext, type ReactNode } from 'react'
import { createPortal } from 'react-dom'

/* The top bar's right-hand area. Pages put their main buttons there ("Add a floor"), next to
 * "New booking", instead of repeating the page title in a heading of their own. */
export const PageActionsSlot = createContext<HTMLElement | null>(null)

export function PageActions({ children }: { children: ReactNode }) {
  const slot = useContext(PageActionsSlot)
  return slot ? createPortal(children, slot) : null
}
