import { render, screen } from '@testing-library/react'
import userEvent from '@testing-library/user-event'
import { afterEach, expect, it, vi } from 'vitest'
import { AuthContext, type Auth } from './auth/AuthContext'
import { NewAreaDialog } from './NewAreaDialog'

const auth: Auth = {
  authenticated: true,
  token: 'jwt',
  accountUrl: undefined,
  profile: undefined,
  roles: ['maintenance-manager'],
  logout: vi.fn(),
  refresh: vi.fn(),
  changePassword: vi.fn(),
}
const corners = [
  { lat: -37.05, lng: 174.85 },
  { lat: -37.05, lng: 174.86 },
  { lat: -37.04, lng: 174.86 },
]
const saved = {
  id: 'area-1',
  code: 'MN',
  name: 'Manukau',
  boundary: { type: 'Polygon', coordinates: [] },
}

function stubApi(response: object = { ok: true, status: 201, json: async () => saved }) {
  const fetchMock = vi.fn(async () => response)
  vi.stubGlobal('fetch', fetchMock)
  return fetchMock
}

function renderDialog() {
  const onCreated = vi.fn()
  const onCancel = vi.fn()
  render(
    <AuthContext.Provider value={auth}>
      <NewAreaDialog corners={corners} onCreated={onCreated} onCancel={onCancel} />
    </AuthContext.Provider>,
  )
  return { onCreated, onCancel }
}

afterEach(() => vi.unstubAllGlobals())

it('needs a two-letter code and a name before it saves', async () => {
  renderDialog()
  const save = screen.getByRole('button', { name: 'Add Area' })
  expect(save).toBeDisabled()

  await userEvent.type(screen.getByLabelText('Code'), 'M')
  await userEvent.type(screen.getByLabelText('Name'), 'Manukau')
  expect(save).toBeDisabled()

  await userEvent.type(screen.getByLabelText('Code'), 'N')
  expect(save).toBeEnabled()
})

it('turns the code into capital letters and keeps only two letters', async () => {
  renderDialog()

  await userEvent.type(screen.getByLabelText('Code'), 'mn1x')

  expect(screen.getByLabelText('Code')).toHaveValue('MN')
})

it('saves the area with its drawn outline, closed, in longitude then latitude order', async () => {
  const fetchMock = stubApi()
  const { onCreated } = renderDialog()

  await userEvent.type(screen.getByLabelText('Code'), 'mn')
  await userEvent.type(screen.getByLabelText('Name'), 'Manukau')
  await userEvent.click(screen.getByRole('button', { name: 'Add Area' }))

  expect(fetchMock).toHaveBeenCalledWith('/api/areas', {
    method: 'POST',
    headers: { Authorization: 'Bearer jwt', 'Content-Type': 'application/json' },
    body: JSON.stringify({
      code: 'MN',
      name: 'Manukau',
      boundary: {
        type: 'Polygon',
        coordinates: [
          [
            [174.85, -37.05],
            [174.86, -37.05],
            [174.86, -37.04],
            [174.85, -37.05],
          ],
        ],
      },
    }),
  })
  expect(onCreated).toHaveBeenCalledWith(saved)
})

it('shows why the API refused the area', async () => {
  stubApi({
    ok: false,
    status: 409,
    json: async () => ({ title: 'The boundary overlaps another Area.' }),
  })
  const { onCreated } = renderDialog()

  await userEvent.type(screen.getByLabelText('Code'), 'mn')
  await userEvent.type(screen.getByLabelText('Name'), 'Manukau')
  await userEvent.click(screen.getByRole('button', { name: 'Add Area' }))

  expect(await screen.findByRole('alert')).toHaveTextContent('The boundary overlaps another Area.')
  expect(onCreated).not.toHaveBeenCalled()
})

it('cancels on request', async () => {
  const { onCancel } = renderDialog()

  await userEvent.click(screen.getByRole('button', { name: 'Cancel' }))

  expect(onCancel).toHaveBeenCalled()
})
