import type { Me } from './useMe'

/** The start page: it welcomes the signed-in user by name, or shows why the API could not say who they are. */
export function Welcome({ me, error }: { me: Me | undefined; error: string | undefined }) {
  if (error) return <p role="alert">{error}</p>
  if (!me) return <p>Loading…</p>
  return <h2 className="display-5">Welcome, {me.name}</h2>
}
