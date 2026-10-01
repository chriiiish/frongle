import { useAuth } from './auth/AuthContext'
import { Hello } from './Hello'

export default function App() {
  const { authenticated, token, login, logout } = useAuth()

  return (
    <main>
      <header>
        <img src="/logo.svg" alt="Frongle logo" />
        <h1>Frongle</h1>
      </header>
      {authenticated ? (
        <>
          <Hello token={token} />
          <button onClick={logout}>Sign out</button>
        </>
      ) : (
        <button onClick={login}>Sign in</button>
      )}
    </main>
  )
}
