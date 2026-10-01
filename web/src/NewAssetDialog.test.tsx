import { render, screen } from '@testing-library/react'
import userEvent from '@testing-library/user-event'
import { afterEach, expect, it, vi } from 'vitest'
import { AuthContext, type Auth } from './auth/AuthContext'
import { NewAssetDialog } from './NewAssetDialog'

const auth: Auth = {
  authenticated: true,
  token: 'jwt',
  accountUrl: undefined,
  profile: undefined,
  roles: ['work-team'],
  logout: vi.fn(),
  refresh: vi.fn(),
  changePassword: vi.fn(),
}
const created = {
  id: 'asset-1',
  friendlyId: 'MN-SS-00001',
  type: 'StreetSign',
  areaCode: 'MN',
  latitude: -37.045,
  longitude: 174.855,
  status: 'PendingInstallation',
  needsRetag: false,
  formerFriendlyIds: [],
  version: 1,
}
const where = { lat: -37.045, lng: 174.855 }

function stubApi(response: object = { ok: true, status: 201, json: async () => created }) {
  const fetchMock = vi.fn(async () => response)
  vi.stubGlobal('fetch', fetchMock)
  return fetchMock
}

function renderDialog(onCreated = vi.fn(), onCancel = vi.fn()) {
  render(
    <AuthContext.Provider value={auth}>
      <NewAssetDialog location={where} onCreated={onCreated} onCancel={onCancel} />
    </AuthContext.Provider>,
  )
  return { onCreated, onCancel }
}

afterEach(() => vi.unstubAllGlobals())

it('names the place that the user clicked and offers every type of asset', () => {
  renderDialog()

  expect(screen.getByRole('dialog', { name: 'Add an Asset' })).toBeInTheDocument()
  expect(screen.getByText('-37.04500, 174.85500')).toBeInTheDocument()
  const types = screen.getAllByRole('option').map((option) => option.textContent)
  expect(types).toEqual(['Light-post', 'Street sign', 'Telephone pole', 'Traffic light'])
})

it('adds the chosen type of asset at the clicked place and hands the new asset back', async () => {
  const fetchMock = stubApi()
  const { onCreated } = renderDialog()

  await userEvent.selectOptions(screen.getByLabelText('Type'), 'Street sign')
  await userEvent.click(screen.getByRole('button', { name: 'Add Asset' }))

  expect(fetchMock).toHaveBeenCalledWith('/api/assets', {
    method: 'POST',
    headers: { Authorization: 'Bearer jwt', 'Content-Type': 'application/json' },
    body: JSON.stringify({ type: 'StreetSign', latitude: -37.045, longitude: 174.855 }),
  })
  expect(onCreated).toHaveBeenCalledWith(created)
})

it('shows why the API refused and stays open so that the user can cancel', async () => {
  stubApi({
    ok: false,
    status: 422,
    json: async () => ({
      title: 'This spot is outside every area. Ask a Maintenance Manager to draw one.',
    }),
  })
  const { onCreated, onCancel } = renderDialog()

  await userEvent.click(screen.getByRole('button', { name: 'Add Asset' }))

  expect(await screen.findByRole('alert')).toHaveTextContent('This spot is outside every area.')
  expect(onCreated).not.toHaveBeenCalled()
  await userEvent.click(screen.getByRole('button', { name: 'Cancel' }))
  expect(onCancel).toHaveBeenCalled()
})
