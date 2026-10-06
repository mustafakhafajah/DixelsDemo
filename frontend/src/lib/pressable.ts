import type { KeyboardEvent } from 'react'

/* Makes a clickable element that is not a <button> (a calendar cell, a booking block) work from the keyboard too:
 * Tab reaches it and Enter or Space does what a click does. The click handler stays on the element itself.
 * A key pressed on something inside it (e.g. a booking inside a day) is left to that inner element. */
export function pressable(onPress: () => void) {
  return {
    role: 'button' as const,
    tabIndex: 0,
    onKeyDown: (e: KeyboardEvent<HTMLElement>) => {
      if (e.target !== e.currentTarget || (e.key !== 'Enter' && e.key !== ' ')) return
      e.preventDefault()
      onPress()
    },
  }
}
