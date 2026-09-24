export function extractRoles(roleClaim: unknown): string[] {
  if (Array.isArray(roleClaim)) return roleClaim.map(String)
  if (typeof roleClaim === 'string') return [roleClaim]
  return []
}
