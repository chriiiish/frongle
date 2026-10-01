import Keycloak from 'keycloak-js'
import { useEffect, useMemo, useState, type ReactNode } from 'react'
import { AuthContext, type Auth, type Profile } from './AuthContext'

interface Config {
  keycloakUrl: string
  realm: string
  clientId: string
}

function profileFrom(client: Keycloak): Profile | undefined {
  const claims = client.tokenParsed
  if (!claims) return undefined
  return {
    firstName: claims.given_name ?? '',
    lastName: claims.family_name ?? '',
    email: claims.email ?? '',
  }
}

export function KeycloakAuthProvider({ children }: { children: ReactNode }) {
  const [keycloak, setKeycloak] = useState<Keycloak>()
  const [authenticated, setAuthenticated] = useState(false)
  const [token, setToken] = useState<string>()
  const [profile, setProfile] = useState<Profile>()
  const [accountUrl, setAccountUrl] = useState<string>()

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
      setAccountUrl(`${config.keycloakUrl.replace(/\/$/, '')}/realms/${config.realm}/account`)
      setKeycloak(client)
      setToken(client.token)
      setProfile(profileFrom(client))
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
      token,
      accountUrl,
      profile,
      logout: () => void keycloak?.logout(),
      // Forcing a refresh makes Keycloak issue a token that carries the latest name.
      refresh: async () => {
        await keycloak?.updateToken(-1)
        setToken(keycloak?.token)
        if (keycloak) setProfile(profileFrom(keycloak))
      },
    }),
    [keycloak, authenticated, token, accountUrl, profile],
  )

  return <AuthContext.Provider value={auth}>{children}</AuthContext.Provider>
}
