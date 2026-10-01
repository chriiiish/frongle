import { render, screen } from '@testing-library/react'
import userEvent from '@testing-library/user-event'
import { MemoryRouter } from 'react-router'
import { afterEach, expect, it, vi } from 'vitest'
import App from './App'
import { AuthContext, type Auth } from './auth/AuthContext'

vi.mock('./MapPage', () => ({ MapPage: () => <section aria-label="Map" /> }))

const signedOut: Auth = { authenticated: false, token: undefined, logout: vi.fn() }
const signedIn: Auth = { authenticated: true, token: 'jwt', logout: vi.fn() }

function renderApp(auth: Auth, path = '/') {
  render(
    <AuthContext.Provider value={auth}>
      <MemoryRouter initialEntries={[path]}>
        <App />
      </MemoryRouter>
    </AuthContext.Provider>,
  )
}

afterEach(() => vi.unstubAllGlobals())

it('shows the menu with Home, Map, and the account dropdown to a signed-in user', () => {
  vi.stubGlobal('fetch', vi.fn().mockReturnValue(new Promise(() => {})))

  renderApp(signedIn)

  expect(screen.getByRole('link', { name: 'Home' })).toBeInTheDocument()
  expect(screen.getByRole('link', { name: 'Map' })).toBeInTheDocument()
  expect(screen.getByRole('button', { name: 'Account' })).toBeInTheDocument()
})

it('signs the user out from the menu', async () => {
  vi.stubGlobal('fetch', vi.fn().mockReturnValue(new Promise(() => {})))

  renderApp(signedIn)
  await userEvent.click(screen.getByRole('button', { name: 'Logout' }))

  expect(signedIn.logout).toHaveBeenCalled()
})

it('shows no menu and no map while the sign-in redirect happens', () => {
  renderApp(signedOut, '/map')

  expect(screen.getByRole('status')).toHaveTextContent('Signing you in')
  expect(screen.queryByRole('navigation')).not.toBeInTheDocument()
  expect(screen.queryByRole('region', { name: 'Map' })).not.toBeInTheDocument()
  expect(screen.queryByRole('button')).not.toBeInTheDocument()
})

it('welcomes a signed-in user by the name that the API returns', async () => {
  const fetchMock = vi.fn().mockResolvedValue({
    ok: true,
    json: async () => ({ name: 'Morgan Manager', tenant: 'acme' }),
  })
  vi.stubGlobal('fetch', fetchMock)

  renderApp(signedIn)

  expect(
    await screen.findByRole('heading', { name: 'Welcome, Morgan Manager' }),
  ).toBeInTheDocument()
  expect(fetchMock).toHaveBeenCalledWith('/api/me', {
    headers: { Authorization: 'Bearer jwt' },
  })
})

it('gives the welcome page the full screen width and a large heading on desktop', async () => {
  vi.stubGlobal(
    'fetch',
    vi.fn().mockResolvedValue({
      ok: true,
      json: async () => ({ name: 'Morgan Manager', tenant: 'acme' }),
    }),
  )

  renderApp(signedIn)

  const heading = await screen.findByRole('heading', { name: 'Welcome, Morgan Manager' })
  expect(heading.closest('.container-fluid')).not.toBeNull()
  expect(heading).toHaveClass('display-5')
})

it('shows the user name in the menu bar', async () => {
  vi.stubGlobal(
    'fetch',
    vi.fn().mockResolvedValue({
      ok: true,
      json: async () => ({ name: 'Morgan Manager', tenant: 'acme' }),
    }),
  )

  renderApp(signedIn, '/map')

  expect(await screen.findByRole('button', { name: 'Morgan Manager' })).toBeInTheDocument()
})

it('does not show the map on the welcome page', () => {
  vi.stubGlobal('fetch', vi.fn().mockReturnValue(new Promise(() => {})))

  renderApp(signedIn, '/')

  expect(screen.queryByRole('region', { name: 'Map' })).not.toBeInTheDocument()
})

it('shows an error when the API call fails', async () => {
  vi.stubGlobal('fetch', vi.fn().mockResolvedValue({ ok: false, status: 403 }))

  renderApp(signedIn)

  expect(await screen.findByRole('alert')).toHaveTextContent('403')
})

it('shows an error when the API cannot be reached', async () => {
  vi.stubGlobal('fetch', vi.fn().mockRejectedValue(new TypeError('Failed to fetch')))

  renderApp(signedIn)

  expect(await screen.findByRole('alert')).toHaveTextContent('Failed to fetch')
})

it('shows the map on the map page to a signed-in user', () => {
  vi.stubGlobal('fetch', vi.fn().mockReturnValue(new Promise(() => {})))

  renderApp(signedIn, '/map')

  expect(screen.getByRole('region', { name: 'Map' })).toBeInTheDocument()
})
