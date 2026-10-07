/// <reference types="vitest/config" />
import { fileURLToPath, URL } from 'node:url'
import tailwindcss from '@tailwindcss/vite'
import react from '@vitejs/plugin-react'
import { defineConfig, loadEnv } from 'vite'

// https://vite.dev/config/
export default defineConfig(({ mode }) => ({
  /* The address the app lives under, e.g. /portal/ on the shared server; the site root in development. */
  base: loadEnv(mode, process.cwd(), '').VITE_BASE || '/',
  plugins: [react(), tailwindcss()],
  resolve: {
    alias: { '@': fileURLToPath(new URL('./src', import.meta.url)) },
  },
  /* The viewer's time zone is pinned for tests (see vitest.setup.ts); TZ is also set before the workers start. */
  test: { env: { TZ: 'Asia/Amman' }, setupFiles: ['./vitest.setup.ts'] },
}))
