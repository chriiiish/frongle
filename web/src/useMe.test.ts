import { renderHook, waitFor } from '@testing-library/react'
import { afterEach, expect, it, vi } from 'vitest'
import { useMe } from './useMe'

afterEach(() => vi.unstubAllGlobals())

it('does not ask the API who the user is before there is a token', () => {
  const fetchMock = vi.fn()
  vi.stubGlobal('fetch', fetchMock)

  const { result } = renderHook(() => useMe(undefined))

  expect(fetchMock).not.toHaveBeenCalled()
  expect(result.current).toEqual({ me: undefined, error: undefined })
})

it('shows no error once the token arrives and the API answers', async () => {
  const fetchMock = vi.fn().mockResolvedValue({
    ok: true,
    json: async () => ({ name: 'Morgan Manager', tenant: 'acme' }),
  })
  vi.stubGlobal('fetch', fetchMock)

  const { result, rerender } = renderHook(({ token }) => useMe(token), {
    initialProps: { token: undefined as string | undefined },
  })
  rerender({ token: 'jwt' })

  await waitFor(() => expect(result.current.me).toEqual({ name: 'Morgan Manager', tenant: 'acme' }))
  expect(result.current.error).toBeUndefined()
  expect(fetchMock).toHaveBeenCalledTimes(1)
})

it('forgets an earlier error when a later call succeeds', async () => {
  const fetchMock = vi
    .fn()
    .mockResolvedValueOnce({ ok: false, status: 401 })
    .mockResolvedValueOnce({
      ok: true,
      json: async () => ({ name: 'Morgan Manager', tenant: 'acme' }),
    })
  vi.stubGlobal('fetch', fetchMock)

  const { result, rerender } = renderHook(({ token }) => useMe(token), {
    initialProps: { token: 'old' },
  })
  await waitFor(() => expect(result.current.error).toBe('The API returned status 401.'))
  rerender({ token: 'new' })

  await waitFor(() => expect(result.current.me?.name).toBe('Morgan Manager'))
  expect(result.current.error).toBeUndefined()
})
