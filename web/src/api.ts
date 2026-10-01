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
  const body = await response.json().catch(() => ({}))
  const messages: string[] = Object.values(body.errors ?? {}).flat() as string[]
  return body.title ?? (messages.join(' ') || `The API returned status ${response.status}.`)
}

async function call<T>(token: string | undefined, path: string, body?: object): Promise<T> {
  let response: Response
  try {
    response = await fetch(
      path,
      body === undefined
        ? { headers: { Authorization: `Bearer ${token}` } }
        : {
            method: 'POST',
            headers: { Authorization: `Bearer ${token}`, 'Content-Type': 'application/json' },
            body: JSON.stringify(body),
          },
    )
  } catch (failure) {
    throw new ApiError(`The API could not be reached: ${(failure as Error).message}`)
  }
  if (!response.ok) throw new ApiError(await reasonFor(response))
  return response.json()
}

/** The calls that the web app makes to the Frongle API, signed with the token of the user. */
export function createApi(token: string | undefined) {
  return {
    listAreas: () => call<Area[]>(token, '/api/areas'),
    listAssets: ({ west, south, east, north }: Bounds) =>
      call<Asset[]>(token, `/api/assets?west=${west}&south=${south}&east=${east}&north=${north}`),
    createAsset: (type: AssetType, latitude: number, longitude: number) =>
      call<Asset>(token, '/api/assets', { type, latitude, longitude }),
  }
}
