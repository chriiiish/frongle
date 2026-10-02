import { act, render, screen } from '@testing-library/react'
import userEvent from '@testing-library/user-event'
import { afterEach, expect, it, vi } from 'vitest'
import type { Asset } from './api'
import { AuthContext, type Auth } from './auth/AuthContext'
import { SearchBox } from './SearchBox'

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
  friendlyId: 'OT-LP-00001',
  type: 'LightPost',
  areaCode: 'OT',
  latitude: -37.045,
  longitude: 174.865,
  status: 'InService',
  needsRetag: true,
  formerFriendlyIds: ['MN-LP-00001'],
  version: 8,
}

function renderSearch(results: Asset[] = [pole]) {
  const fetchMock = vi.fn(async () => ({ ok: true, json: async () => results }))
  vi.stubGlobal('fetch', fetchMock)
  const onPick = vi.fn()
  render(
    <AuthContext.Provider value={auth}>
      <SearchBox onPick={onPick} />
    </AuthContext.Provider>,
  )
  return { fetchMock, onPick }
}

afterEach(() => vi.unstubAllGlobals())

it('searches by friendly id and lists each match, with the former id that matched', async () => {
  const { fetchMock } = renderSearch()

  await userEvent.type(screen.getByRole('searchbox', { name: 'Find an Asset' }), 'MN-LP')
  await userEvent.click(screen.getByRole('button', { name: 'Search' }))

  expect(fetchMock).toHaveBeenCalledWith('/api/assets/search?q=MN-LP', expect.anything())
  const match = await screen.findByRole('button', { name: /OT-LP-00001/ })
  expect(match).toHaveTextContent('was MN-LP-00001')
})

it('hands back the asset that the user picks and closes the list', async () => {
  const { onPick } = renderSearch()
  await userEvent.type(screen.getByRole('searchbox', { name: 'Find an Asset' }), 'OT{Enter}')

  await userEvent.click(await screen.findByRole('button', { name: /OT-LP-00001/ }))

  expect(onPick).toHaveBeenCalledWith(pole)
  expect(screen.queryByRole('button', { name: /OT-LP-00001/ })).not.toBeInTheDocument()
})

it('says when nothing matches', async () => {
  renderSearch([])

  await userEvent.type(screen.getByRole('searchbox', { name: 'Find an Asset' }), 'ZZ{Enter}')

  expect(await screen.findByText('No Asset matches.')).toBeInTheDocument()
})

it('does not search for an empty text', async () => {
  const { fetchMock } = renderSearch()

  await userEvent.click(screen.getByRole('button', { name: 'Search' }))

  expect(fetchMock).not.toHaveBeenCalled()
})

it('shows the answer to the latest search when an earlier search answers last', async () => {
  const answers: ((assets: Asset[]) => void)[] = []
  vi.stubGlobal(
    'fetch',
    vi.fn(
      () =>
        new Promise((resolve) =>
          answers.push((assets) => resolve({ ok: true, json: async () => assets })),
        ),
    ),
  )
  render(
    <AuthContext.Provider value={auth}>
      <SearchBox onPick={vi.fn()} />
    </AuthContext.Provider>,
  )
  const box = screen.getByRole('searchbox', { name: 'Find an Asset' })
  await userEvent.type(box, 'MN{Enter}')
  await userEvent.clear(box)
  await userEvent.type(box, 'OT{Enter}')

  await act(async () => answers[1]([{ ...pole, friendlyId: 'OT-LP-00001' }]))
  await act(async () => answers[0]([{ ...pole, id: 'asset-2', friendlyId: 'MN-LP-00009' }]))

  expect(screen.getByRole('button', { name: /OT-LP-00001/ })).toBeInTheDocument()
  expect(screen.queryByRole('button', { name: /MN-LP-00009/ })).not.toBeInTheDocument()
})
