/// <reference types="vite/client" />

/* The VITE_ settings, documented in frontend/.env.example. */
interface ImportMetaEnv {
  readonly VITE_API_URL?: string
  readonly VITE_BASE?: string
}

interface ImportMeta {
  readonly env: ImportMetaEnv
}
