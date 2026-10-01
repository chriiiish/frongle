import { useState, type FormEvent } from 'react'
import { type Asset } from './api'
import { Modal } from './Modal'
import { type Place } from './NewAssetDialog'
import { useApi } from './useApi'

/** Asks the user to confirm a move, because a move into another Area gives the Asset a new Friendly Id. */
export function MoveAssetDialog({
  asset,
  location,
  onMoved,
  onCancel,
}: {
  asset: Asset
  location: Place
  onMoved: (asset: Asset) => void
  onCancel: () => void
}) {
  const api = useApi()
  const [saving, setSaving] = useState(false)
  const [problem, setProblem] = useState<string>()

  async function move(event: FormEvent) {
    event.preventDefault()
    setSaving(true)
    setProblem(undefined)
    try {
      onMoved(await api.moveAsset(asset, location.lat, location.lng))
    } catch (failure) {
      setProblem((failure as Error).message)
      setSaving(false)
    }
  }

  return (
    <Modal
      title={`Move ${asset.friendlyId}`}
      onClose={onCancel}
      onSubmit={move}
      footer={
        <>
          <button type="button" className="btn btn-outline-dark" onClick={onCancel}>
            Cancel
          </button>
          <button type="submit" className="btn btn-primary" disabled={saving}>
            Move Asset
          </button>
        </>
      }
    >
      <p className="text-body-secondary">
        {location.lat.toFixed(5)}, {location.lng.toFixed(5)}
      </p>
      <p className="mb-0">
        If the new place is in another Area, the Asset gets a new Friendly Id and someone must fit
        the new tag.
      </p>
      {problem && (
        <p className="alert alert-danger mt-3 mb-0" role="alert">
          {problem}
        </p>
      )}
    </Modal>
  )
}
