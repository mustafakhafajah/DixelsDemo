import { create } from 'zustand'

export type ToastKind = 'ok' | 'warn' | 'err'

export interface Toast {
  id: number
  kind: ToastKind
  title: string
  message?: string
  code?: string
}

interface ToastState {
  toasts: Toast[]
  push: (kind: ToastKind, title: string, message?: string, code?: string) => void
  dismiss: (id: number) => void
}

let seq = 1

export const useToastStore = create<ToastState>((set, get) => ({
  toasts: [],
  push: (kind, title, message, code) => {
    const id = seq++
    set({ toasts: [...get().toasts, { id, kind, title, message, code }] })
    setTimeout(() => get().dismiss(id), kind === 'err' ? 9000 : 5500)
  },
  dismiss: (id) => set({ toasts: get().toasts.filter((t) => t.id !== id) }),
}))

export const toast = (kind: ToastKind, title: string, message?: string, code?: string) =>
  useToastStore.getState().push(kind, title, message, code)
