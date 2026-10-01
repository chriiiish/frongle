import { render, screen } from '@testing-library/react'
import userEvent from '@testing-library/user-event'
import { afterEach, expect, it, vi } from 'vitest'
import App from './App'
import { AuthContext, type Auth } from './auth/AuthContext'

const signedOut: Auth = { authenticated: false, token: undefined, logout: vi.fn() }
const signedIn: Auth = { authenticated: true, token: 'jwt', logout: vi.fn() }

function renderApp(auth: Auth) {
  render(
    <AuthContext.Provider value={auth}>
      <App />
    </AuthContext.Provider>,
  )
}

afterEach(() => vi.unstubAllGlobals())

it('shows the menu with Home and Logout to a signed-in user', () => {
  vi.stubGlobal('fetch', vi.fn().mockReturnValue(new Promise(() => {})))

  renderApp(signedIn)

  expect(screen.getByRole('link', { name: 'Home' })).toBeInTheDocument()
  expect(screen.getByRole('button', { name: 'Logout' })).toBeInTheDocument()
})

it('signs the user out from the menu', async () => {
  vi.stubGlobal('fetch', vi.fn().mockReturnValue(new Promise(() => {})))

  renderApp(signedIn)
  await userEvent.click(screen.getByRole('button', { name: 'Logout' }))

  expect(signedIn.logout).toHaveBeenCalled()
})

it('shows no menu and no sign-in button while the sign-in redirect happens', () => {
  renderApp(signedOut)

  expect(screen.getByRole('status')).toHaveTextContent('Signing you in')
  expect(screen.queryByRole('navigation')).not.toBeInTheDocument()
  expect(screen.queryByRole('button')).not.toBeInTheDocument()
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
