import { useEffect, useState } from 'react'

export interface Me {
  name: string
  tenant: string
}

/** Asks the API who the signed-in user is. The caller gets the user, or an error message when the API fails. */
export function useMe(token: string | undefined) {
  const [me, setMe] = useState<Me>()
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

  return { me, error }
}
