import { useAuth } from './auth/AuthContext'
import { Hello } from './Hello'
import { Menu } from './Menu'

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
      <main className="container py-3">
        <Hello token={token} />
      </main>
    </>
  )
}
