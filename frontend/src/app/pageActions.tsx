import { useContext, type ReactNode } from 'react'
import { createPortal } from 'react-dom'
import { PageActionsSlot } from './pageActionsSlot'

export function PageActions({ children }: { children: ReactNode }) {
  const slot = useContext(PageActionsSlot)
  return slot ? createPortal(children, slot) : null
}
