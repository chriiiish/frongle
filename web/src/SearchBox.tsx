import { useState, type FormEvent } from 'react'
import { type Asset } from './api'
import { useApi } from './useApi'

/** Finds Assets by Friendly Id, including the Friendly Ids that they had before a retag. */
export function SearchBox({ onPick }: { onPick: (asset: Asset) => void }) {
  const api = useApi()
  const [text, setText] = useState('')
  const [query, setQuery] = useState('')
  const [results, setResults] = useState<Asset[]>()
  const [problem, setProblem] = useState<string>()

  async function search(event: FormEvent) {
    event.preventDefault()
    const wanted = text.trim()
    if (wanted === '') return
    setProblem(undefined)
    try {
      setResults(await api.searchAssets(wanted))
      setQuery(wanted)
    } catch (failure) {
      setProblem((failure as Error).message)
    }
  }

  // A crew may know the Asset by an old tag, so say which old tag matched.
  function formerMatch(asset: Asset) {
    if (asset.friendlyId.toLowerCase().includes(query.toLowerCase())) return undefined
    return asset.formerFriendlyIds.find((id) => id.toLowerCase().includes(query.toLowerCase()))
  }

  return (
    <div>
      <form className="input-group shadow-sm" role="search" onSubmit={search}>
        <input
          className="form-control"
          type="search"
          aria-label="Find an Asset"
          placeholder="Find an Asset"
          value={text}
          onChange={(event) => setText(event.target.value)}
        />
        <button className="btn btn-primary" type="submit">
          Search
        </button>
      </form>
      {problem && (
        <p className="alert alert-danger py-1 px-3 mt-1 mb-0" role="alert">
          {problem}
        </p>
      )}
      {results?.length === 0 && (
        <p className="alert alert-light py-1 px-3 mt-1 mb-0">No Asset matches.</p>
      )}
      {results && results.length > 0 && (
        <div className="list-group shadow-sm mt-1">
          {results.map((asset) => (
            <button
              key={asset.id}
              type="button"
              className="list-group-item list-group-item-action"
              onClick={() => {
                setResults(undefined)
                onPick(asset)
              }}
            >
              {asset.friendlyId}
              {formerMatch(asset) && (
                <small className="text-body-secondary ms-2">was {formerMatch(asset)}</small>
              )}
            </button>
          ))}
        </div>
      )}
    </div>
  )
}
