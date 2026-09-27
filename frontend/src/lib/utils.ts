import { clsx, type ClassValue } from 'clsx'
import { extendTailwindMerge } from 'tailwind-merge'

/* Tailwind runs with the tw: prefix (see shadcn.css), so merging must know it. */
const twMerge = extendTailwindMerge({ prefix: 'tw' })

export function cn(...inputs: ClassValue[]) {
  return twMerge(clsx(inputs))
}
