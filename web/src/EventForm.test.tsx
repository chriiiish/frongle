import { render, screen } from '@testing-library/react'
import userEvent from '@testing-library/user-event'
import { afterEach, beforeEach, expect, it, vi } from 'vitest'
import { EventForm } from './EventForm'

beforeEach(() => {
  vi.useFakeTimers({ toFake: ['Date'] })
  vi.setSystemTime(new Date(2026, 9, 2, 14, 30))
})
afterEach(() => vi.useRealTimers())

function renderForm(props: Partial<Parameters<typeof EventForm>[0]> = {}) {
  const onSubmit = vi.fn()
  const onCancel = vi.fn()
  render(
    <EventForm
      submitLabel="Add Event"
      saving={false}
      photoSlots={5}
      onSubmit={onSubmit}
      onCancel={onCancel}
      {...props}
    />,
  )
  return { onSubmit, onCancel }
}

it('starts with a Checked event that happened just now', () => {
  renderForm()

  expect(screen.getByLabelText('Type')).toHaveValue('Checked')
  expect(screen.getByLabelText('When')).toHaveValue('2026-10-02T14:30')
  expect(screen.getAllByRole('option').map((o) => o.textContent)).toEqual([
    'Installed',
    'Checked',
    'Repaired',
    'Maintained',
    'Removed',
  ])
})

it('does not let the user save an event without a title', async () => {
  renderForm()
  const save = screen.getByRole('button', { name: 'Add Event' })
  expect(save).toBeDisabled()

  await userEvent.type(screen.getByLabelText('Title'), 'Checked pole')

  expect(save).toBeEnabled()
})

it('hands back what the user typed, with the time as a UTC instant', async () => {
  const { onSubmit } = renderForm()

  await userEvent.selectOptions(screen.getByLabelText('Type'), 'Repaired')
  await userEvent.type(screen.getByLabelText('Title'), 'Replaced lamp')
  await userEvent.type(screen.getByLabelText('Notes'), '70 W LED')
  await userEvent.click(screen.getByRole('button', { name: 'Add Event' }))

  expect(onSubmit).toHaveBeenCalledWith(
    {
      type: 'Repaired',
      title: 'Replaced lamp',
      notes: '70 W LED',
      occurredAt: new Date(2026, 9, 2, 14, 30).toISOString(),
    },
    [],
  )
})

it('sends no notes when the user wrote none', async () => {
  const { onSubmit } = renderForm()

  await userEvent.type(screen.getByLabelText('Title'), 'Checked pole')
  await userEvent.click(screen.getByRole('button', { name: 'Add Event' }))

  expect(onSubmit).toHaveBeenCalledWith(expect.objectContaining({ notes: null }), [])
})

it('shows the event that the user corrects, in local time', () => {
  renderForm({
    initial: {
      type: 'Maintained',
      title: 'Tightened bolts',
      notes: 'Two were loose',
      occurredAt: new Date(2026, 8, 30, 9, 5).toISOString(),
    },
    submitLabel: 'Save Event',
  })

  expect(screen.getByLabelText('Type')).toHaveValue('Maintained')
  expect(screen.getByLabelText('Title')).toHaveValue('Tightened bolts')
  expect(screen.getByLabelText('Notes')).toHaveValue('Two were loose')
  expect(screen.getByLabelText('When')).toHaveValue('2026-09-30T09:05')
  expect(screen.getByRole('button', { name: 'Save Event' })).toBeEnabled()
})

it('shows the reason when the save failed and cancels on request', async () => {
  const { onCancel } = renderForm({ problem: 'An Event cannot be in the future.' })

  expect(screen.getByRole('alert')).toHaveTextContent('An Event cannot be in the future.')
  await userEvent.click(screen.getByRole('button', { name: 'Cancel' }))
  expect(onCancel).toHaveBeenCalled()
})

const photo = (name = 'pole.jpg', type = 'image/jpeg', size = 1024) =>
  new File([new Uint8Array(size)], name, { type })

it('lets the user pick photos with the camera or the gallery and hands them back with the event', async () => {
  const { onSubmit } = renderForm()
  const pole = photo('pole.jpg')

  await userEvent.upload(screen.getByLabelText('Photos'), pole)
  await userEvent.type(screen.getByLabelText('Title'), 'Checked pole')
  await userEvent.click(screen.getByRole('button', { name: 'Add Event' }))

  expect(screen.getByLabelText('Photos')).toHaveAttribute('accept', 'image/*')
  expect(onSubmit).toHaveBeenCalledWith(expect.anything(), [pole])
})

it('lists the chosen photos and lets the user drop one', async () => {
  renderForm()

  await userEvent.upload(screen.getByLabelText('Photos'), [
    photo('a.jpg'),
    photo('b.png', 'image/png'),
  ])
  expect(screen.getByText('a.jpg')).toBeInTheDocument()
  expect(screen.getByText('b.png')).toBeInTheDocument()

  await userEvent.click(screen.getByRole('button', { name: 'Remove a.jpg' }))
  expect(screen.queryByText('a.jpg')).not.toBeInTheDocument()
  expect(screen.getByText('b.png')).toBeInTheDocument()
})

it('refuses a photo that is not a JPEG, PNG, or WebP, or is over 10 MB', async () => {
  renderForm()

  await userEvent.upload(
    screen.getByLabelText('Photos'),
    [photo('notes.pdf', 'application/pdf'), photo('huge.jpg', 'image/jpeg', 10 * 1024 * 1024 + 1)],
    { applyAccept: false },
  )

  expect(screen.getByRole('alert')).toHaveTextContent(
    'Only JPEG, PNG, and WebP photos up to 10 MB are accepted.',
  )
  expect(screen.queryByText('notes.pdf')).not.toBeInTheDocument()
  expect(screen.queryByText('huge.jpg')).not.toBeInTheDocument()
})

it('stops at the number of photos that the event can still hold', async () => {
  renderForm({ photoSlots: 2 })

  await userEvent.upload(screen.getByLabelText('Photos'), [
    photo('a.jpg'),
    photo('b.jpg'),
    photo('c.jpg'),
  ])

  expect(screen.getByRole('alert')).toHaveTextContent('An event holds 5 photos at most.')
  expect(screen.getByText('a.jpg')).toBeInTheDocument()
  expect(screen.getByText('b.jpg')).toBeInTheDocument()
  expect(screen.queryByText('c.jpg')).not.toBeInTheDocument()
})
