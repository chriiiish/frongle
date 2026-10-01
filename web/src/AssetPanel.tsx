import { useCallback, useEffect, useState } from 'react'
import {
  type Asset,
  type AssetEvent,
  type AssetStatus,
  type AssetType,
  type EventDraft,
  type EventImage,
} from './api'
import { EventForm } from './EventForm'
import { useApi } from './useApi'

const TYPE_LABEL: Record<AssetType, string> = {
  LightPost: 'Light-post',
  StreetSign: 'Street sign',
  TelephonePole: 'Telephone pole',
  TrafficLight: 'Traffic light',
}
const STATUS_LABEL: Record<AssetStatus, string> = {
  PendingInstallation: 'Pending installation',
  InService: 'In service',
  Removed: 'Removed',
}

const MAX_PHOTOS = 5

function newestFirst(events: AssetEvent[]) {
  return [...events].sort((a, b) => b.occurredAt.localeCompare(a.occurredAt))
}

/** What the user is doing in the panel: only reading, writing a new event, or correcting one. */
type Mode = { kind: 'reading' } | { kind: 'adding' } | { kind: 'editing'; event: AssetEvent }

/** Shows the type, status, and history of one Asset, and lets the user add events to it or correct them. */
export function AssetPanel({
  asset,
  onChanged,
  onClose,
}: {
  asset: Asset
  onChanged: (asset: Asset) => void
  onClose: () => void
}) {
  const api = useApi()
  const [events, setEvents] = useState<AssetEvent[]>()
  const [mode, setMode] = useState<Mode>({ kind: 'reading' })
  const [saving, setSaving] = useState(false)
  const [problem, setProblem] = useState<string>()

  const load = useCallback(
    () =>
      api.listEvents(asset.id).then(
        (found) => setEvents(newestFirst(found)),
        (failure: Error) => setProblem(failure.message),
      ),
    [api, asset.id],
  )
  useEffect(() => {
    void load()
  }, [load])

  async function save(draft: EventDraft, photos: File[]) {
    setSaving(true)
    setProblem(undefined)
    try {
      const saved =
        mode.kind === 'editing'
          ? await api.changeEvent(asset.id, mode.event, draft)
          : await api.addEvent(asset.id, draft)
      const photoProblem = await attach(saved.id, photos)
      await load()
      // The latest event decides the status, so the map needs the Asset again.
      onChanged(await api.getAsset(asset.id))
      setMode({ kind: 'reading' })
      setProblem(photoProblem)
    } catch (failure) {
      setProblem((failure as Error).message)
      // Someone else may have changed the event, and the next try needs its new version.
      await load()
    }
    setSaving(false)
  }

  // The event is saved by now, so a photo that fails leaves the event in place and the user can add the photo again.
  async function attach(eventId: string, photos: File[]) {
    for (const photo of photos) {
      try {
        await api.attachPhoto(asset.id, eventId, photo)
      } catch (failure) {
        return `${photo.name} could not be uploaded. ${(failure as Error).message}`
      }
    }
  }

  async function removePhoto(event: AssetEvent, image: EventImage) {
    try {
      await api.removePhoto(asset.id, event.id, image.id)
      await load()
    } catch (failure) {
      setProblem((failure as Error).message)
    }
  }

  return (
    <aside
      className="asset-panel position-absolute bottom-0 start-0 end-0 bg-body shadow p-3"
      aria-label="Asset"
    >
      <div className="d-flex align-items-start justify-content-between">
        <div>
          <h2 className="h5 mb-0">{asset.friendlyId}</h2>
          <p className="mb-2 text-body-secondary">
            {TYPE_LABEL[asset.type]}{' '}
            <span className="badge text-bg-secondary">{STATUS_LABEL[asset.status]}</span>
          </p>
        </div>
        <button type="button" className="btn-close" aria-label="Close" onClick={onClose} />
      </div>

      {mode.kind === 'reading' ? (
        <>
          <button
            type="button"
            className="btn btn-primary mb-3"
            onClick={() => setMode({ kind: 'adding' })}
          >
            Add event
          </button>
          {problem && (
            <p className="alert alert-danger" role="alert">
              {problem}
            </p>
          )}
          {events === undefined && !problem && <p role="status">Loading the history…</p>}
          {events?.length === 0 && (
            <p>No events yet. Add an Installed event when the Asset is in place.</p>
          )}
          <ul className="list-group list-group-flush">
            {events?.map((event) => (
              <li className="list-group-item px-0" key={event.id}>
                <div className="d-flex justify-content-between">
                  <strong>{event.title}</strong>
                  <button
                    type="button"
                    className="btn btn-sm btn-outline-dark"
                    onClick={() => setMode({ kind: 'editing', event })}
                  >
                    Edit
                  </button>
                </div>
                <div className="text-body-secondary small">
                  {event.type} ·{' '}
                  <time dateTime={event.occurredAt}>
                    {new Date(event.occurredAt).toLocaleString(undefined, {
                      dateStyle: 'medium',
                      timeStyle: 'short',
                    })}
                  </time>
                </div>
                {event.notes && <p className="mb-0 mt-1">{event.notes}</p>}
                {event.images.length > 0 && (
                  <div className="d-flex flex-wrap gap-2 mt-2">
                    {event.images.map((image) => (
                      <div className="position-relative" key={image.id}>
                        <a href={image.readUrl} target="_blank" rel="noreferrer">
                          <img
                            className="photo-thumb img-thumbnail"
                            src={image.readUrl}
                            alt={`Photo of ${event.title}`}
                          />
                        </a>
                        <button
                          type="button"
                          className="btn-close btn-sm position-absolute top-0 end-0 m-1 bg-body"
                          aria-label="Remove photo"
                          onClick={() => void removePhoto(event, image)}
                        />
                      </div>
                    ))}
                  </div>
                )}
              </li>
            ))}
          </ul>
        </>
      ) : (
        <EventForm
          initial={mode.kind === 'editing' ? mode.event : undefined}
          submitLabel={mode.kind === 'editing' ? 'Save Event' : 'Add Event'}
          saving={saving}
          problem={problem}
          photoSlots={MAX_PHOTOS - (mode.kind === 'editing' ? mode.event.images.length : 0)}
          onSubmit={save}
          onCancel={() => {
            setProblem(undefined)
            setMode({ kind: 'reading' })
          }}
        />
      )}
    </aside>
  )
}
