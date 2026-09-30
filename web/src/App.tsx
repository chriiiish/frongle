import { useAuth } from './auth/AuthContext'
import { Hello } from './Hello'

export default function App() {
  const { authenticated, token, login, logout } = useAuth()

  return (
    <main>
      <h1>Frongle</h1>
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
