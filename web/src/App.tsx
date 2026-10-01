import { useAuth } from './auth/AuthContext'
import { Hello } from './Hello'

export default function App() {
  const { authenticated, token, login, logout } = useAuth()

  return (
    <main className="container py-3">
      <header className="d-flex align-items-center gap-3 pb-3 mb-3 border-bottom border-2 border-primary">
        <img src="/logo.svg" alt="Frongle logo" width="40" height="40" />
        <h1 className="h3 m-0">Frongle</h1>
      </header>
      {authenticated ? (
        <>
          <Hello token={token} />
          <button className="btn btn-primary" onClick={logout}>
            Sign out
          </button>
        </>
      ) : (
        <button className="btn btn-primary" onClick={login}>
          Sign in
        </button>
      )}
    </main>
  )
}
