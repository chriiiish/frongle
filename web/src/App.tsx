import { Route, Routes } from 'react-router'
import { useAuth } from './auth/AuthContext'
import { MapPage } from './MapPage'
import { Menu } from './Menu'
import { useMe } from './useMe'
import { Welcome } from './Welcome'

export default function App() {
  const { authenticated, token, logout } = useAuth()
  const { me, error } = useMe(authenticated ? token : undefined)

  if (!authenticated) {
    return (
      <main className="container py-3">
        <p role="status">Signing you in…</p>
      </main>
    )
  }

  return (
    <>
      <Menu me={me} onLogout={logout} />
      <main>
        <Routes>
          <Route
            path="/"
            element={
              <div className="container-fluid py-3 px-md-4">
                <Welcome me={me} error={error} />
              </div>
            }
          />
          <Route path="/map" element={<MapPage />} />
        </Routes>
      </main>
    </>
  )
}
