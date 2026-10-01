import { StrictMode, type ReactNode } from 'react'
import { createRoot } from 'react-dom/client'
import { DirectionProvider } from '@radix-ui/react-direction'
import { useTranslation } from 'react-i18next'
import './index.css'
import './shadcn.css'
import { i18nReady } from './i18n'
import App from './App.tsx'

/* The Radix pickers (dropdowns, calendar popovers) follow the language's direction too. */
function Direction({ children }: { children: ReactNode }) {
  const { i18n } = useTranslation()
  return <DirectionProvider dir={i18n.dir()}>{children}</DirectionProvider>
}

/* Rendered once the language is loaded, so the first screen is never shown in the wrong one. */
i18nReady.then(() => {
  createRoot(document.getElementById('root')!).render(
    <StrictMode>
      <Direction>
        <App />
      </Direction>
    </StrictMode>,
  )
})
