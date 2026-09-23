const API_URL = import.meta.env.VITE_API_URL ?? 'https://localhost:44393'

export interface PingResponse {
  message: string
  timestampUtc: string
}

export async function pingBackend(): Promise<PingResponse> {
  const response = await fetch(`${API_URL}/api/demo/ping`)

  if (!response.ok) {
    throw new Error(`Backend responded with ${response.status}`)
  }

  return response.json()
}
