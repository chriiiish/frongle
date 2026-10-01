import { act, cleanup, render, screen, waitFor } from '@testing-library/react'
import type { ReactNode } from 'react'
import userEvent from '@testing-library/user-event'
import { afterEach, beforeEach, expect, it, vi } from 'vitest'
import { AuthContext, type Auth } from './auth/AuthContext'
import { MapPage } from './MapPage'

// jsdom cannot draw a Leaflet map, so these stand-ins show the props that MapPage passes.
const leaflet = vi.hoisted(() => {
  const state = {
    zoom: 15,
    flownTo: undefined as { center: [number, number]; zoom: number } | undefined,
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
      setView: (center: [number, number], zoom: number) => {
        state.flownTo = { center, zoom }
      },
    },
  }
  return state
})
vi.mock('react-leaflet', () => ({
  MapContainer: ({
    center,
    zoom,
    children,
  }: {
    center: [number, number]
    zoom: number
    children: ReactNode
  }) => (
    <div data-testid="map" data-center={center.join(',')} data-zoom={zoom}>
      {children}
    </div>
  ),
  TileLayer: ({ url, attribution }: { url: string; attribution: string }) => (
    <div data-testid="tiles" data-url={url} data-attribution={attribution} />
  ),
  Polygon: ({
    positions,
    pathOptions,
    children,
  }: {
    positions: [number, number][][] | [number, number][]
    pathOptions: { dashArray?: string }
    children: ReactNode
  }) => (
    <div
      data-testid={pathOptions.dashArray ? 'outline' : 'area'}
      data-positions={JSON.stringify(positions)}
    >
      {children}
    </div>
  ),
  CircleMarker: ({
    center,
    pathOptions,
    eventHandlers,
    children,
  }: {
    center: [number, number]
    pathOptions: { color: string; className?: string }
    eventHandlers?: { click: () => void }
    children: ReactNode
  }) => (
    <div
      data-testid={pathOptions.className === 'corner' ? 'corner' : 'asset'}
      data-center={center.join(',')}
      data-color={pathOptions.color}
      onClick={eventHandlers?.click}
    >
      {children}
    </div>
  ),
  Tooltip: ({ children }: { children: ReactNode }) => <span>{children}</span>,
  ZoomControl: () => null,
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
  needsRetag: false,
  formerFriendlyIds: [],
  version: 5,
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

async function renderMapPage(roles = auth.roles) {
  await act(async () => {
    render(
      <AuthContext.Provider value={{ ...auth, roles }}>
        <MapPage />
      </AuthContext.Provider>,
    )
  })
}

beforeEach(() => {
  leaflet.zoom = 15
  leaflet.flownTo = undefined
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

const MANAGER = ['maintenance-manager']
const click = (lat: number, lng: number) =>
  act(async () => leaflet.handlers.click({ latlng: { lat, lng } }))

it('offers the draw tool to a Maintenance Manager and to nobody else', async () => {
  stubApi()
  await renderMapPage()
  expect(screen.queryByRole('button', { name: 'Draw Area' })).not.toBeInTheDocument()
  cleanup()

  await renderMapPage(MANAGER)

  expect(screen.getByRole('button', { name: 'Draw Area' })).toBeInTheDocument()
})

it('marks each corner of an area that the manager draws, instead of adding an asset', async () => {
  stubApi()
  await renderMapPage(MANAGER)

  await userEvent.click(screen.getByRole('button', { name: 'Draw Area' }))
  await click(-37.05, 174.85)
  await click(-37.05, 174.86)

  expect(screen.getByText('Click the map to mark each corner of the Area.')).toBeInTheDocument()
  expect(screen.getAllByTestId('corner')).toHaveLength(2)
  expect(screen.queryByRole('dialog')).not.toBeInTheDocument()
  expect(screen.getByRole('button', { name: 'Finish' })).toBeDisabled()
})

it('takes back the last corner and stops drawing on request', async () => {
  stubApi()
  await renderMapPage(MANAGER)
  await userEvent.click(screen.getByRole('button', { name: 'Draw Area' }))
  await click(-37.05, 174.85)
  await click(-37.05, 174.86)

  await userEvent.click(screen.getByRole('button', { name: 'Undo' }))
  expect(screen.getAllByTestId('corner')).toHaveLength(1)

  await userEvent.click(screen.getByRole('button', { name: 'Cancel' }))
  expect(screen.queryByTestId('corner')).not.toBeInTheDocument()
  expect(screen.getByRole('button', { name: 'Draw Area' })).toBeInTheDocument()
})

it('outlines the area while the manager draws it', async () => {
  stubApi()
  await renderMapPage(MANAGER)
  await userEvent.click(screen.getByRole('button', { name: 'Draw Area' }))
  await click(-37.05, 174.85)
  await click(-37.05, 174.86)
  await click(-37.04, 174.86)

  expect(JSON.parse(screen.getByTestId('outline').dataset.positions!)).toEqual([
    [-37.05, 174.85],
    [-37.05, 174.86],
    [-37.04, 174.86],
  ])
})

it('names the area after the manager finishes drawing and shows it on the map when it is saved', async () => {
  const saved = { id: 'area-2', code: 'OT', name: 'Otara', boundary: manukau.boundary }
  const fetchMock = stubApi({ '/api/areas': [] })
  fetchMock.mockImplementation(async (_url: string, init?: RequestInit) => ({
    ok: true,
    status: init?.method === 'POST' ? 201 : 200,
    json: async () => (init?.method === 'POST' ? saved : []),
  }))
  await renderMapPage(MANAGER)
  await userEvent.click(screen.getByRole('button', { name: 'Draw Area' }))
  await click(-37.05, 174.85)
  await click(-37.05, 174.86)
  await click(-37.04, 174.86)

  await userEvent.click(screen.getByRole('button', { name: 'Finish' }))
  await userEvent.type(screen.getByLabelText('Code'), 'ot')
  await userEvent.type(screen.getByLabelText('Name'), 'Otara')
  await userEvent.click(screen.getByRole('button', { name: 'Add Area' }))

  expect(await screen.findByTestId('area')).toHaveTextContent('OT')
  expect(screen.queryByRole('dialog')).not.toBeInTheDocument()
  expect(screen.queryByTestId('outline')).not.toBeInTheDocument()
  expect(screen.getByRole('button', { name: 'Draw Area' })).toBeInTheDocument()
})

const moved = {
  ...pole,
  friendlyId: 'OT-LP-00001',
  areaCode: 'OT',
  needsRetag: true,
  version: 6,
  latitude: -37.0451,
  longitude: 174.8651,
}

async function selectPole() {
  await userEvent.click(await screen.findByTestId('asset'))
  await screen.findByRole('heading', { name: 'MN-LP-00001' })
}

it('moves an asset to the place that the user clicks, after the user confirms', async () => {
  const fetchMock = stubApi({ '/api/assets/asset-1/events': [] })
  fetchMock.mockImplementation(async (url: string, init?: RequestInit) => {
    const path = url.split('?')[0]
    const body =
      init?.method === 'PUT'
        ? moved
        : path === '/api/areas'
          ? [manukau]
          : path === '/api/assets'
            ? [pole]
            : []
    return { ok: true, status: 200, json: async () => body }
  })
  await renderMapPage()
  await selectPole()

  await userEvent.click(screen.getByRole('button', { name: 'Move' }))
  expect(screen.getByText('Click the new place for MN-LP-00001.')).toBeInTheDocument()
  await click(-37.0451, 174.8651)
  expect(screen.queryByRole('dialog', { name: 'Add an Asset' })).not.toBeInTheDocument()
  await userEvent.click(screen.getByRole('button', { name: 'Move Asset' }))

  const marker = await screen.findByTestId('asset')
  expect(marker).toHaveAttribute('data-center', '-37.0451,174.8651')
  expect(await screen.findByRole('heading', { name: 'OT-LP-00001' })).toBeInTheDocument()
})

it('stops the move when the user cancels', async () => {
  stubApi({ '/api/assets/asset-1/events': [] })
  await renderMapPage()
  await selectPole()
  await userEvent.click(screen.getByRole('button', { name: 'Move' }))

  await userEvent.click(screen.getByRole('button', { name: 'Cancel' }))
  await click(-37.0451, 174.8651)

  expect(screen.queryByText('Click the new place for MN-LP-00001.')).not.toBeInTheDocument()
  expect(screen.getByRole('dialog', { name: 'Add an Asset' })).toBeInTheDocument()
})

it('finds an asset by friendly id, shows it on the map, and opens its history', async () => {
  const found = {
    ...pole,
    id: 'asset-7',
    friendlyId: 'MN-SS-00007',
    latitude: -37.041,
    longitude: 174.851,
  }
  const fetchMock = stubApi({ '/api/assets/search': [found], '/api/assets/asset-7/events': [] })
  await renderMapPage()

  await userEvent.type(screen.getByRole('searchbox', { name: 'Find an Asset' }), 'SS-00007{Enter}')
  await userEvent.click(await screen.findByRole('button', { name: /MN-SS-00007/ }))

  expect(await screen.findByRole('heading', { name: 'MN-SS-00007' })).toBeInTheDocument()
  expect(fetchMock).toHaveBeenCalledWith('/api/assets/asset-7/events', expect.anything())
  expect(leaflet.flownTo).toEqual({ center: [-37.041, 174.851], zoom: 17 })
})
