import { render, screen, waitFor, within } from '@testing-library/react'
import userEvent from '@testing-library/user-event'
import { afterEach, expect, it, vi } from 'vitest'
import { AssetPanel } from './AssetPanel'
import { AuthContext, type Auth } from './auth/AuthContext'

const auth: Auth = {
  authenticated: true,
  token: 'jwt',
  accountUrl: undefined,
  profile: undefined,
  roles: ['work-team'],
  logout: vi.fn(),
  refresh: vi.fn(),
  changePassword: vi.fn(),
}
const pole = {
  id: 'asset-1',
  friendlyId: 'MN-LP-00001',
  type: 'LightPost' as const,
  areaCode: 'MN',
  latitude: -37.045,
  longitude: 174.855,
  status: 'PendingInstallation' as const,
}
const installed = {
  id: 'event-1',
  assetId: 'asset-1',
  type: 'Installed',
  title: 'Installed new post',
  notes: 'Concrete base poured',
  occurredAt: '2026-09-20T01:00:00Z',
  version: 11,
  images: [
    {
      id: 'image-1',
      contentType: 'image/jpeg',
      sizeBytes: 2048,
      readUrl: 'https://s3.test/read/1',
    },
  ],
}
const checked = {
  id: 'event-2',
  assetId: 'asset-1',
  type: 'Checked',
  title: 'Annual check',
  notes: null,
  occurredAt: '2026-09-28T02:00:00Z',
  version: 12,
  images: [],
}

// What the API holds. A test changes it after the panel loads, to stand for the save that the API makes.
let events: object[] = [checked, installed]

// The API answers by method and path. A test can replace any answer.
function stubApi(answers: Record<string, { ok: boolean; status?: number; body: unknown }> = {}) {
  const defaults: Record<string, { ok: boolean; status?: number; body: unknown }> = {
    'GET /api/assets/asset-1/events': {
      ok: true,
      get body() {
        return events
      },
    },
    'GET /api/assets/asset-1': { ok: true, body: { ...pole, status: 'InService' } },
  }
  const fetchMock = vi.fn(async (url: string, init: RequestInit = {}) => {
    const answer = { ...defaults, ...answers }[`${init.method ?? 'GET'} ${url}`]
    if (!answer) return { ok: false, status: 404, json: async () => ({}) }
    return { ok: answer.ok, status: answer.status ?? 200, json: async () => answer.body }
  })
  vi.stubGlobal('fetch', fetchMock)
  return fetchMock
}

function renderPanel(onChanged = vi.fn(), onClose = vi.fn()) {
  render(
    <AuthContext.Provider value={auth}>
      <AssetPanel asset={pole} onChanged={onChanged} onClose={onClose} />
    </AuthContext.Provider>,
  )
  return { onChanged, onClose }
}

afterEach(() => {
  vi.unstubAllGlobals()
  events = [checked, installed]
})

it('names the asset and shows its type and status', async () => {
  stubApi()

  renderPanel()

  expect(screen.getByRole('heading', { name: 'MN-LP-00001' })).toBeInTheDocument()
  expect(screen.getByText('Light-post')).toBeInTheDocument()
  expect(screen.getByText('Pending installation')).toBeInTheDocument()
  await screen.findByText('Annual check')
})

it('lists the events, newest first, with their type, time, and notes', async () => {
  stubApi()

  renderPanel()

  const events = await screen.findAllByRole('listitem')
  expect(events).toHaveLength(2)
  expect(events[0]).toHaveTextContent('Annual check')
  expect(events[0]).toHaveTextContent('Checked')
  expect(within(events[0]).getByText(/./, { selector: 'time' })).toHaveAttribute(
    'datetime',
    '2026-09-28T02:00:00Z',
  )
  expect(events[1]).toHaveTextContent('Installed new post')
  expect(events[1]).toHaveTextContent('Concrete base poured')
})

