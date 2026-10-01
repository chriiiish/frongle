import { Route, Routes } from 'react-router'
import { useAuth } from './auth/AuthContext'
import { MapPage } from './MapPage'
import { Menu } from './Menu'
import { Welcome } from './Welcome'

export default function App() {
  const { authenticated, token, logout } = useAuth()

  if (!authenticated) {
    return (
      <main className="container py-3">
        <p role="status">Signing you in…</p>
      </main>
    )
  }

  return (
    <>
      <Menu onLogout={logout} />
      <main>
        <Routes>
          <Route
            path="/"
            element={
              <div className="container py-3">
                <Welcome token={token} />
              </div>
            }
          />
          <Route path="/map" element={<MapPage />} />
        </Routes>
      </main>
    </>
  )
}
