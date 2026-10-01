import { useState, type FormEvent } from 'react'
import { postToKeycloak } from './accountApi'
import { useAuth } from './auth/AuthContext'
import { Field } from './Field'

const EMPTY = { currentPassword: '', newPassword: '', confirmation: '' }

/** The form that changes the user's password in Keycloak. Keycloak checks the current password and the password rules. */
export function PasswordForm() {
  const { token, accountUrl } = useAuth()
  const [passwords, setPasswords] = useState(EMPTY)
  const [changed, setChanged] = useState(false)
  const [error, setError] = useState<string>()

  async function changePassword(event: FormEvent) {
    event.preventDefault()
    const failure = await postToKeycloak(`${accountUrl}/credentials/password`, token, passwords)
    setError(failure)
    setChanged(!failure)
    if (!failure) setPasswords(EMPTY)
  }

  return (
    <form className="card card-body" aria-label="Password" onSubmit={changePassword}>
      <h3 className="h5 mb-3">Password</h3>
      <Field
        label="Current password"
        type="password"
        autoComplete="current-password"
        value={passwords.currentPassword}
        onChange={(currentPassword) => setPasswords({ ...passwords, currentPassword })}
      />
      <Field
        label="New password"
        type="password"
        autoComplete="new-password"
        value={passwords.newPassword}
        onChange={(newPassword) => setPasswords({ ...passwords, newPassword })}
      />
      <Field
        label="Confirm new password"
        type="password"
        autoComplete="new-password"
        value={passwords.confirmation}
        onChange={(confirmation) => setPasswords({ ...passwords, confirmation })}
      />
      <button className="btn btn-primary align-self-start" type="submit">
        Change password
      </button>
      {changed && (
        <p className="mt-3 mb-0" role="status">
          Your password is changed.
        </p>
      )}
      {error && (
        <p className="mt-3 mb-0 text-danger" role="alert">
          {error}
        </p>
      )}
    </form>
  )
}
