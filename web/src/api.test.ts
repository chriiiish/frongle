import { afterEach, expect, it, vi } from 'vitest'
import { ApiError, createApi } from './api'

function stubFetch(response: object) {
  const fetchMock = vi.fn(async () => response)
  vi.stubGlobal('fetch', fetchMock)
  return fetchMock
}

afterEach(() => vi.unstubAllGlobals())

it('lists the areas with the token of the user', async () => {
  const areas = [{ id: '1', code: 'MN', name: 'Manukau' }]
  const fetchMock = stubFetch({ ok: true, json: async () => areas })

  expect(await createApi('jwt').listAreas()).toEqual(areas)

  expect(fetchMock).toHaveBeenCalledWith('/api/areas', {
    headers: { Authorization: 'Bearer jwt' },
  })
})

it('asks for the assets inside the box that the map shows', async () => {
  const fetchMock = stubFetch({ ok: true, json: async () => [] })

  await createApi('jwt').listAssets({ west: 174.8, south: -37.1, east: 174.9, north: -37 })

  expect(fetchMock.mock.calls[0]).toEqual([
    '/api/assets?west=174.8&south=-37.1&east=174.9&north=-37',
    { headers: { Authorization: 'Bearer jwt' } },
  ])
})

it('explains a refusal with the title that the API gave', async () => {
  stubFetch({
    ok: false,
    status: 422,
    json: async () => ({ title: 'This spot is outside every area.' }),
  })

  await expect(createApi('jwt').listAreas()).rejects.toThrow(
    new ApiError('This spot is outside every area.'),
  )
})

it('explains a validation problem with the messages that the API gave', async () => {
  stubFetch({
    ok: false,
    status: 400,
    json: async () => ({
      errors: { code: ['The code must be two capital letters.'], name: ['The Area needs a name.'] },
    }),
  })

  await expect(createApi('jwt').listAreas()).rejects.toThrow(
    'The code must be two capital letters. The Area needs a name.',
  )
})

it('explains a validation problem with its messages, not with its generic title', async () => {
  stubFetch({
    ok: false,
    status: 400,
    json: async () => ({
      title: 'One or more validation errors occurred.',
      errors: { code: ['The code must be two capital letters.'] },
    }),
  })

  await expect(createApi('jwt').listAreas()).rejects.toThrow(
    new ApiError('The code must be two capital letters.'),
  )
})

it('names the status when the API answers with a body of null', async () => {
  stubFetch({ ok: false, status: 502, json: async () => null })

  await expect(createApi('jwt').listAreas()).rejects.toThrow('The API returned status 502.')
})

it('names the status when the API gives no reason', async () => {
  stubFetch({ ok: false, status: 503, json: async () => Promise.reject(new Error('no body')) })

  await expect(createApi('jwt').listAreas()).rejects.toThrow('The API returned status 503.')
})

it('says so when the API cannot be reached', async () => {
  vi.stubGlobal('fetch', vi.fn().mockRejectedValue(new Error('offline')))

  await expect(createApi('jwt').listAreas()).rejects.toThrow(
    'The API could not be reached: offline',
  )
})
