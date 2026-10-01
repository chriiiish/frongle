/** Sends JSON to the Keycloak account service. The caller gets undefined on success, or a message that says why Keycloak refused. */
export async function postToKeycloak(
  url: string,
  token: string | undefined,
  body: object,
): Promise<string | undefined> {
  try {
    const response = await fetch(url, {
      method: 'POST',
      headers: { Authorization: `Bearer ${token}`, 'Content-Type': 'application/json' },
      body: JSON.stringify(body),
    })
    if (response.ok) return undefined
    const { errors } = await response.json().catch(() => ({ errors: [] }))
    const messages: string[] = (errors ?? []).map(
      (error: { errorMessage: string }) => error.errorMessage,
    )
    return messages.join(' ') || `Keycloak returned status ${response.status}.`
  } catch (failure) {
    return `Keycloak could not be reached: ${(failure as Error).message}`
  }
}
