import { createContext, useContext } from 'react'

export interface Auth {
  authenticated: boolean
  token: string | undefined
  accountUrl: string | undefined
  logout: () => void
  refresh: () => Promise<void>
}

export const AuthContext = createContext<Auth | undefined>(undefined)

export function useAuth(): Auth {
  const auth = useContext(AuthContext)
  if (!auth) throw new Error('useAuth must be used inside an AuthContext provider')
  return auth
}
