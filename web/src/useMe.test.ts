import { act, renderHook, waitFor } from '@testing-library/react'
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

it('keeps the answer for the newest token when an answer for an older token arrives last', async () => {
  const answers: ((response: object) => void)[] = []
  vi.stubGlobal(
    'fetch',
    vi.fn(() => new Promise((resolve) => answers.push(resolve))),
  )
  const { result, rerender } = renderHook(({ token }) => useMe(token), {
    initialProps: { token: 'old' },
  })
  rerender({ token: 'new' })
  await waitFor(() => expect(answers).toHaveLength(2))

  await act(async () =>
    answers[1]({ ok: true, json: async () => ({ name: 'Morgan Manager', tenant: 'acme' }) }),
  )
  await act(async () => answers[0]({ ok: false, status: 401 }))

  expect(result.current.me?.name).toBe('Morgan Manager')
  expect(result.current.error).toBeUndefined()
})
