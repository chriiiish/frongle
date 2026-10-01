import { useState, type FormEvent } from 'react'
import { type EventDraft, type EventType } from './api'

const EVENT_TYPES: EventType[] = ['Installed', 'Checked', 'Repaired', 'Maintained', 'Removed']

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
  onSubmit,
  onCancel,
}: {
  initial?: EventDraft
  submitLabel: string
  saving: boolean
  problem?: string
  onSubmit: (draft: EventDraft) => void
  onCancel: () => void
}) {
  const [type, setType] = useState<EventType>(initial?.type ?? 'Checked')
  const [title, setTitle] = useState(initial?.title ?? '')
  const [notes, setNotes] = useState(initial?.notes ?? '')
  const [when, setWhen] = useState(() =>
    toLocalInput(initial ? new Date(initial.occurredAt) : new Date()),
  )

  function submit(event: FormEvent) {
    event.preventDefault()
    onSubmit({
      type,
      title: title.trim(),
      notes: notes.trim() === '' ? null : notes,
      occurredAt: new Date(when).toISOString(),
    })
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
          value={when}
          onChange={(event) => setWhen(event.target.value)}
        />
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
