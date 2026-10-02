import { act, render, screen, waitFor } from '@testing-library/react'
import type { ReactNode } from 'react'
import userEvent from '@testing-library/user-event'
import { afterEach, beforeEach, expect, it, vi } from 'vitest'
import { AuthContext, type Auth } from './auth/AuthContext'
import { MapPage } from './MapPage'

// jsdom cannot draw a Leaflet map, so these stand-ins show the props that MapPage passes.
const leaflet = vi.hoisted(() => {
  const state = {
    zoom: 15,
    handlers: {} as Record<string, (event?: unknown) => void>,
    // Leaflet gives every component the same map object, so the stand-in does too.
    map: {
      getBounds: () => ({
        getWest: () => 174.85,
        getSouth: () => -37.05,
        getEast: () => 174.87,
        getNorth: () => -37.04,
      }),
      getZoom: () => state.zoom,
    },
  }
  return state
})
vi.mock('react-leaflet', () => ({
  MapContainer: ({
    center,
    zoom,
    maxBounds,
    children,
  }: {
    center: [number, number]
    zoom: number
    maxBounds: [number, number][]
    children: ReactNode
  }) => (
    <div
      data-testid="map"
      data-center={center.join(',')}
      data-zoom={zoom}
      data-max-bounds={JSON.stringify(maxBounds)}
    >
      {children}
    </div>
  ),
  TileLayer: ({ url, attribution }: { url: string; attribution: string }) => (
    <div data-testid="tiles" data-url={url} data-attribution={attribution} />
  ),
  Polygon: ({ positions, children }: { positions: [number, number][][]; children: ReactNode }) => (
    <div data-testid="area" data-positions={JSON.stringify(positions)}>
      {children}
    </div>
  ),
  CircleMarker: ({
    center,
    pathOptions,
    eventHandlers,
    bubblingMouseEvents,
    children,
  }: {
    center: [number, number]
    pathOptions: { color: string }
    eventHandlers?: { click: () => void }
    bubblingMouseEvents?: boolean
    children: ReactNode
  }) => (
    <div
      data-testid="asset"
      data-center={center.join(',')}
      data-color={pathOptions.color}
      onClick={eventHandlers?.click}
      data-bubbling={String(bubblingMouseEvents)}
    >
      {children}
    </div>
  ),
  Tooltip: ({ children }: { children: ReactNode }) => <span>{children}</span>,
  useMap: () => leaflet.map,
  useMapEvents: (handlers: Record<string, (event?: unknown) => void>) => {
    Object.assign(leaflet.handlers, handlers)
    return {}
  },
}))

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
const manukau = {
  id: 'area-1',
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
}
const pole = {
  id: 'asset-1',
  friendlyId: 'MN-LP-00001',
  type: 'LightPost',
  areaCode: 'MN',
  latitude: -37.045,
  longitude: 174.855,
  status: 'InService',
}

function stubApi(overrides: Record<string, unknown> = {}) {
  const responses: Record<string, unknown> = {
    '/api/areas': [manukau],
    '/api/assets': [pole],
    ...overrides,
  }
  const fetchMock = vi.fn(async (url: string) => {
    const path = url.split('?')[0]
    return path in responses
      ? { ok: true, json: async () => responses[path] }
      : { ok: false, status: 404, json: async () => ({}) }
  })
  vi.stubGlobal('fetch', fetchMock)
  return fetchMock
}

async function renderMapPage() {
  await act(async () => {
    render(
      <AuthContext.Provider value={auth}>
        <MapPage />
      </AuthContext.Provider>,
    )
  })
}

beforeEach(() => {
  leaflet.zoom = 15
  leaflet.handlers = {}
})
afterEach(() => vi.unstubAllGlobals())

it('opens the map on Auckland, New Zealand', async () => {
  stubApi()

  await renderMapPage()

  const map = screen.getByTestId('map')
  expect(map).toHaveAttribute('data-center', '-36.8485,174.7633')
  expect(map).toHaveAttribute('data-zoom', '12')
})

it('draws OpenStreetMap tiles and credits OpenStreetMap', async () => {
  stubApi()

  await renderMapPage()

  const tiles = screen.getByTestId('tiles')
  expect(tiles).toHaveAttribute('data-url', 'https://tile.openstreetmap.org/{z}/{x}/{y}.png')
  expect(tiles.getAttribute('data-attribution')).toContain('OpenStreetMap')
})

it('puts the map in a region that is named Map for screen readers', async () => {
  stubApi()

  await renderMapPage()

  expect(screen.getByRole('region', { name: 'Map' })).toContainElement(screen.getByTestId('map'))
})

it('uses the full width of the screen instead of the narrow page column', async () => {
  stubApi()

  await renderMapPage()

  expect(screen.getByRole('region', { name: 'Map' })).toHaveClass('container-fluid')
})