it('says that an asset with no events is waiting for installation', async () => {
  stubApi({ 'GET /api/assets/asset-1/events': { ok: true, body: [] } })

  renderPanel()

  expect(
    await screen.findByText('No events yet. Add an Installed event when the Asset is in place.'),
  ).toBeInTheDocument()
})

it('adds an event, shows it first, and tells the map the new status of the asset', async () => {
  const added = { ...checked, id: 'event-3', type: 'Repaired', title: 'Replaced lamp', version: 20 }
  const fetchMock = stubApi({
    'POST /api/assets/asset-1/events': { ok: true, status: 201, body: added },
  })
  const { onChanged } = renderPanel()
  await screen.findByText('Annual check')
  events = [added, checked, installed]

  await userEvent.click(screen.getByRole('button', { name: 'Add event' }))
  await userEvent.selectOptions(screen.getByLabelText('Type'), 'Repaired')
  await userEvent.type(screen.getByLabelText('Title'), 'Replaced lamp')
  await userEvent.click(screen.getByRole('button', { name: 'Add Event' }))

  const post = fetchMock.mock.calls.find(([, init]) => init?.method === 'POST')!
  expect(JSON.parse(post[1]!.body as string)).toMatchObject({
    type: 'Repaired',
    title: 'Replaced lamp',
    notes: null,
  })
  expect((await screen.findAllByRole('listitem'))[0]).toHaveTextContent('Replaced lamp')
  expect(onChanged).toHaveBeenCalledWith({ ...pole, status: 'InService' })
  expect(screen.queryByLabelText('Title')).not.toBeInTheDocument()
})

it('shows why the API refused an event and keeps the form open', async () => {
  stubApi({
    'POST /api/assets/asset-1/events': {
      ok: false,
      status: 409,
      body: { title: 'The Asset was removed. Add an Installed Event before any other Event.' },
    },
  })
  renderPanel()
  await screen.findByText('Annual check')

  await userEvent.click(screen.getByRole('button', { name: 'Add event' }))
  await userEvent.type(screen.getByLabelText('Title'), 'Looked at it')
  await userEvent.click(screen.getByRole('button', { name: 'Add Event' }))

  expect(await screen.findByRole('alert')).toHaveTextContent('The Asset was removed.')
  expect(screen.getByLabelText('Title')).toHaveValue('Looked at it')
})

it('corrects an event and sends the version that it read', async () => {
  const corrected = { ...checked, title: 'Annual check, passed', version: 13 }
  const fetchMock = stubApi({
    'PUT /api/assets/asset-1/events/event-2': { ok: true, body: corrected },
  })
  renderPanel()
  const first = (await screen.findAllByRole('listitem'))[0]
  events = [corrected, installed]

  await userEvent.click(within(first).getByRole('button', { name: 'Edit' }))
  const title = screen.getByLabelText('Title')
  await userEvent.clear(title)
  await userEvent.type(title, 'Annual check, passed')
  await userEvent.click(screen.getByRole('button', { name: 'Save Event' }))

  const put = fetchMock.mock.calls.find(([, init]) => init?.method === 'PUT')!
  expect(JSON.parse(put[1]!.body as string)).toMatchObject({
    type: 'Checked',
    title: 'Annual check, passed',
    version: 12,
  })
  expect(await screen.findByText('Annual check, passed')).toBeInTheDocument()
})

it('closes on request', async () => {
  stubApi()
  const { onClose } = renderPanel()
  await screen.findByText('Annual check')

  await userEvent.click(screen.getByRole('button', { name: 'Close' }))

  expect(onClose).toHaveBeenCalled()
})

it('shows the photos of an event and lets the user open one in full', async () => {
  stubApi()

  renderPanel()

  const photo = await screen.findByRole('img', { name: 'Photo of Installed new post' })
  expect(photo).toHaveAttribute('src', 'https://s3.test/read/1')
  expect(photo.closest('a')).toHaveAttribute('href', 'https://s3.test/read/1')
})

