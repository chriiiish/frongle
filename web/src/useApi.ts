import { useMemo } from 'react'
import { createApi } from './api'
import { useAuth } from './auth/AuthContext'

/** The Frongle API, signed with the current token. It changes only when Keycloak renews the token. */
export function useApi() {
  const { token } = useAuth()
  return useMemo(() => createApi(token), [token])
}
