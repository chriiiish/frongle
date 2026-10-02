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
    // Without a token the API can only answer 401, and that error would outlive the sign-in.
    if (!token) return
    // A refreshed token starts a second request. The first must not overwrite it if it answers last.
    let current = true
    async function load() {
      setError(undefined)
      const response = await fetch('/api/me', { headers: { Authorization: `Bearer ${token}` } })
      if (!response.ok) {
        if (current) setError(`The API returned status ${response.status}.`)
        return
      }
      const found = await response.json()
      if (current) setMe(found)
    }
    load().catch(
      (failure: Error) => current && setError(`The API could not be reached: ${failure.message}`),
    )
    return () => {
      current = false
    }
  }, [token])

  return { me, error }
}
