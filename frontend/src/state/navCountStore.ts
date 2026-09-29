import { useEffect } from 'react'
import { create } from 'zustand'

/* The sidebar's counts follow a page's filters: a filtered list page publishes how many rows match,
 * and the sidebar shows that instead of the full count until the filter is cleared or the page is left. */
export type NavCountKey = 'floors' | 'spaces'

interface NavCountState {
  counts: Partial<Record<NavCountKey, number>>
  set: (key: NavCountKey, value: number | undefined) => void
}

export const useNavCountStore = create<NavCountState>((set) => ({
  counts: {},
  set: (key, value) => set((s) => ({ counts: { ...s.counts, [key]: value } })),
}))

/* undefined = no filter on, so the sidebar shows the full count. */
export function useFilteredNavCount(key: NavCountKey, value: number | undefined) {
  const setCount = useNavCountStore((s) => s.set)
  useEffect(() => {
    setCount(key, value)
    return () => setCount(key, undefined)
  }, [key, value, setCount])
}
