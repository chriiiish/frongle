import { render, screen } from '@testing-library/react'
import userEvent from '@testing-library/user-event'
import { afterEach, expect, it, vi } from 'vitest'
import type { Asset } from './api'
import { AuthContext, type Auth } from './auth/AuthContext'
import { MoveAssetDialog } from './MoveAssetDialog'

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
const pole: Asset = {
  id: 'asset-1',
  friendlyId: 'MN-LP-00001',
  type: 'LightPost',
  areaCode: 'MN',
  latitude: -37.045,
  longitude: 174.855,
  status: 'InService',
  needsRetag: false,
  formerFriendlyIds: [],
  version: 7,
}
const moved = { ...pole, friendlyId: 'OT-LP-00001', areaCode: 'OT', needsRetag: true, version: 8 }
const where = { lat: -37.045, lng: 174.865 }

function renderDialog(response: object = { ok: true, status: 200, json: async () => moved }) {
  const fetchMock = vi.fn(async () => response)
  vi.stubGlobal('fetch', fetchMock)
  const onMoved = vi.fn()
  const onCancel = vi.fn()
  render(
    <AuthContext.Provider value={auth}>
      <MoveAssetDialog asset={pole} location={where} onMoved={onMoved} onCancel={onCancel} />
    </AuthContext.Provider>,
  )
  return { fetchMock, onMoved, onCancel }
}

afterEach(() => vi.unstubAllGlobals())

it('asks the user to confirm the new place and warns about a retag', () => {
  renderDialog()

  expect(screen.getByRole('dialog', { name: 'Move MN-LP-00001' })).toBeInTheDocument()
  expect(screen.getByText('-37.04500, 174.86500')).toBeInTheDocument()
  expect(screen.getByText(/another Area, the Asset gets a new Friendly Id/)).toBeInTheDocument()
})

it('moves the asset and hands the moved asset back', async () => {
  const { fetchMock, onMoved } = renderDialog()

  await userEvent.click(screen.getByRole('button', { name: 'Move Asset' }))

  expect(fetchMock).toHaveBeenCalledWith(
    '/api/assets/asset-1/location',
    expect.objectContaining({ method: 'PUT' }),
  )
  expect(onMoved).toHaveBeenCalledWith(moved)
})

it('shows why the API refused the move', async () => {
  const { onMoved } = renderDialog({
    ok: false,
    status: 409,
    json: async () => ({
      title: 'Someone else changed this Asset. Read it again and redo your change.',
    }),
  })

  await userEvent.click(screen.getByRole('button', { name: 'Move Asset' }))

  expect(await screen.findByRole('alert')).toHaveTextContent('Someone else changed this Asset.')
  expect(onMoved).not.toHaveBeenCalled()
})

it('cancels on request', async () => {
  const { onCancel } = renderDialog()

  await userEvent.click(screen.getByRole('button', { name: 'Cancel' }))

  expect(onCancel).toHaveBeenCalled()
})

it('cannot be closed while the move is being saved, so that a move the user cancelled is not applied', async () => {
  vi.stubGlobal(
    'fetch',
    vi.fn(() => new Promise(() => {})),
  )
  const onCancel = vi.fn()
  render(
    <AuthContext.Provider value={auth}>
      <MoveAssetDialog asset={pole} location={where} onMoved={vi.fn()} onCancel={onCancel} />
    </AuthContext.Provider>,
  )
  await userEvent.click(screen.getByRole('button', { name: 'Move Asset' }))

  expect(screen.getByRole('button', { name: 'Cancel' })).toBeDisabled()
  expect(screen.getByRole('button', { name: 'Close' })).toBeDisabled()
  await userEvent.keyboard('{Escape}')
  expect(onCancel).not.toHaveBeenCalled()
})
