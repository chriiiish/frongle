import { useEffect, useState } from 'react'
import { type HistoryChange } from './api'
import { useApi } from './useApi'

const WHAT: Record<HistoryChange['entityType'], string> = {
  Asset: 'Asset',
  AssetEvent: 'Event',
  EventImage: 'Photo',
}

function describe(change: HistoryChange) {
  const what = `${WHAT[change.entityType]} ${change.field}`
  if (change.operation === 'Created') return `${what} set to ${change.newValue}`
  if (change.operation === 'Deleted') return `${what} deleted, it was ${change.oldValue}`
  return `${what} changed from ${change.oldValue ?? 'nothing'} to ${change.newValue ?? 'nothing'}`
}

/** Lists who changed the Asset, its Events, and their photos, what they changed, and when. Newest first. */
export function HistoryList({ assetId }: { assetId: string }) {
  const api = useApi()
  const [changes, setChanges] = useState<HistoryChange[]>()
  const [problem, setProblem] = useState<string>()

  useEffect(() => {
    api.listHistory(assetId).then(setChanges, (failure: Error) => setProblem(failure.message))
  }, [api, assetId])

  if (problem)
    return (
      <p className="alert alert-danger" role="alert">
        {problem}
      </p>
    )
  if (!changes) return <p role="status">Loading the changes…</p>
  return (
    <ul className="list-group list-group-flush">
      {changes.map((change, index) => (
        <li className="list-group-item px-0" key={index}>
          <div>{describe(change)}</div>
          <div className="text-body-secondary small">
            {change.changedByName ?? change.changedBy} ·{' '}
            <time dateTime={change.changedAt}>
              {new Date(change.changedAt).toLocaleString(undefined, {
                dateStyle: 'medium',
                timeStyle: 'short',
              })}
            </time>
          </div>
        </li>
      ))}
    </ul>
  )
}
