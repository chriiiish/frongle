import { render, screen } from '@testing-library/react'
import userEvent from '@testing-library/user-event'
import { afterEach, expect, it, vi } from 'vitest'
import App from './App'
import { AuthContext, type Auth } from './auth/AuthContext'

const signedOut: Auth = { authenticated: false, token: undefined, login: vi.fn(), logout: vi.fn() }
const signedIn: Auth = { authenticated: true, token: 'jwt', login: vi.fn(), logout: vi.fn() }

function renderApp(auth: Auth) {
  render(
    <AuthContext.Provider value={auth}>
      <App />
    </AuthContext.Provider>,
  )
}

afterEach(() => vi.unstubAllGlobals())

it('shows the app name', () => {
  renderApp(signedOut)

  expect(screen.getByRole('heading', { name: 'Frongle' })).toBeInTheDocument()
})

it('shows the Frongle logo beside the app name', () => {
  renderApp(signedOut)

  expect(screen.getByRole('img', { name: 'Frongle logo' })).toHaveAttribute('src', '/logo.svg')
})

it('styles the sign-in button as a Bootstrap primary button', () => {
  renderApp(signedOut)

  expect(screen.getByRole('button', { name: 'Sign in' })).toHaveClass('btn', 'btn-primary')
})

it('asks a signed-out user to sign in', async () => {
  renderApp(signedOut)

  await userEvent.click(screen.getByRole('button', { name: 'Sign in' }))

  expect(signedOut.login).toHaveBeenCalled()
})

it('shows the greeting, tenant, and roles from the API for a signed-in user', async () => {
  const fetchMock = vi.fn().mockResolvedValue({
    ok: true,
    json: async () => ({ greeting: 'Hello from Frongle', tenantId: 'acme', roles: ['work-team'] }),
  })
  vi.stubGlobal('fetch', fetchMock)

  renderApp(signedIn)

  expect(await screen.findByText('Hello from Frongle')).toBeInTheDocument()
  expect(screen.getByText(/acme/)).toBeInTheDocument()
  expect(screen.getByText(/work-team/)).toBeInTheDocument()
  expect(fetchMock).toHaveBeenCalledWith('/api/hello', {
    headers: { Authorization: 'Bearer jwt' },
  })
})

it('shows an error when the API call fails', async () => {
  vi.stubGlobal('fetch', vi.fn().mockResolvedValue({ ok: false, status: 403 }))

  renderApp(signedIn)

  expect(await screen.findByRole('alert')).toHaveTextContent('403')
})
