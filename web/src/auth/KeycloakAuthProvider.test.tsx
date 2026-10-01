import { render, waitFor } from '@testing-library/react'
import { afterEach, expect, it, vi } from 'vitest'
import { KeycloakAuthProvider } from './KeycloakAuthProvider'

const init = vi.hoisted(() => vi.fn().mockResolvedValue(false))

vi.mock('keycloak-js', () => ({
  default: class {
    init = init
  },
}))

afterEach(() => vi.unstubAllGlobals())

it('sends a signed-out user straight to the Keycloak login page', async () => {
  vi.stubGlobal(
    'fetch',
    vi.fn().mockResolvedValue({
      json: async () => ({
        keycloakUrl: 'https://kc.test/auth',
        realm: 'frongle',
        clientId: 'frongle-web',
      }),
    }),
  )

  render(<KeycloakAuthProvider>content</KeycloakAuthProvider>)

  await waitFor(() =>
    expect(init).toHaveBeenCalledWith({ onLoad: 'login-required', pkceMethod: 'S256' }),
  )
})
