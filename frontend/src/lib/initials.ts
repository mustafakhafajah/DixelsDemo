/* Up to two letters for an avatar: first and last name ("Ada Lovelace" -> "AL"), or a single name's first two. */
export function initials(name: string): string {
  const parts = name.replace(/[^\p{L}\s.]/gu, ' ').split(/[\s.]+/).filter(Boolean)
  if (!parts.length) return '?'
  return (parts.length === 1 ? parts[0].slice(0, 2) : parts[0][0] + parts[parts.length - 1][0]).toUpperCase()
}
