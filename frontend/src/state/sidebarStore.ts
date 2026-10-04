import { create } from 'zustand'

/* Whether the sidebar is collapsed to a strip of icons (laptops and up; phones and tablets use the slide-in
 * menu instead). The person's choice is remembered on this device. */
const STORAGE_KEY = 'dixels-sidebar-collapsed'

/* localStorage can throw (private windows, blocked site data), so every access is guarded. */
function stored(): boolean {
  try {
    return localStorage.getItem(STORAGE_KEY) === '1'
  } catch {
    return false
  }
}

interface SidebarState {
  collapsed: boolean
  toggle: () => void
}

export const useSidebarStore = create<SidebarState>((set, get) => ({
  collapsed: stored(),
  toggle: () => {
    const collapsed = !get().collapsed
    try {
      localStorage.setItem(STORAGE_KEY, collapsed ? '1' : '0')
    } catch {
      /* Not remembered, but the sidebar still collapses for this visit. */
    }
    set({ collapsed })
  },
}))
