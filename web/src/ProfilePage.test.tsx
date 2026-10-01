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
  logout: vi.fn(),
  refresh: vi.fn(),
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

function renderProfilePage() {
  render(
    <AuthContext.Provider value={auth}>
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

async function fillPasswordForm(confirmation: string) {
  await userEvent.type(await screen.findByLabelText('Current password'), 'password')
  await userEvent.type(screen.getByLabelText('New password'), 'a-better-secret')
  await userEvent.type(screen.getByLabelText('Confirm new password'), confirmation)
  await userEvent.click(screen.getByRole('button', { name: 'Change password' }))
}

it('changes the password in Keycloak and clears the password fields', async () => {
  const fetchMock = stubKeycloak()
  renderProfilePage()

  await fillPasswordForm('a-better-secret')

  expect(await screen.findByRole('status')).toHaveTextContent('Your password is changed.')
  expect(fetchMock).toHaveBeenCalledWith(`${ACCOUNT_URL}/credentials/password`, {
    method: 'POST',
    headers: { Authorization: 'Bearer jwt', 'Content-Type': 'application/json' },
    body: JSON.stringify({
      currentPassword: 'password',
      newPassword: 'a-better-secret',
      confirmation: 'a-better-secret',
    }),
  })
  expect(screen.getByLabelText('Current password')).toHaveValue('')
  expect(screen.getByLabelText('New password')).toHaveValue('')
  expect(screen.getByLabelText('Confirm new password')).toHaveValue('')
})

it('shows the message from Keycloak when it refuses the new password', async () => {
  stubKeycloak({
    ok: false,
    status: 400,
    json: async () => ({ errors: [{ errorMessage: 'Invalid password: minimum length 8.' }] }),
  })
  renderProfilePage()

  await fillPasswordForm('a-better-secret')

  expect(await screen.findByRole('alert')).toHaveTextContent('Invalid password: minimum length 8.')
  expect(screen.queryByText('Your password is changed.')).not.toBeInTheDocument()
})

it('stacks the two forms on a phone and puts them side by side on desktop', async () => {
  stubKeycloak()
  renderProfilePage()

  const details = await screen.findByRole('form', { name: 'Details' })
  const password = screen.getByRole('form', { name: 'Password' })

  expect(details.closest('section')).toHaveClass('col-lg-6')
  expect(password.closest('section')).toHaveClass('col-lg-6')
})
