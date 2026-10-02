import { createContext, useContext } from 'react'

/** The name and email that the user's token carries. */
export interface Profile {
  firstName: string
  lastName: string
  email: string
}

export interface Auth {
  authenticated: boolean
  token: string | undefined
  accountUrl: string | undefined
  profile: Profile | undefined
  /** The Keycloak roles of the user, such as maintenance-manager and work-team. */
  roles: string[]
  logout: () => void
  refresh: () => Promise<void>
  changePassword: () => void
}

export const AuthContext = createContext<Auth | undefined>(undefined)

export function useAuth(): Auth {
  const auth = useContext(AuthContext)
  if (!auth) throw new Error('useAuth must be used inside an AuthContext provider')
  return auth
}
