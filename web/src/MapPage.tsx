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
  ZoomControl,
} from 'react-leaflet'
import { type Area, type Asset, type AssetStatus, type Bounds } from './api'
import { AssetPanel } from './AssetPanel'
import { MoveAssetDialog } from './MoveAssetDialog'
import { NewAreaDialog } from './NewAreaDialog'
import { NewAssetDialog, type Place } from './NewAssetDialog'
import { SearchBox } from './SearchBox'
import { useAuth } from './auth/AuthContext'
import { useApi } from './useApi'

const AUCKLAND: [number, number] = [-36.8485, 174.7633]
const START_ZOOM = 12
// Below this zoom a screen covers too much ground to list every Asset.
const MIN_ASSET_ZOOM = 14
const MOVE_DELAY_MS = 250
const FIND_ZOOM = 17
const TILE_URL = 'https://tile.openstreetmap.org/{z}/{x}/{y}.png'
const TILE_CREDIT =
  '&copy; <a href="https://www.openstreetmap.org/copyright">OpenStreetMap</a> contributors'

const MANAGER_ROLE = 'maintenance-manager'
const MIN_CORNERS = 3
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

/** Moves the map to a place when the target changes, and zooms in far enough to show the Assets. */
function FlyTo({ target }: { target: { center: [number, number] } | undefined }) {
  const map = useMap()
  useEffect(() => {
    if (target) map.setView(target.center, FIND_ZOOM)
  }, [map, target])
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
  const [adding, setAdding] = useState<Place>()
  const [added, setAdded] = useState<string>()
  const [selectedId, setSelectedId] = useState<string>()
  // The corners that a manager has marked so far, or nothing when the manager is not drawing an Area.
  const [corners, setCorners] = useState<Place[]>()
  const [naming, setNaming] = useState(false)
  // The Asset that the user is moving, and the place that the user clicked for it.
  const [moving, setMoving] = useState<Asset>()
  const [moveTo, setMoveTo] = useState<Place>()
  const [flyTarget, setFlyTarget] = useState<{ center: [number, number] }>()
  const selected = assets.find((asset) => asset.id === selectedId)
  const [areasProblem, setAreasProblem] = useState<string>()
  const [assetsProblem, setAssetsProblem] = useState<string>()
  // Counts the Assets that the user added, so that a list request that began before one cannot answer without it.
  const additions = useRef(0)
  const areaAdditions = useRef(0)
  const zoomedIn = view !== undefined && view.zoom >= MIN_ASSET_ZOOM

  useEffect(() => {
    let current = true
    const addedBefore = areaAdditions.current
    api.listAreas().then(
      (listed) => {
        // An Area that the manager drew while this request was out is newer than this answer.
        if (!current || addedBefore !== areaAdditions.current) return
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

  function clickMap(place: Place) {
    if (corners) setCorners([...corners, place])
    else if (moving) setMoveTo(place)
    else setAdding(place)
  }

  function showFound(asset: Asset) {
    setAssets((current) => (current.some((a) => a.id === asset.id) ? current : [...current, asset]))
    setSelectedId(asset.id)
    setFlyTarget({ center: [asset.latitude, asset.longitude] })
  }

  return (
    <section className="container-fluid p-0 p-md-3 position-relative" aria-label="Map">
      <MapContainer
        className="map"
        center={AUCKLAND}
        zoom={START_ZOOM}
        zoomControl={false}
        maxBounds={WORLD_BOUNDS}
        maxBoundsViscosity={1}
      >
        <ZoomControl position="topright" />
        <TileLayer url={TILE_URL} attribution={TILE_CREDIT} />
        <ViewWatcher onChange={setView} />
        <ClickWatcher onClick={clickMap} />
        <FlyTo target={flyTarget} />
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
              eventHandlers={{
                click: (event) =>
                  corners ? setCorners([...corners, event.latlng]) : setSelectedId(asset.id),
              }}
              pathOptions={{ color: STATUS_COLOUR[asset.status], fillOpacity: 0.9 }}
            >
              <Tooltip>
                {asset.friendlyId} {STATUS_LABEL[asset.status]}
              </Tooltip>
            </CircleMarker>
          ))}
      </MapContainer>
      {/* The tools and the notices share one column, so they cannot cover each other. The zoom buttons sit at the right. */}
      <div
        data-testid="map-overlay"
        className="position-absolute top-0 start-0 end-0 p-2 p-md-3 pe-5 d-flex flex-column align-items-start gap-2 pe-none map-notice"
      >
        <div className="pe-auto col-12 col-md-5 col-lg-4">
          <SearchBox onPick={showFound} />
        </div>
        {moving ? (
          <div className="card card-body py-2 px-3 shadow-sm pe-auto">
            <p className="mb-2">Click the new place for {moving.friendlyId}.</p>
            <button
              type="button"
              className="btn btn-sm btn-outline-dark align-self-start"
              onClick={() => setMoving(undefined)}
            >
              Cancel
            </button>
          </div>
        ) : (
          roles.includes(MANAGER_ROLE) &&
          (corners ? (
            <div className="card card-body py-2 px-3 shadow-sm pe-auto">
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
              className="btn btn-primary shadow-sm pe-auto"
              onClick={() => setCorners([])}
            >
              Draw Area
            </button>
          ))
        )}
        {view && !zoomedIn && (
          <p className="alert alert-info py-1 px-3 mb-0 shadow-sm pe-auto" role="status">
            Zoom in to see Assets.
          </p>
        )}
        {added && (
          <p className="alert alert-success py-1 px-3 mb-0 shadow-sm pe-auto" role="status">
            Added {added}.
          </p>
        )}
        {Object.entries({ areas: areasProblem, assets: assetsProblem }).map(
          ([source, problem]) =>
            problem && (
              <p
                key={source}
                className="alert alert-danger py-1 px-3 mb-0 shadow-sm pe-auto"
                role="alert"
              >
                {problem}
              </p>
            ),
        )}
      </div>
      {selected && (
        <AssetPanel
          key={selected.id}
          asset={selected}
          onChanged={(changed) =>
            setAssets((current) => current.map((a) => (a.id === changed.id ? changed : a)))
          }
          onClose={() => setSelectedId(undefined)}
          onMove={() => {
            setMoving(selected)
            setSelectedId(undefined)
          }}
        />
      )}
      {corners && naming && (
        <NewAreaDialog
          corners={corners}
          onCreated={(area) => {
            areaAdditions.current += 1
            setAreas((current) => [...current, area])
            setCorners(undefined)
            setNaming(false)
          }}
          onCancel={() => setNaming(false)}
        />
      )}
      {moving && moveTo && (
        <MoveAssetDialog
          asset={moving}
          location={moveTo}
          onMoved={(moved) => {
            setAssets((current) => current.map((a) => (a.id === moved.id ? moved : a)))
            setSelectedId(moved.id)
            setMoving(undefined)
            setMoveTo(undefined)
          }}
          onCancel={() => setMoveTo(undefined)}
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
