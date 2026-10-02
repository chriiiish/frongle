import { useState, type ChangeEvent, type FormEvent } from 'react'
import { type EventDraft, type EventType } from './api'

const EVENT_TYPES: EventType[] = ['Installed', 'Checked', 'Repaired', 'Maintained', 'Removed']

const PHOTO_TYPES = ['image/jpeg', 'image/png', 'image/webp']
const MAX_PHOTO_BYTES = 10 * 1024 * 1024
const MAX_PHOTOS = 5

function pad(number: number) {
  return String(number).padStart(2, '0')
}

/** Formats an instant the way that a datetime-local input wants it: local time, to the minute. */
function toLocalInput(instant: Date) {
  const date = `${instant.getFullYear()}-${pad(instant.getMonth() + 1)}-${pad(instant.getDate())}`
  return `${date}T${pad(instant.getHours())}:${pad(instant.getMinutes())}`
}

/** A form to write an Event, or to correct one. A new Event starts as a Check that happened just now. */
export function EventForm({
  initial,
  submitLabel,
  saving,
  problem,
  photoSlots,
  onSubmit,
  onCancel,
}: {
  initial?: EventDraft
  submitLabel: string
  saving: boolean
  problem?: string
  /** How many more photos the event can hold. */
  photoSlots: number
  onSubmit: (draft: EventDraft, photos: File[]) => void
  onCancel: () => void
}) {
  const [type, setType] = useState<EventType>(initial?.type ?? 'Checked')
  const [title, setTitle] = useState(initial?.title ?? '')
  const [notes, setNotes] = useState(initial?.notes ?? '')
  const [photos, setPhotos] = useState<File[]>([])
  const [photoProblem, setPhotoProblem] = useState<string>()
  const [when, setWhen] = useState(() =>
    toLocalInput(initial ? new Date(initial.occurredAt) : new Date()),
  )

  function choosePhotos(event: ChangeEvent<HTMLInputElement>) {
    const chosen = Array.from(event.target.files ?? [])
    event.target.value = ''
    const usable = chosen.filter(
      (file) => PHOTO_TYPES.includes(file.type) && file.size <= MAX_PHOTO_BYTES,
    )
    const room = photoSlots - photos.length
    setPhotos([...photos, ...usable.slice(0, room)])
    if (usable.length < chosen.length) {
      setPhotoProblem('Only JPEG, PNG, and WebP photos up to 10 MB are accepted.')
    } else if (usable.length > room) {
      setPhotoProblem(`An event holds ${MAX_PHOTOS} photos at most.`)
    } else {
      setPhotoProblem(undefined)
    }
  }

  function submit(event: FormEvent) {
    event.preventDefault()
    onSubmit(
      {
        type,
        title: title.trim(),
        notes: notes.trim() === '' ? null : notes,
        occurredAt: new Date(when).toISOString(),
      },
      photos,
    )
  }

  return (
    <form onSubmit={submit}>
      <div className="mb-3">
        <label className="form-label" htmlFor="event-type">
          Type
        </label>
        <select
          className="form-select"
          id="event-type"
          value={type}
          onChange={(event) => setType(event.target.value as EventType)}
        >
          {EVENT_TYPES.map((value) => (
            <option key={value}>{value}</option>
          ))}
        </select>
      </div>
      <div className="mb-3">
        <label className="form-label" htmlFor="event-title">
          Title
        </label>
        <input
          className="form-control"
          id="event-title"
          value={title}
          onChange={(event) => setTitle(event.target.value)}
        />
      </div>
      <div className="mb-3">
        <label className="form-label" htmlFor="event-notes">
          Notes
        </label>
        <textarea
          className="form-control"
          id="event-notes"
          rows={3}
          value={notes}
          onChange={(event) => setNotes(event.target.value)}
        />
      </div>
      <div className="mb-3">
        <label className="form-label" htmlFor="event-when">
          When
        </label>
        <input
          className="form-control"
          id="event-when"
          type="datetime-local"
          required
          value={when}
          onChange={(event) => setWhen(event.target.value)}
        />
      </div>
      <div className="mb-3">
        <label className="form-label" htmlFor="event-photos">
          Photos
        </label>
        <input
          className="form-control"
          id="event-photos"
          type="file"
          accept="image/*"
          multiple
          disabled={photos.length >= photoSlots}
          onChange={choosePhotos}
        />
        <ul className="list-unstyled mt-2 mb-0">
          {photos.map((photo, index) => (
            <li key={index} className="d-flex align-items-center gap-2">
              <span className="text-truncate">{photo.name}</span>
              <button
                type="button"
                className="btn-close btn-sm"
                aria-label={`Remove ${photo.name}`}
                onClick={() => setPhotos(photos.filter((_, other) => other !== index))}
              />
            </li>
          ))}
        </ul>
        {photoProblem && (
          <p className="alert alert-warning mt-2 mb-0" role="alert">
            {photoProblem}
          </p>
        )}
      </div>
      {problem && (
        <p className="alert alert-danger" role="alert">
          {problem}
        </p>
      )}
      <div className="d-flex gap-2 justify-content-end">
        <button type="button" className="btn btn-outline-dark" onClick={onCancel}>
          Cancel
        </button>
        <button type="submit" className="btn btn-primary" disabled={saving || title.trim() === ''}>
          {submitLabel}
        </button>
      </div>
    </form>
  )
}
