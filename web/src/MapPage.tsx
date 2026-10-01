import 'leaflet/dist/leaflet.css'
import { useCallback, useEffect, useState } from 'react'
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
import { NewAreaDialog } from './NewAreaDialog'
import { NewAssetDialog, type Place } from './NewAssetDialog'
import { useAuth } from './auth/AuthContext'
import { useApi } from './useApi'

const AUCKLAND: [number, number] = [-36.8485, 174.7633]
const START_ZOOM = 12
// Below this zoom a screen covers too much ground to list every Asset.
const MIN_ASSET_ZOOM = 14
const MOVE_DELAY_MS = 250
const TILE_URL = 'https://tile.openstreetmap.org/{z}/{x}/{y}.png'
const TILE_CREDIT =
  '&copy; <a href="https://www.openstreetmap.org/copyright">OpenStreetMap</a> contributors'

const MANAGER_ROLE = 'maintenance-manager'
const MIN_CORNERS = 3

const STATUS_COLOUR: Record<AssetStatus, string> = {
  PendingInstallation: '#f0ad4e',
  InService: '#198754',
  Removed: '#6c757d',
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
  const { roles } = useAuth()
  const [areas, setAreas] = useState<Area[]>([])
  const [assets, setAssets] = useState<Asset[]>([])
  const [view, setView] = useState<MapView>()
  const [problem, setProblem] = useState<string>()
  const [adding, setAdding] = useState<Place>()
  const [added, setAdded] = useState<string>()
  const [selectedId, setSelectedId] = useState<string>()
  // The corners that a manager has marked so far, or nothing when the manager is not drawing an Area.
  const [corners, setCorners] = useState<Place[]>()
  const [naming, setNaming] = useState(false)
  const selected = assets.find((asset) => asset.id === selectedId)
  const zoomedIn = view !== undefined && view.zoom >= MIN_ASSET_ZOOM

  useEffect(() => {
    api.listAreas().then(setAreas, (failure: Error) => setProblem(failure.message))
  }, [api])

  useEffect(() => {
    if (!view || !zoomedIn) return
    const timer = setTimeout(
      () =>
        api
          .listAssets(view.bounds)
          .then(setAssets, (failure: Error) => setProblem(failure.message)),
      MOVE_DELAY_MS,
    )
    return () => clearTimeout(timer)
  }, [api, view, zoomedIn])

  return (
    <section className="container-fluid p-0 p-md-3 position-relative" aria-label="Map">
      <MapContainer className="map" center={AUCKLAND} zoom={START_ZOOM}>
        <TileLayer url={TILE_URL} attribution={TILE_CREDIT} />
        <ViewWatcher onChange={setView} />
        <ClickWatcher
          onClick={(place) => (corners ? setCorners([...corners, place]) : setAdding(place))}
        />
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
        {corners && corners.length >= 2 && (
          <Polygon
            positions={corners.map(({ lat, lng }): [number, number] => [lat, lng])}
            interactive={false}
            pathOptions={{ color: '#ea4e2d', weight: 3, dashArray: '6' }}
          />
        )}
        {corners?.map(({ lat, lng }, index) => (
          <CircleMarker
            key={index}
            center={[lat, lng]}
            radius={5}
            interactive={false}
            pathOptions={{ color: '#ea4e2d', className: 'corner' }}
          />
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
              <Tooltip>{asset.friendlyId}</Tooltip>
            </CircleMarker>
          ))}
      </MapContainer>
      {roles.includes(MANAGER_ROLE) && (
        <div className="position-absolute top-0 start-0 mt-3 mt-md-5 ms-3 ms-md-5 map-notice">
          {corners ? (
            <div className="card card-body py-2 px-3 shadow-sm">
              <p className="mb-2">Click the map to mark each corner of the Area.</p>
              <div className="d-flex gap-2">
                <button
                  type="button"
                  className="btn btn-sm btn-outline-dark"
                  disabled={corners.length === 0}
                  onClick={() => setCorners(corners.slice(0, -1))}
                >
                  Undo
                </button>
                <button
                  type="button"
                  className="btn btn-sm btn-outline-dark"
                  onClick={() => setCorners(undefined)}
                >
                  Cancel
                </button>
                <button
                  type="button"
                  className="btn btn-sm btn-primary"
                  disabled={corners.length < MIN_CORNERS}
                  onClick={() => setNaming(true)}
                >
                  Finish
                </button>
              </div>
            </div>
          ) : (
            <button
              type="button"
              className="btn btn-primary shadow-sm"
              onClick={() => setCorners([])}
            >
              Draw Area
            </button>
          )}
        </div>
      )}
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
        {problem && (
          <p className="alert alert-danger py-1 px-3 shadow-sm" role="alert">
            {problem}
          </p>
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
      {corners && naming && (
        <NewAreaDialog
          corners={corners}
          onCreated={(area) => {
            setAreas((current) => [...current, area])
            setCorners(undefined)
            setNaming(false)
          }}
          onCancel={() => setNaming(false)}
        />
      )}
      {adding && (
        <NewAssetDialog
          location={adding}
          onCreated={(asset) => {
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
