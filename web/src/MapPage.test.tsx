import { render, screen } from '@testing-library/react'
import type { ReactNode } from 'react'
import { expect, it, vi } from 'vitest'
import { MapPage } from './MapPage'

// jsdom cannot draw a Leaflet map, so these stand-ins show the props that MapPage passes.
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
}))

it('opens the map on Auckland, New Zealand', () => {
  render(<MapPage />)

  const map = screen.getByTestId('map')
  expect(map).toHaveAttribute('data-center', '-36.8485,174.7633')
  expect(map).toHaveAttribute('data-zoom', '12')
})

it('draws OpenStreetMap tiles and credits OpenStreetMap', () => {
  render(<MapPage />)

  const tiles = screen.getByTestId('tiles')
  expect(tiles).toHaveAttribute('data-url', 'https://tile.openstreetmap.org/{z}/{x}/{y}.png')
  expect(tiles.getAttribute('data-attribution')).toContain('OpenStreetMap')
})

it('puts the map in a region that is named Map for screen readers', () => {
  render(<MapPage />)

  expect(screen.getByRole('region', { name: 'Map' })).toContainElement(screen.getByTestId('map'))
})