it('keeps the map inside the longitudes and latitudes that the API accepts', async () => {
  stubApi()

  await renderMapPage()

  expect(JSON.parse(screen.getByTestId('map').dataset.maxBounds!)).toEqual([
    [-90, -180],
    [90, 180],
  ])
})

it('outlines each area and labels it with its code', async () => {
  stubApi()

  await renderMapPage()

  const area = await screen.findByTestId('area')
  // The API sends longitude first and Leaflet wants latitude first.
  expect(JSON.parse(area.dataset.positions!)[0][0]).toEqual([-37.05, 174.85])
  expect(area).toHaveTextContent('MN')
})

it('marks each asset in view, coloured by its status, with its friendly id', async () => {
  const fetchMock = stubApi({
    '/api/assets': [
      pole,
      { ...pole, id: 'asset-2', friendlyId: 'MN-LP-00002', status: 'PendingInstallation' },
      { ...pole, id: 'asset-3', friendlyId: 'MN-LP-00003', status: 'Removed' },
    ],
  })

  await renderMapPage()

  const assets = await screen.findAllByTestId('asset')
  expect(assets.map((asset) => asset.dataset.color)).toEqual(['#198754', '#f0ad4e', '#6c757d'])
  expect(assets[0]).toHaveAttribute('data-center', '-37.045,174.855')
  expect(assets[0]).toHaveTextContent('MN-LP-00001')
  expect(fetchMock).toHaveBeenCalledWith(
    '/api/assets?west=174.85&south=-37.05&east=174.87&north=-37.04',
    expect.anything(),
  )
})

it('asks for the assets again when the user moves the map', async () => {
  const fetchMock = stubApi()
  await renderMapPage()
  await screen.findByTestId('asset')
  fetchMock.mockClear()

  await act(async () => leaflet.handlers.moveend())

  await waitFor(() =>
    expect(fetchMock).toHaveBeenCalledWith(
      expect.stringContaining('/api/assets?'),
      expect.anything(),
    ),
  )
})

it('asks the user to zoom in instead of loading assets from far away', async () => {
  leaflet.zoom = 11
  const fetchMock = stubApi()

  await renderMapPage()

  expect(await screen.findByText('Zoom in to see Assets.')).toBeInTheDocument()
  expect(screen.queryByTestId('asset')).not.toBeInTheDocument()
  expect(fetchMock).not.toHaveBeenCalledWith(
    expect.stringContaining('/api/assets'),
    expect.anything(),
  )
})

it('says the status of each asset in words, not only in colour', async () => {
  stubApi({
    '/api/assets': [
      pole,
      { ...pole, id: 'asset-2', friendlyId: 'MN-LP-00002', status: 'PendingInstallation' },
      { ...pole, id: 'asset-3', friendlyId: 'MN-LP-00003', status: 'Removed' },
    ],
  })

  await renderMapPage()

  const assets = await screen.findAllByTestId('asset')
  expect(assets[0]).toHaveTextContent('MN-LP-00001 In Service')
  expect(assets[1]).toHaveTextContent('MN-LP-00002 Pending Installation')
  expect(assets[2]).toHaveTextContent('MN-LP-00003 Removed')
})

it('clears the problem with the assets when a later request for them works', async () => {
  let assetsFail = true
  vi.stubGlobal(
    'fetch',
    vi.fn(async (url: string) => {
      if (url.startsWith('/api/areas')) return { ok: true, json: async () => [manukau] }
      return assetsFail
        ? { ok: false, status: 500, json: async () => ({}) }
        : { ok: true, json: async () => [pole] }
    }),
  )
  await renderMapPage()
  expect(await screen.findByRole('alert')).toHaveTextContent('The API returned status 500.')

  assetsFail = false
  await act(async () => leaflet.handlers.moveend())

  await waitFor(() => expect(screen.queryByRole('alert')).not.toBeInTheDocument())
  expect(await screen.findByTestId('asset')).toBeInTheDocument()
})

it('keeps the problem with the areas when the assets load', async () => {
  vi.stubGlobal(
    'fetch',
    vi.fn(async (url: string) =>
      url.startsWith('/api/areas')
        ? { ok: false, status: 503, json: async () => ({}) }
        : { ok: true, json: async () => [pole] },
    ),
  )

  await renderMapPage()

  expect(await screen.findByTestId('asset')).toBeInTheDocument()
  expect(screen.getByRole('alert')).toHaveTextContent('The API returned status 503.')
})

it('shows the assets of the latest view when an earlier request answers last', async () => {
  const answers: ((assets: unknown[]) => void)[] = []
  vi.stubGlobal(
    'fetch',
    vi.fn(async (url: string) => {
      if (url.startsWith('/api/areas')) return { ok: true, json: async () => [manukau] }
      return { ok: true, json: () => new Promise((resolve) => answers.push(resolve)) }
    }),
  )
  await renderMapPage()
  await waitFor(() => expect(answers).toHaveLength(1))
  await act(async () => leaflet.handlers.moveend())
  await waitFor(() => expect(answers).toHaveLength(2))

  await act(async () => answers[1]([{ ...pole, friendlyId: 'MN-LP-00002' }]))
  await act(async () => answers[0]([pole]))

  expect(screen.getByTestId('asset')).toHaveTextContent('MN-LP-00002')
})

