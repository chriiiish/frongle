import { useState, type FormEvent } from 'react'
import { type Asset, type AssetType } from './api'
import { useApi } from './useApi'
import { useModalFocus } from './useModalFocus'

const ASSET_TYPES: { value: AssetType; label: string }[] = [
  { value: 'LightPost', label: 'Light-post' },
  { value: 'StreetSign', label: 'Street sign' },
  { value: 'TelephonePole', label: 'Telephone pole' },
  { value: 'TrafficLight', label: 'Traffic light' },
]

/** A place on the map, in degrees. */
export interface Place {
  lat: number
  lng: number
}

/** Asks which type of Asset stands at the place that the user clicked, then adds it through the API. */
export function NewAssetDialog({
  location,
  onCreated,
  onCancel,
}: {
  location: Place
  onCreated: (asset: Asset) => void
  onCancel: () => void
}) {
  const api = useApi()
  const [type, setType] = useState<AssetType>('LightPost')
  const [saving, setSaving] = useState(false)
  const [problem, setProblem] = useState<string>()
  const dialog = useModalFocus<HTMLDivElement>(onCancel)

  async function add(event: FormEvent) {
    event.preventDefault()
    setSaving(true)
    setProblem(undefined)
    try {
      onCreated(await api.createAsset(type, location.lat, location.lng))
    } catch (failure) {
      setProblem((failure as Error).message)
      setSaving(false)
    }
  }

  return (
    <>
      <div
        ref={dialog}
        tabIndex={-1}
        className="modal d-block"
        role="dialog"
        aria-modal="true"
        aria-labelledby="new-asset-title"
      >
        <div className="modal-dialog modal-dialog-centered">
          <form className="modal-content" onSubmit={add}>
            <div className="modal-header">
              <h2 className="modal-title fs-5" id="new-asset-title">
                Add an Asset
              </h2>
              <button type="button" className="btn-close" aria-label="Close" onClick={onCancel} />
            </div>
            <div className="modal-body">
              <p className="text-body-secondary">
                {location.lat.toFixed(5)}, {location.lng.toFixed(5)}
              </p>
              <label className="form-label" htmlFor="asset-type">
                Type
              </label>
              <select
                className="form-select"
                id="asset-type"
                value={type}
                onChange={(event) => setType(event.target.value as AssetType)}
              >
                {ASSET_TYPES.map(({ value, label }) => (
                  <option key={value} value={value}>
                    {label}
                  </option>
                ))}
              </select>
              {problem && (
                <p className="alert alert-danger mt-3 mb-0" role="alert">
                  {problem}
                </p>
              )}
            </div>
            <div className="modal-footer">
              <button type="button" className="btn btn-outline-dark" onClick={onCancel}>
                Cancel
              </button>
              <button type="submit" className="btn btn-primary" disabled={saving}>
                Add Asset
              </button>
            </div>
          </form>
        </div>
      </div>
      <div className="modal-backdrop show" />
    </>
  )
}
