import 'leaflet/dist/leaflet.css'
import { MapContainer, TileLayer } from 'react-leaflet'

const AUCKLAND: [number, number] = [-36.8485, 174.7633]
const START_ZOOM = 12
const TILE_URL = 'https://tile.openstreetmap.org/{z}/{x}/{y}.png'
const TILE_CREDIT =
  '&copy; <a href="https://www.openstreetmap.org/copyright">OpenStreetMap</a> contributors'

/** The map page: a street map that opens on Auckland, New Zealand. */
export function MapPage() {
  return (
    <section className="container-fluid p-0 p-md-3" aria-label="Map">
      <MapContainer className="map" center={AUCKLAND} zoom={START_ZOOM}>
        <TileLayer url={TILE_URL} attribution={TILE_CREDIT} />
      </MapContainer>
    </section>
  )
}
