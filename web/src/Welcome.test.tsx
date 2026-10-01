import { render, screen, within } from '@testing-library/react'
import { MemoryRouter } from 'react-router'
import { expect, it, vi } from 'vitest'
import { AuthContext, type Auth } from './auth/AuthContext'
import { Welcome } from './Welcome'

const me = { name: 'Morgan Manager', tenant: 'acme' }

function renderWelcome(
  roles: string[],
  props: Parameters<typeof Welcome>[0] = { me, error: undefined },
) {
  const auth: Auth = {
    authenticated: true,
    token: 'jwt',
    accountUrl: undefined,
    profile: undefined,
    roles,
    logout: vi.fn(),
    refresh: vi.fn(),
    changePassword: vi.fn(),
  }
  render(
    <AuthContext.Provider value={auth}>
      <MemoryRouter>
        <Welcome {...props} />
      </MemoryRouter>
    </AuthContext.Provider>,
  )
}

it('welcomes the user by name and names the tenant', () => {
  renderWelcome(['maintenance-manager'])

  expect(screen.getByRole('heading', { name: 'Welcome, Morgan Manager' })).toHaveClass('display-5')
  expect(screen.getByText('acme')).toBeInTheDocument()
})

it('tells a Maintenance Manager what they do and offers to draw an Area', () => {
  renderWelcome(['maintenance-manager'])

  expect(screen.getByText(/You draw Areas/)).toBeInTheDocument()
  expect(screen.getByRole('heading', { name: 'Draw an Area' })).toBeInTheDocument()
  expect(screen.queryByRole('heading', { name: 'Record work' })).not.toBeInTheDocument()
})

it('tells a Work Team what they do and offers to record work', () => {
  renderWelcome(['work-team'])

  expect(screen.getByText(/You install, check, repair, and remove Assets/)).toBeInTheDocument()
  expect(screen.getByRole('heading', { name: 'Record work' })).toBeInTheDocument()
  expect(screen.queryByRole('heading', { name: 'Draw an Area' })).not.toBeInTheDocument()
})

it('sends the user to the map from every card', () => {
  renderWelcome(['work-team'])

  const cards = screen.getAllByRole('article')
  expect(cards).toHaveLength(3)
  for (const card of cards) {
    expect(within(card).getByRole('link')).toHaveAttribute('href', '/map')
  }
  expect(screen.getByRole('link', { name: 'Open the map' })).toBeInTheDocument()
})

it('explains the three colours of the markers on the map', () => {
  renderWelcome(['work-team'])

  const legend = within(screen.getByRole('region', { name: 'What the colours mean' }))
  expect(legend.getAllByRole('listitem').map((item) => item.textContent)).toEqual([
    'Pending installation: on the map, nobody has installed it yet',
    'In service: installed and in use',
    'Removed: taken away from the street',
  ])
})

it('shows that the page is loading while the API answers', () => {
  renderWelcome([], { me: undefined, error: undefined })

  expect(screen.getByRole('status')).toHaveTextContent('Loading')
})

it('shows why the API could not say who the user is', () => {
  renderWelcome([], { me: undefined, error: 'The API returned status 403.' })

  expect(screen.getByRole('alert')).toHaveTextContent('The API returned status 403.')
})
