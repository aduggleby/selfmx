import { StrictMode } from 'react'
import { createRoot } from 'react-dom/client'
import { BrowserRouter } from 'react-router-dom'
import './index.css'
import App from './App.tsx'

const baseUrl = import.meta.env.BASE_URL || '/'
const basename = baseUrl === '/' ? '/' : baseUrl.replace(/\/$/, '')

// If the UI bundle ever gets served at a non-/ui URL (proxy rewrite, stale cache, etc),
// force the browser onto the canonical /ui/... path before React Router mounts.
if (typeof window !== 'undefined' && basename !== '/') {
  const p = window.location.pathname
  if (p === '/' || p === basename) {
    window.location.replace(`${basename}/`)
  } else if (!p.startsWith(`${basename}/`)) {
    window.location.replace(`${basename}${p}`)
  }
}

createRoot(document.getElementById('root')!).render(
  <StrictMode>
    <BrowserRouter basename={basename}>
      <App />
    </BrowserRouter>
  </StrictMode>,
)
