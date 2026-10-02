import { useState, type FormEvent } from 'react'
import { type Area } from './api'
import { Field } from './Field'
import { Modal } from './Modal'
import { type Place } from './NewAssetDialog'
import { useApi } from './useApi'

/** Asks for the code and name of an Area that the manager just drew, then adds it through the API. */
export function NewAreaDialog({
  corners,
  onCreated,
  onCancel,
}: {
  corners: Place[]
  onCreated: (area: Area) => void
  onCancel: () => void
}) {
  const api = useApi()
  const [code, setCode] = useState('')
  const [name, setName] = useState('')
  const [saving, setSaving] = useState(false)
  const [problem, setProblem] = useState<string>()

  async function add(event: FormEvent) {
    event.preventDefault()
    setSaving(true)
    setProblem(undefined)
    // GeoJSON lists longitude first, and a ring ends where it began.
    const ring = [...corners, corners[0]].map(({ lat, lng }) => [lng, lat])
    try {
      onCreated(await api.createArea(code, name.trim(), { type: 'Polygon', coordinates: [ring] }))
    } catch (failure) {
      setProblem((failure as Error).message)
      setSaving(false)
    }
  }

  return (
    <Modal
      title="Add an Area"
      onClose={onCancel}
      closeDisabled={saving}
      onSubmit={add}
      footer={
        <>
          <button
            type="button"
            className="btn btn-outline-dark"
            disabled={saving}
            onClick={onCancel}
          >
            Cancel
          </button>
          <button
            type="submit"
            className="btn btn-primary"
            disabled={saving || code.length !== 2 || name.trim() === ''}
          >
            Add Area
          </button>
        </>
      }
    >
      <Field
        label="Code"
        value={code}
        onChange={(value) =>
          setCode(
            value
              .replace(/[^a-z]/gi, '')
              .toUpperCase()
              .slice(0, 2),
          )
        }
      />
      <Field label="Name" value={name} onChange={setName} />
      <p className="text-body-secondary small mb-0">
        The code is two letters. It starts the Friendly Id of every Asset in the Area, and it cannot
        change later.
      </p>
      {problem && (
        <p className="alert alert-danger mt-3 mb-0" role="alert">
          {problem}
        </p>
      )}
    </Modal>
  )
}
