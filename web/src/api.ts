export type AssetStatus = 'PendingInstallation' | 'InService' | 'Removed'

/** A polygon as GeoJSON: rings of [longitude, latitude] positions. */
export interface Boundary {
  type: 'Polygon'
  coordinates: number[][][]
}

export interface Area {
  id: string
  code: string
  name: string
  boundary: Boundary
}

export interface Asset {
  id: string
  friendlyId: string
  type: AssetType
  areaCode: string
  latitude: number
  longitude: number
  status: AssetStatus
}

export type EventType = 'Installed' | 'Checked' | 'Repaired' | 'Maintained' | 'Removed'

/** A photo of an event. The read link is short-lived. */
export interface EventImage {
  id: string
  contentType: string
  sizeBytes: number
  readUrl: string
}

/** One thing that happened to an Asset. */
export interface AssetEvent {
  id: string
  assetId: string
  type: EventType
  title: string
  notes: string | null
  /** When it happened, as a UTC instant. */
  occurredAt: string
  /** The version to send back when the user corrects the event. */
  version: number
  images: EventImage[]
}

/** What the user writes about an event. */
export interface EventDraft {
  type: EventType
  title: string
  notes: string | null
  occurredAt: string
}

/** A box on the map, in degrees. */
export interface Bounds {
  west: number
  south: number
  east: number
  north: number
}

export type AssetType = 'LightPost' | 'StreetSign' | 'TelephonePole' | 'TrafficLight'

/** The API refused a request, or could not be reached. The message says why, in words that the user can read. */
export class ApiError extends Error {}

async function reasonFor(response: Response): Promise<string> {
  const body = (await response.json().catch(() => null)) ?? {}
  const messages: string[] = Object.values(body.errors ?? {}).flat() as string[]
  // A validation problem has a generic title and the messages that say what to fix, so the messages come first.
  return messages.join(' ') || body.title || `The API returned status ${response.status}.`
}

async function call<T>(
  token: string | undefined,
  path: string,
  send?: { method: 'POST' | 'PUT' | 'DELETE'; body?: object },
): Promise<T> {
  let response: Response
  try {
    response = await fetch(
      path,
      send === undefined
        ? { headers: { Authorization: `Bearer ${token}` } }
        : send.body === undefined
          ? { method: send.method, headers: { Authorization: `Bearer ${token}` } }
          : {
              method: send.method,
              headers: { Authorization: `Bearer ${token}`, 'Content-Type': 'application/json' },
              body: JSON.stringify(send.body),
            },
    )
  } catch (failure) {
    throw new ApiError(`The API could not be reached: ${(failure as Error).message}`)
  }
  if (!response.ok) throw new ApiError(await reasonFor(response))
  return response.status === 204 ? (undefined as T) : response.json()
}

/** The calls that the web app makes to the Frongle API, signed with the token of the user. */
export function createApi(token: string | undefined) {
  return {
    listAreas: () => call<Area[]>(token, '/api/areas'),
    listAssets: ({ west, south, east, north }: Bounds) =>
      call<Asset[]>(token, `/api/assets?west=${west}&south=${south}&east=${east}&north=${north}`),
    createArea: (code: string, name: string, boundary: Boundary) =>
      call<Area>(token, '/api/areas', { method: 'POST', body: { code, name, boundary } }),
    /** Asks the API for a place to put the photo, then sends the photo straight to storage. */
    attachPhoto: async (assetId: string, eventId: string, photo: File) => {
      const upload = await call<{ uploadUrl: string; contentType: string }>(
        token,
        `/api/assets/${assetId}/events/${eventId}/images`,
        { method: 'POST', body: { contentType: photo.type, sizeBytes: photo.size } },
      )
      let response: Response
      try {
        // The link is signed already, so this call must not carry the token.
        response = await fetch(upload.uploadUrl, {
          method: 'PUT',
          headers: { 'Content-Type': upload.contentType },
          body: photo,
        })
      } catch (failure) {
        throw new ApiError(`The storage server could not be reached: ${(failure as Error).message}`)
      }
      if (!response.ok) throw new ApiError(`The storage server returned status ${response.status}.`)
    },
    removePhoto: (assetId: string, eventId: string, imageId: string) =>
      call<void>(token, `/api/assets/${assetId}/events/${eventId}/images/${imageId}`, {
        method: 'DELETE',
      }),
    getAsset: (id: string) => call<Asset>(token, `/api/assets/${id}`),
    listEvents: (assetId: string) => call<AssetEvent[]>(token, `/api/assets/${assetId}/events`),
    addEvent: (assetId: string, draft: EventDraft) =>
      call<AssetEvent>(token, `/api/assets/${assetId}/events`, { method: 'POST', body: draft }),
    changeEvent: (assetId: string, event: AssetEvent, draft: EventDraft) =>
      call<AssetEvent>(token, `/api/assets/${assetId}/events/${event.id}`, {
        method: 'PUT',
        body: { ...draft, version: event.version },
      }),
    createAsset: (type: AssetType, latitude: number, longitude: number) =>
      call<Asset>(token, '/api/assets', { method: 'POST', body: { type, latitude, longitude } }),
  }
}
