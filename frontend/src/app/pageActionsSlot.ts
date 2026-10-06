import { createContext } from 'react'

/* The top bar's right-hand area. Pages put their main buttons there ("Add a floor"), next to
 * "New booking", instead of repeating the page title in a heading of their own. */
export const PageActionsSlot = createContext<HTMLElement | null>(null)
