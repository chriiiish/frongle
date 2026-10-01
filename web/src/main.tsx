import { StrictMode } from 'react'
import { createRoot } from 'react-dom/client'
import { BrowserRouter } from 'react-router'
import 'bootstrap/dist/css/bootstrap.min.css'
import './index.css'
import App from './App.tsx'
import { KeycloakAuthProvider } from './auth/KeycloakAuthProvider.tsx'

createRoot(document.getElementById('root')!).render(
  <StrictMode>
    <KeycloakAuthProvider>
      <BrowserRouter>
        <App />
      </BrowserRouter>
    </KeycloakAuthProvider>
  </StrictMode>,
)
