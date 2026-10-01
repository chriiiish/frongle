import { render, screen } from '@testing-library/react'
import userEvent from '@testing-library/user-event'
import { afterEach, expect, it, vi } from 'vitest'
import { AuthContext, type Auth } from './auth/AuthContext'
import { ProfilePage } from './ProfilePage'

const ACCOUNT_URL = 'https://kc.test/realms/frongle/account'
const auth: Auth = {
  authenticated: true,
  token: 'jwt',
  accountUrl: ACCOUNT_URL,
  profile: undefined,
  logout: vi.fn(),
  refresh: vi.fn(),
  changePassword: vi.fn(),
}
// Keycloak also returns fields that the user must not change, such as the tenant.
const morgan = {
  username: 'manager@acme.test',
  firstName: 'Morgan',
  lastName: 'Manager',
  email: 'manager@acme.test',
  attributes: { tenant_id: ['acme'] },
}

// Keycloak answers a read with the details and a save with "no content".
function stubKeycloak(saveResponse: object = { ok: true, status: 204 }) {
  const fetchMock = vi.fn(async (_url: string, init: RequestInit = {}) =>
    init.method === 'POST' ? saveResponse : { ok: true, json: async () => morgan },
  )
  vi.stubGlobal('fetch', fetchMock)
  return fetchMock
}

function renderProfilePage(signedInAs: Auth = auth) {
  render(
    <AuthContext.Provider value={signedInAs}>
      <ProfilePage />
    </AuthContext.Provider>,
  )
}

afterEach(() => vi.unstubAllGlobals())

it('shows the current first name, last name, and email of the user', async () => {
  const fetchMock = stubKeycloak()

  renderProfilePage()

  expect(await screen.findByLabelText('First name')).toHaveValue('Morgan')
  expect(screen.getByLabelText('Last name')).toHaveValue('Manager')
  expect(screen.getByLabelText('Email')).toHaveValue('manager@acme.test')
  expect(fetchMock).toHaveBeenCalledWith(ACCOUNT_URL, {
    headers: { Authorization: 'Bearer jwt', Accept: 'application/json' },
  })
})

it('saves the changed name and email to Keycloak and refreshes the token', async () => {
  const fetchMock = stubKeycloak()
  renderProfilePage()
  const firstName = await screen.findByLabelText('First name')

  await userEvent.clear(firstName)
  await userEvent.type(firstName, 'Morgana')
  await userEvent.clear(screen.getByLabelText('Email'))
  await userEvent.type(screen.getByLabelText('Email'), 'morgana@acme.test')
  await userEvent.click(screen.getByRole('button', { name: 'Save details' }))

  expect(await screen.findByRole('status')).toHaveTextContent('Your details are saved.')
  expect(fetchMock).toHaveBeenCalledWith(ACCOUNT_URL, {
    method: 'POST',
    headers: { Authorization: 'Bearer jwt', 'Content-Type': 'application/json' },
    body: JSON.stringify({
      firstName: 'Morgana',
      lastName: 'Manager',
      email: 'morgana@acme.test',
    }),
  })
  expect(auth.refresh).toHaveBeenCalled()
})

it('shows the message from Keycloak when it rejects the details', async () => {
  stubKeycloak({
    ok: false,
    status: 400,
    json: async () => ({ errors: [{ field: 'email', errorMessage: 'Invalid email address.' }] }),
  })
  renderProfilePage()
  await screen.findByLabelText('Email')
  vi.mocked(auth.refresh).mockClear()

  await userEvent.click(screen.getByRole('button', { name: 'Save details' }))

  expect(await screen.findByRole('alert')).toHaveTextContent('Invalid email address.')
  expect(screen.queryByText('Your details are saved.')).not.toBeInTheDocument()
  expect(auth.refresh).not.toHaveBeenCalled()
})

it('sends the user to Keycloak to change the password', async () => {
  stubKeycloak()
  renderProfilePage()

  await userEvent.click(await screen.findByRole('button', { name: 'Change password' }))

  expect(auth.changePassword).toHaveBeenCalledOnce()
})

it('does not ask for passwords on the page', async () => {
  stubKeycloak()
  renderProfilePage()
  await screen.findByLabelText('Email')

  expect(screen.queryByLabelText(/password/i)).not.toBeInTheDocument()
})

it('stacks the two forms on a phone and puts them side by side on desktop', async () => {
  stubKeycloak()
  renderProfilePage()

  const details = await screen.findByRole('form', { name: 'Details' })
  const password = screen.getByRole('button', { name: 'Change password' })

  expect(details.closest('section')).toHaveClass('col-lg-6')
  expect(password.closest('section')).toHaveClass('col-lg-6')
})

it('starts with the details from the token and says so when Keycloak does not answer', async () => {
  vi.stubGlobal('fetch', vi.fn().mockRejectedValue(new TypeError('Failed to fetch')))

  renderProfilePage({
    ...auth,
    profile: { firstName: 'Tess', lastName: 'Token', email: 'tess@acme.test' },
  })

  expect(screen.getByLabelText('First name')).toHaveValue('Tess')
  expect(screen.getByLabelText('Last name')).toHaveValue('Token')
  expect(screen.getByLabelText('Email')).toHaveValue('tess@acme.test')
  expect(await screen.findByRole('alert')).toHaveTextContent('Failed to fetch')
})

it('keeps the token details and shows an error when Keycloak refuses to share the details', async () => {
  vi.stubGlobal('fetch', vi.fn().mockResolvedValue({ ok: false, status: 403 }))

  renderProfilePage({
    ...auth,
    profile: { firstName: 'Tess', lastName: 'Token', email: 'tess@acme.test' },
  })

  expect(await screen.findByRole('alert')).toHaveTextContent('Keycloak returned status 403.')
  expect(screen.getByLabelText('First name')).toHaveValue('Tess')
})
