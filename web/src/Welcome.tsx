import { useEffect, useState } from 'react'

interface MeResponse {
  name: string
}

/** The start page: it welcomes the signed-in user by the name that the API reads from their token. */
export function Welcome({ token }: { token: string | undefined }) {
  const [me, setMe] = useState<MeResponse>()
  const [error, setError] = useState<string>()

  useEffect(() => {
    async function load() {
      const response = await fetch('/api/me', { headers: { Authorization: `Bearer ${token}` } })
      if (!response.ok) {
        setError(`The API returned status ${response.status}.`)
        return
      }
      setMe(await response.json())
    }
    load().catch((failure: Error) => setError(`The API could not be reached: ${failure.message}`))
  }, [token])

  if (error) return <p role="alert">{error}</p>
  if (!me) return <p>Loading…</p>
  return <h2>Welcome, {me.name}</h2>
}
