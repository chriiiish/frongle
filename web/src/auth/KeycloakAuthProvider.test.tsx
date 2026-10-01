import { act, render, screen, waitFor } from '@testing-library/react'
import userEvent from '@testing-library/user-event'
import { afterEach, expect, it, vi } from 'vitest'
import { useAuth } from './AuthContext'
import { KeycloakAuthProvider } from './KeycloakAuthProvider'

const init = vi.hoisted(() => vi.fn().mockResolvedValue(false))
const login = vi.hoisted(() => vi.fn())
const clients = vi.hoisted(() => [] as { token: string; onAuthRefreshSuccess?: () => void }[])

vi.mock('keycloak-js', () => ({
  default: class {
    token = 'old-token'
    onAuthRefreshSuccess?: () => void
    constructor() {
      clients.push(this)
    }
    tokenParsed = { given_name: 'Morgan', family_name: 'Manager', email: 'manager@acme.test' }
    init = init
    login = login
    updateToken = async () => {
      this.token = 'new-token'
      return true
    }
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

it('tells the app where the Keycloak account service is', async () => {
  init.mockResolvedValueOnce(true)
  vi.stubGlobal(
    'fetch',
    vi.fn().mockResolvedValue({
      json: async () => ({
        keycloakUrl: 'https://kc.test/auth/',
        realm: 'frongle',
        clientId: 'frongle-web',
      }),
    }),
  )
  function ShowAccountUrl() {
    return <p>{useAuth().accountUrl}</p>
  }

  render(
    <KeycloakAuthProvider>
      <ShowAccountUrl />
    </KeycloakAuthProvider>,
  )

  expect(await screen.findByText('https://kc.test/auth/realms/frongle/account')).toBeInTheDocument()
})

it('gives the app a fresh token when it asks for a refresh', async () => {
  init.mockResolvedValueOnce(true)
  vi.stubGlobal(
    'fetch',
    vi.fn().mockResolvedValue({
      json: async () => ({ keycloakUrl: 'https://kc.test', realm: 'frongle', clientId: 'x' }),
    }),
  )
  function RefreshButton() {
    const { token, refresh } = useAuth()
    return <button onClick={() => void refresh()}>{token}</button>
  }

  render(
    <KeycloakAuthProvider>
      <RefreshButton />
    </KeycloakAuthProvider>,
  )
  await userEvent.click(await screen.findByRole('button', { name: 'old-token' }))

  expect(await screen.findByRole('button', { name: 'new-token' })).toBeInTheDocument()
})

it('tells the app the name and email that the token carries', async () => {
  init.mockResolvedValueOnce(true)
  vi.stubGlobal(
    'fetch',
    vi.fn().mockResolvedValue({
      json: async () => ({ keycloakUrl: 'https://kc.test', realm: 'frongle', clientId: 'x' }),
    }),
  )
  function ShowProfile() {
    const { profile } = useAuth()
    return <p>{profile && `${profile.firstName} ${profile.lastName} ${profile.email}`}</p>
  }

  render(
    <KeycloakAuthProvider>
      <ShowProfile />
    </KeycloakAuthProvider>,
  )

  expect(await screen.findByText('Morgan Manager manager@acme.test')).toBeInTheDocument()
})

it('gives the app the new token when Keycloak renews it in the background', async () => {
  init.mockResolvedValueOnce(true)
  vi.stubGlobal(
    'fetch',
    vi.fn().mockResolvedValue({
      json: async () => ({ keycloakUrl: 'https://kc.test', realm: 'frongle', clientId: 'x' }),
    }),
  )
  function ShowToken() {
    return <p>{useAuth().token}</p>
  }
  render(
    <KeycloakAuthProvider>
      <ShowToken />
    </KeycloakAuthProvider>,
  )
  await screen.findByText('old-token')

  const client = clients[clients.length - 1]
  client.token = 'renewed-token'
  act(() => client.onAuthRefreshSuccess?.())

  expect(await screen.findByText('renewed-token')).toBeInTheDocument()
})

it('sends the user to the Keycloak page that changes the password', async () => {
  init.mockResolvedValueOnce(true)
  vi.stubGlobal(
    'fetch',
    vi.fn().mockResolvedValue({
      json: async () => ({ keycloakUrl: 'https://kc.test', realm: 'frongle', clientId: 'x' }),
    }),
  )
  function ChangePasswordButton() {
    return <button onClick={useAuth().changePassword}>Change it</button>
  }
  render(
    <KeycloakAuthProvider>
      <ChangePasswordButton />
    </KeycloakAuthProvider>,
  )

  await userEvent.click(await screen.findByRole('button', { name: 'Change it' }))

  expect(login).toHaveBeenCalledWith({ action: 'UPDATE_PASSWORD' })
})
