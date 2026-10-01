import Keycloak from 'keycloak-js'
import { useEffect, useMemo, useState, type ReactNode } from 'react'
import { AuthContext, type Auth } from './AuthContext'

interface Config {
  keycloakUrl: string
  realm: string
  clientId: string
}

export function KeycloakAuthProvider({ children }: { children: ReactNode }) {
  const [keycloak, setKeycloak] = useState<Keycloak>()
  const [authenticated, setAuthenticated] = useState(false)

  useEffect(() => {
    let cancelled = false
    async function start() {
      const config: Config = await (await fetch('/config.json')).json()
      const client = new Keycloak({
        url: config.keycloakUrl,
        realm: config.realm,
        clientId: config.clientId,
      })
      client.onTokenExpired = () => void client.updateToken(30)
      const isSignedIn = await client.init({ onLoad: 'login-required', pkceMethod: 'S256' })
      if (cancelled) return
      setKeycloak(client)
      setAuthenticated(isSignedIn)
    }
    void start()
    return () => {
      cancelled = true
    }
  }, [])

  const auth = useMemo<Auth>(
    () => ({
      authenticated,
      token: keycloak?.token,
      logout: () => void keycloak?.logout(),
    }),
    [keycloak, authenticated],
  )

  return <AuthContext.Provider value={auth}>{children}</AuthContext.Provider>
}