it('uploads the photos of a new event straight to the storage link and shows them', async () => {
  const added = { ...checked, id: 'event-3', title: 'Replaced lamp', version: 20 }
  const withPhoto = {
    ...added,
    images: [
      { id: 'image-9', contentType: 'image/png', sizeBytes: 4, readUrl: 'https://s3.test/read/9' },
    ],
  }
  const fetchMock = stubApi({
    'POST /api/assets/asset-1/events': { ok: true, status: 201, body: added },
    'POST /api/assets/asset-1/events/event-3/images': {
      ok: true,
      status: 201,
      body: { id: 'image-9', uploadUrl: 'https://s3.test/upload/9', contentType: 'image/png' },
    },
    'PUT https://s3.test/upload/9': { ok: true, body: {} },
  })
  renderPanel()
  await screen.findByText('Annual check')
  events = [withPhoto, checked, installed]

  await userEvent.click(screen.getByRole('button', { name: 'Add event' }))
  await userEvent.type(screen.getByLabelText('Title'), 'Replaced lamp')
  const file = new File(['abcd'], 'lamp.png', { type: 'image/png' })
  await userEvent.upload(screen.getByLabelText('Photos'), file)
  await userEvent.click(screen.getByRole('button', { name: 'Add Event' }))

  expect(await screen.findByRole('img', { name: 'Photo of Replaced lamp' })).toBeInTheDocument()
  const asked = fetchMock.mock.calls.find(
    ([url]) => url === '/api/assets/asset-1/events/event-3/images',
  )!
  expect(JSON.parse(asked[1]!.body as string)).toEqual({ contentType: 'image/png', sizeBytes: 4 })
  const upload = fetchMock.mock.calls.find(([url]) => url === 'https://s3.test/upload/9')!
  // The storage link is signed already, so the call must not carry the API token.
  expect(upload[1]).toEqual({ method: 'PUT', headers: { 'Content-Type': 'image/png' }, body: file })
})

it('says which photo could not be uploaded and keeps the event that was saved', async () => {
  const added = { ...checked, id: 'event-3', title: 'Replaced lamp', version: 20 }
  stubApi({
    'POST /api/assets/asset-1/events': { ok: true, status: 201, body: added },
    'POST /api/assets/asset-1/events/event-3/images': {
      ok: false,
      status: 409,
      body: { title: 'An Event holds 5 images at most. Remove one first.' },
    },
  })
  renderPanel()
  await screen.findByText('Annual check')
  events = [added, checked, installed]

  await userEvent.click(screen.getByRole('button', { name: 'Add event' }))
  await userEvent.type(screen.getByLabelText('Title'), 'Replaced lamp')
  await userEvent.upload(
    screen.getByLabelText('Photos'),
    new File(['abcd'], 'lamp.png', { type: 'image/png' }),
  )
  await userEvent.click(screen.getByRole('button', { name: 'Add Event' }))

  expect(await screen.findByRole('alert')).toHaveTextContent(
    'lamp.png could not be uploaded. An Event holds 5 images at most.',
  )
  expect(screen.getByText('Replaced lamp')).toBeInTheDocument()
})

it('removes a photo from an event', async () => {
  const fetchMock = stubApi({
    'DELETE /api/assets/asset-1/events/event-1/images/image-1': {
      ok: true,
      status: 204,
      body: undefined,
    },
  })
  renderPanel()
  await screen.findByRole('img', { name: 'Photo of Installed new post' })
  events = [checked, { ...installed, images: [] }]

  await userEvent.click(screen.getByRole('button', { name: 'Remove photo' }))

  await waitFor(() => expect(screen.queryByRole('img')).not.toBeInTheDocument())
  expect(fetchMock).toHaveBeenCalledWith(
    '/api/assets/asset-1/events/event-1/images/image-1',
    expect.objectContaining({ method: 'DELETE' }),
  )
})
