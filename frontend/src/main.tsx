import { StrictMode } from 'react'
import { createRoot } from 'react-dom/client'
/* The fonts ship with the app (Latin and Arabic letters only), so it also works without internet access. */
import './fonts.css'
import './index.css'
import './shadcn.css'
import { i18nReady } from './i18n'
import App from './App.tsx'

/* Rendered once the language is loaded, so the first screen is never shown in the wrong one. */
i18nReady.then(() => {
  createRoot(document.getElementById('root')!).render(
    <StrictMode>
      <App />
    </StrictMode>,
  )
})
