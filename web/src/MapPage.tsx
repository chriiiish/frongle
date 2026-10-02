import 'leaflet/dist/leaflet.css'
import { useCallback, useEffect, useRef, useState } from 'react'
import {
  CircleMarker,
  MapContainer,
  Polygon,
  TileLayer,
  Tooltip,
  useMap,
  useMapEvents,
} from 'react-leaflet'
import { type Area, type Asset, type AssetStatus, type Bounds } from './api'
import { AssetPanel } from './AssetPanel'
import { NewAssetDialog, type Place } from './NewAssetDialog'
import { useApi } from './useApi'

const AUCKLAND: [number, number] = [-36.8485, 174.7633]
const START_ZOOM = 12
// Below this zoom a screen covers too much ground to list every Asset.
const MIN_ASSET_ZOOM = 14
const MOVE_DELAY_MS = 250
const TILE_URL = 'https://tile.openstreetmap.org/{z}/{x}/{y}.png'
const TILE_CREDIT =
  '&copy; <a href="https://www.openstreetmap.org/copyright">OpenStreetMap</a> contributors'

// Leaflet repeats the world sideways, but the API accepts only longitudes from -180 to 180.
const WORLD_BOUNDS: [number, number][] = [
  [-90, -180],
  [90, 180],
]

const STATUS_COLOUR: Record<AssetStatus, string> = {
  PendingInstallation: '#f0ad4e',
  InService: '#198754',
  Removed: '#6c757d',
}

// Colour alone fails for users who cannot tell the colours apart, so the tooltip also says the status.
const STATUS_LABEL: Record<AssetStatus, string> = {
  PendingInstallation: 'Pending Installation',
  InService: 'In Service',
  Removed: 'Removed',
}

interface MapView {
  bounds: Bounds
  zoom: number
}

/** Tells the page which part of the map the user sees, at the start and after every move. */
function ViewWatcher({ onChange }: { onChange: (view: MapView) => void }) {
  const map = useMap()
  const report = useCallback(() => {
    const bounds = map.getBounds()
    onChange({
      bounds: {
        west: bounds.getWest(),
        south: bounds.getSouth(),
        east: bounds.getEast(),
        north: bounds.getNorth(),
      },
      zoom: map.getZoom(),
    })
  }, [map, onChange])
  useMapEvents({ moveend: report })
  useEffect(report, [report])
  return null
}

/** Tells the page where the user clicked the map. */
function ClickWatcher({ onClick }: { onClick: (place: Place) => void }) {
  useMapEvents({ click: (event) => onClick(event.latlng) })
  return null
}

/** The API sends longitude first, as GeoJSON does, and Leaflet wants latitude first. */
function toLatLngRings(area: Area): [number, number][][] {
  return area.boundary.coordinates.map((ring) => ring.map(([lng, lat]) => [lat, lng]))
}

/** The map page: a street map that opens on Auckland, New Zealand, with the Areas and the Assets in view. */
export function MapPage() {
  const api = useApi()
  const [areas, setAreas] = useState<Area[]>([])
  const [assets, setAssets] = useState<Asset[]>([])
  const [view, setView] = useState<MapView>()
  const [adding, setAdding] = useState<Place>()
  const [added, setAdded] = useState<string>()
  const [selectedId, setSelectedId] = useState<string>()
  const selected = assets.find((asset) => asset.id === selectedId)
  const [areasProblem, setAreasProblem] = useState<string>()
  const [assetsProblem, setAssetsProblem] = useState<string>()
  // Counts the Assets that the user added, so that a list request that began before one cannot answer without it.
  const additions = useRef(0)
  const zoomedIn = view !== undefined && view.zoom >= MIN_ASSET_ZOOM

  useEffect(() => {
    let current = true
    api.listAreas().then(
      (listed) => {
        if (!current) return
        setAreas(listed)
        setAreasProblem(undefined)
      },
      (failure: Error) => current && setAreasProblem(failure.message),
    )
    return () => {
      current = false
    }
  }, [api])

  useEffect(() => {
    if (!view || !zoomedIn) return
    // A slow answer for an earlier view must not replace the Assets of the view the user sees now.
    let current = true
    const timer = setTimeout(() => {
      const addedBefore = additions.current
      void api.listAssets(view.bounds).then(
        (listed) => {
          if (!current || addedBefore !== additions.current) return
          setAssets(listed)
          setAssetsProblem(undefined)
        },
        (failure: Error) => current && setAssetsProblem(failure.message),
      )
    }, MOVE_DELAY_MS)
    return () => {
      current = false
      clearTimeout(timer)
    }
  }, [api, view, zoomedIn])

  return (
    <section className="container-fluid p-0 p-md-3 position-relative" aria-label="Map">
      <MapContainer
        className="map"
        center={AUCKLAND}
        zoom={START_ZOOM}
        maxBounds={WORLD_BOUNDS}
        maxBoundsViscosity={1}
      >
        <TileLayer url={TILE_URL} attribution={TILE_CREDIT} />
        <ViewWatcher onChange={setView} />
        <ClickWatcher onClick={setAdding} />
        {areas.map((area) => (
          <Polygon
            key={area.id}
            positions={toLatLngRings(area)}
            interactive={false}
            pathOptions={{ color: '#191919', weight: 2, fillOpacity: 0.05 }}
          >
            <Tooltip permanent direction="center" interactive={false}>
              {area.code}
            </Tooltip>
          </Polygon>
        ))}
        {zoomedIn &&
          assets.map((asset) => (
            <CircleMarker
              key={asset.id}
              center={[asset.latitude, asset.longitude]}
              radius={9}
              bubblingMouseEvents={false}
              eventHandlers={{ click: () => setSelectedId(asset.id) }}
              pathOptions={{ color: STATUS_COLOUR[asset.status], fillOpacity: 0.9 }}
            >
              <Tooltip>
                {asset.friendlyId} {STATUS_LABEL[asset.status]}
              </Tooltip>
            </CircleMarker>
          ))}
      </MapContainer>
      <div className="position-absolute top-0 start-50 translate-middle-x mt-3 mt-md-5 map-notice">
        {view && !zoomedIn && (
          <p className="alert alert-info py-1 px-3 shadow-sm" role="status">
            Zoom in to see Assets.
          </p>
        )}
        {added && (
          <p className="alert alert-success py-1 px-3 shadow-sm" role="status">
            Added {added}.
          </p>
        )}
        {Object.entries({ areas: areasProblem, assets: assetsProblem }).map(
          ([source, problem]) =>
            problem && (
              <p key={source} className="alert alert-danger py-1 px-3 shadow-sm" role="alert">
                {problem}
              </p>
            ),
        )}
      </div>
      {selected && (
        <AssetPanel
          asset={selected}
          onChanged={(changed) =>
            setAssets((current) => current.map((a) => (a.id === changed.id ? changed : a)))
          }
          onClose={() => setSelectedId(undefined)}
        />
      )}
      {adding && (
        <NewAssetDialog
          location={adding}
          onCreated={(asset) => {
            additions.current += 1
            setAssets((current) => [...current, asset])
            setAdded(asset.friendlyId)
            setAdding(undefined)
          }}
          onCancel={() => setAdding(undefined)}
        />
      )}
    </section>
  )
}
