import { useAuth } from './auth/AuthContext'

/** The password card: the button sends the user to the Keycloak page that changes the password. */
export function PasswordCard() {
  const { changePassword } = useAuth()

  return (
    <div className="card card-body">
      <h3 className="h5 mb-3">Password</h3>
      <p>You choose a new password on a Keycloak page, then Keycloak brings you back here.</p>
      <button className="btn btn-primary align-self-start" type="button" onClick={changePassword}>
        Change password
      </button>
    </div>
  )
}
