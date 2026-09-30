import { useEffect, useState } from 'react'

interface HelloResponse {
  greeting: string
  tenantId: string
  roles: string[]
}

export function Hello({ token }: { token: string | undefined }) {
  const [hello, setHello] = useState<HelloResponse>()
  const [error, setError] = useState<string>()

  useEffect(() => {
    async function load() {
      const response = await fetch('/api/hello', { headers: { Authorization: `Bearer ${token}` } })
      if (!response.ok) {
        setError(`The API returned status ${response.status}.`)
        return
      }
      setHello(await response.json())
    }
    void load()
  }, [token])

  if (error) return <p role="alert">{error}</p>
  if (!hello) return <p>Loading…</p>
  return (
    <>
      <p>{hello.greeting}</p>
      <p>
        Tenant: {hello.tenantId}. Roles: {hello.roles.join(', ')}.
      </p>
    </>
  )
}