it('tells the user when the API refuses to answer', async () => {
  stubApi({ '/api/areas': undefined })
  vi.stubGlobal(
    'fetch',
    vi.fn(async () => ({ ok: false, status: 500, json: async () => ({}) })),
  )

  await renderMapPage()

  expect(await screen.findAllByRole('alert')).not.toHaveLength(0)
  expect(screen.getAllByRole('alert')[0]).toHaveTextContent('The API returned status 500.')
})

it('offers to add an asset where the user clicks the map', async () => {
  stubApi()
  await renderMapPage()

  await act(async () => leaflet.handlers.click({ latlng: { lat: -37.0451, lng: 174.8552 } }))

  expect(screen.getByRole('dialog', { name: 'Add an Asset' })).toHaveTextContent(
    '-37.04510, 174.85520',
  )
})

it('puts a new asset on the map as pending installation and says which friendly id it got', async () => {
  const created = {
    ...pole,
    id: 'asset-9',
    friendlyId: 'MN-SS-00001',
    status: 'PendingInstallation',
  }
  const fetchMock = stubApi({ '/api/assets': [] })
  fetchMock.mockImplementation(async (url: string, init?: RequestInit) => ({
    ok: true,
    status: init?.method === 'POST' ? 201 : 200,
    json: async () => (init?.method === 'POST' ? created : url.startsWith('/api/areas') ? [] : []),
  }))
  await renderMapPage()
  await act(async () => leaflet.handlers.click({ latlng: { lat: -37.045, lng: 174.855 } }))

  await userEvent.click(screen.getByRole('button', { name: 'Add Asset' }))

  const marker = await screen.findByTestId('asset')
  expect(marker).toHaveAttribute('data-color', '#f0ad4e')
  expect(screen.queryByRole('dialog')).not.toBeInTheDocument()
  expect(screen.getByRole('status')).toHaveTextContent('Added MN-SS-00001')
})

it('closes the dialog without adding anything when the user cancels', async () => {
  stubApi()
  await renderMapPage()
  await act(async () => leaflet.handlers.click({ latlng: { lat: -37.045, lng: 174.855 } }))

  await userEvent.click(screen.getByRole('button', { name: 'Cancel' }))

  expect(screen.queryByRole('dialog')).not.toBeInTheDocument()
})

it('opens the history of an asset when the user clicks it', async () => {
  const fetchMock = stubApi({ '/api/assets/asset-1/events': [] })
  await renderMapPage()

  await userEvent.click(await screen.findByTestId('asset'))

  expect(await screen.findByRole('heading', { name: 'MN-LP-00001' })).toBeInTheDocument()
  expect(fetchMock).toHaveBeenCalledWith('/api/assets/asset-1/events', expect.anything())
})

it('closes the history of an asset on request', async () => {
  stubApi({ '/api/assets/asset-1/events': [] })
  await renderMapPage()
  await userEvent.click(await screen.findByTestId('asset'))
  await screen.findByRole('heading', { name: 'MN-LP-00001' })

  await userEvent.click(screen.getByRole('button', { name: 'Close' }))

  expect(screen.queryByRole('heading', { name: 'MN-LP-00001' })).not.toBeInTheDocument()
})

it('does not pass a click on an asset through to the map, so that it does not open the add dialog', async () => {
  stubApi()
  await renderMapPage()

  expect(await screen.findByTestId('asset')).toHaveAttribute('data-bubbling', 'false')
})

it('keeps a new asset on the map when a list request that began before it answers afterwards', async () => {
  const created = {
    ...pole,
    id: 'asset-9',
    friendlyId: 'MN-SS-00001',
    status: 'PendingInstallation',
  }
  const listAnswers: ((assets: unknown[]) => void)[] = []
  vi.stubGlobal(
    'fetch',
    vi.fn(async (url: string, init?: RequestInit) => {
      if (init?.method === 'POST') return { ok: true, status: 201, json: async () => created }
      if (url.startsWith('/api/areas')) return { ok: true, json: async () => [] }
      return { ok: true, json: () => new Promise((resolve) => listAnswers.push(resolve)) }
    }),
  )
  await renderMapPage()
  await waitFor(() => expect(listAnswers).toHaveLength(1))
  await act(async () => leaflet.handlers.click({ latlng: { lat: -37.045, lng: 174.855 } }))
  await userEvent.click(screen.getByRole('button', { name: 'Add Asset' }))
  expect(await screen.findByTestId('asset')).toHaveTextContent('MN-SS-00001')

  await act(async () => listAnswers[0]([]))

  expect(screen.getByTestId('asset')).toHaveTextContent('MN-SS-00001')
})
