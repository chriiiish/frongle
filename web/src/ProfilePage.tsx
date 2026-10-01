import { useEffect, useState, type FormEvent } from 'react'
import { postToKeycloak } from './accountApi'
import { useAuth } from './auth/AuthContext'
import { Field } from './Field'
import { PasswordCard } from './PasswordCard'

interface Details {
  firstName: string
  lastName: string
  email: string
}

/** The profile page: the user reads and changes their own details, which Keycloak stores. */
export function ProfilePage() {
  const { token, accountUrl, refresh, profile } = useAuth()
  const [details, setDetails] = useState<Details | undefined>(profile)
  const [saved, setSaved] = useState(false)
  const [error, setError] = useState<string>()
  const [loadError, setLoadError] = useState<string>()

  useEffect(() => {
    async function load() {
      const response = await fetch(accountUrl!, {
        headers: { Authorization: `Bearer ${token}`, Accept: 'application/json' },
      })
      if (!response.ok) {
        setLoadError(`Keycloak returned status ${response.status}.`)
        return
      }
      const { firstName, lastName, email } = await response.json()
      setDetails({ firstName, lastName, email })
    }
    load().catch((failure: Error) =>
      setLoadError(`Keycloak could not be reached: ${failure.message}`),
    )
  }, [token, accountUrl])

  async function saveDetails(event: FormEvent) {
    event.preventDefault()
    const failure = await postToKeycloak(accountUrl!, token, details!)
    setError(failure)
    setSaved(!failure)
    if (!failure) await refresh()
  }

  if (!details) {
    return (
      <p className="container-fluid py-3 px-md-4" role={loadError ? 'alert' : undefined}>
        {loadError ?? 'Loading…'}
      </p>
    )
  }
  return (
    <div className="container-fluid py-3 px-md-4">
      <h2 className="display-6 mb-4">Profile</h2>
      {loadError && (
        <p className="text-danger" role="alert">
          {loadError}
        </p>
      )}
      <div className="row g-4">
        <section className="col-lg-6">
          <form className="card card-body" aria-label="Details" onSubmit={saveDetails}>
            <h3 className="h5 mb-3">Details</h3>
            <Field
              label="First name"
              autoComplete="given-name"
              value={details.firstName}
              onChange={(firstName) => setDetails({ ...details, firstName })}
            />
            <Field
              label="Last name"
              autoComplete="family-name"
              value={details.lastName}
              onChange={(lastName) => setDetails({ ...details, lastName })}
            />
            <Field
              label="Email"
              type="email"
              autoComplete="email"
              value={details.email}
              onChange={(email) => setDetails({ ...details, email })}
            />
            <button className="btn btn-primary align-self-start" type="submit">
              Save details
            </button>
            {saved && (
              <p className="mt-3 mb-0" role="status">
                Your details are saved.
              </p>
            )}
            {error && (
              <p className="mt-3 mb-0 text-danger" role="alert">
                {error}
              </p>
            )}
          </form>
        </section>
        <section className="col-lg-6">
          <PasswordCard />
        </section>
      </div>
    </div>
  )
}
