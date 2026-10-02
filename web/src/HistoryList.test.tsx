import { act, render, screen } from '@testing-library/react'
import { afterEach, expect, it, vi } from 'vitest'
import { AuthContext, type Auth } from './auth/AuthContext'
import { HistoryList } from './HistoryList'

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
const change = (value: string) => ({
  entityType: 'Asset',
  entityId: 'asset-1',
  operation: 'Created',
  field: 'FriendlyId',
  oldValue: null,
  newValue: value,
  changedBy: 'user-1',
  changedByName: 'Grace Hopper',
  changedAt: '2026-10-02T03:00:00Z',
})

afterEach(() => vi.unstubAllGlobals())

function setUp() {
  const answers: Record<string, (response: object) => void> = {}
  vi.stubGlobal(
    'fetch',
    vi.fn(
      (url: string) =>
        new Promise((resolve) => {
          answers[url] = resolve
        }),
    ),
  )
  const view = (assetId: string) => (
    <AuthContext.Provider value={auth}>
      <HistoryList assetId={assetId} />
    </AuthContext.Provider>
  )
  return { answers, view }
}

it('shows the changes of the Asset that is selected now, not those of the one before', async () => {
  const { answers, view } = setUp()
  const { rerender } = render(view('asset-1'))
  await act(async () => rerender(view('asset-2')))

  expect(screen.getByRole('status')).toHaveTextContent('Loading the changes')
  await act(async () =>
    answers['/api/assets/asset-2/history']({ ok: true, json: async () => [change('NEW-2')] }),
  )
  await act(async () =>
    answers['/api/assets/asset-1/history']({ ok: true, json: async () => [change('OLD-1')] }),
  )

  expect(screen.getByText(/NEW-2/)).toBeInTheDocument()
  expect(screen.queryByText(/OLD-1/)).not.toBeInTheDocument()
})

it('drops the problem and the list of the earlier Asset when another Asset is selected', async () => {
  const { answers, view } = setUp()
  const { rerender } = render(view('asset-1'))
  await act(async () =>
    answers['/api/assets/asset-1/history']({ ok: false, status: 503, json: async () => ({}) }),
  )
  expect(await screen.findByRole('alert')).toBeInTheDocument()

  await act(async () => rerender(view('asset-2')))
  expect(screen.queryByRole('alert')).not.toBeInTheDocument()
  await act(async () =>
    answers['/api/assets/asset-2/history']({ ok: true, json: async () => [change('NEW-2')] }),
  )

  expect(screen.getByText(/NEW-2/)).toBeInTheDocument()
  expect(screen.queryByRole('alert')).not.toBeInTheDocument()
})
