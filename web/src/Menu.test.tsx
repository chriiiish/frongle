import { render, screen, within } from '@testing-library/react'
import userEvent from '@testing-library/user-event'
import { MemoryRouter } from 'react-router'
import { expect, it, vi } from 'vitest'
import { Menu } from './Menu'
import type { Me } from './useMe'

const morgan = { name: 'Morgan Manager', tenant: 'acme' }

function renderMenu(path = '/', onLogout = vi.fn(), me: Me | null = morgan) {
  render(
    <MemoryRouter initialEntries={[path]}>
      <Menu me={me ?? undefined} onLogout={onLogout} />
    </MemoryRouter>,
  )
  return onLogout
}

it('is a Bootstrap navbar', () => {
  renderMenu()

  expect(screen.getByRole('navigation', { name: 'Main' })).toHaveClass('navbar')
})

it('shows the app name and logo as the brand, linked to home', () => {
  renderMenu()

  expect(screen.getByRole('heading', { name: 'Frongle' })).toBeInTheDocument()
  expect(screen.getByRole('img', { name: 'Frongle logo' })).toHaveAttribute('src', '/logo.svg')
  expect(screen.getByRole('link', { name: /Frongle/ })).toHaveAttribute('href', '/')
})

it('has a Home link to the welcome page that is current on the welcome page', () => {
  renderMenu('/')

  const home = screen.getByRole('link', { name: 'Home' })
  expect(home).toHaveAttribute('href', '/')
  expect(home).toHaveClass('nav-link', 'active')
  expect(home).toHaveAttribute('aria-current', 'page')
})

it('has a Map link to the map page that is current on the map page', () => {
  renderMenu('/map')

  const map = screen.getByRole('link', { name: 'Map' })
  expect(map).toHaveAttribute('href', '/map')
  expect(map).toHaveClass('nav-link', 'active')
  expect(map).toHaveAttribute('aria-current', 'page')
  expect(screen.getByRole('link', { name: 'Home' })).not.toHaveClass('active')
})

it('keeps the menu collapsed on a phone until the toggle is pressed', async () => {
  renderMenu()
  const toggle = screen.getByRole('button', { name: 'Toggle navigation' })
  const links = screen.getByRole('link', { name: 'Home' }).closest('.navbar-collapse')

  expect(toggle).toHaveAttribute('aria-expanded', 'false')
  expect(links).not.toHaveClass('show')

  await userEvent.click(toggle)

  expect(toggle).toHaveAttribute('aria-expanded', 'true')
  expect(links).toHaveClass('show')
})

it('closes the menu on a phone after the user picks a link', async () => {
  renderMenu()
  const toggle = screen.getByRole('button', { name: 'Toggle navigation' })
  await userEvent.click(toggle)

  await userEvent.click(screen.getByRole('link', { name: 'Map' }))

  expect(toggle).toHaveAttribute('aria-expanded', 'false')
})

it('spans the full width of the screen like the map page', () => {
  renderMenu()

  expect(screen.getByRole('navigation', { name: 'Main' }).firstElementChild).toHaveClass(
    'container-fluid',
  )
})

const accountToggle = () => screen.getByRole('button', { name: /Morgan Manager/ })

it('shows only the user name beside the dropdown arrow in the menu bar', () => {
  renderMenu()

  expect(accountToggle()).toHaveTextContent(/^Morgan Manager$/)
})

it('shows a plain Account label until the API has said who the user is', () => {
  renderMenu('/', vi.fn(), null)

  expect(screen.getByRole('button', { name: 'Account' })).toBeInTheDocument()
})

it('keeps the account dropdown closed until the user opens it', async () => {
  renderMenu()
  const dropdown = screen.getByRole('list', { name: 'Account' })

  expect(accountToggle()).toHaveAttribute('aria-expanded', 'false')
  expect(dropdown).not.toHaveClass('show')

  await userEvent.click(accountToggle())

  expect(accountToggle()).toHaveAttribute('aria-expanded', 'true')
  expect(dropdown).toHaveClass('show')
})

it('lists the tenant, a separator, Profile, Tenant Settings, a separator, and Logout in that order', async () => {
  renderMenu()
  await userEvent.click(accountToggle())

  const items = within(screen.getByRole('list', { name: 'Account' })).getAllByRole('listitem')

  expect(items.map((item) => item.textContent || 'separator')).toEqual([
    'acme',
    'separator',
    'Profile',
    'Tenant Settings',
    'separator',
    'Logout',
  ])
  expect(screen.getAllByRole('separator')).toHaveLength(2)
  expect(screen.getByRole('link', { name: 'Profile' })).toHaveAttribute('href', '/profile')
  expect(screen.getByRole('link', { name: 'Tenant Settings' })).toHaveAttribute(
    'href',
    '/tenant-settings',
  )
})

it('shows the tenant as plain text that the user cannot click', async () => {
  renderMenu()
  await userEvent.click(accountToggle())

  expect(screen.getByText('acme')).toHaveClass('dropdown-item-text')
  expect(screen.queryByRole('link', { name: 'acme' })).not.toBeInTheDocument()
  expect(screen.queryByRole('button', { name: 'acme' })).not.toBeInTheDocument()
})

it('has no tenant line until the API has said who the user is', async () => {
  renderMenu('/', vi.fn(), null)
  await userEvent.click(screen.getByRole('button', { name: 'Account' }))

  const items = within(screen.getByRole('list', { name: 'Account' })).getAllByRole('listitem')

  expect(items[0]).toHaveTextContent('Profile')
})

it('signs the user out from the account dropdown', async () => {
  const onLogout = renderMenu()
  await userEvent.click(accountToggle())

  await userEvent.click(screen.getByRole('button', { name: 'Logout' }))

  expect(onLogout).toHaveBeenCalledOnce()
})

it('closes the account dropdown after the user picks an option', async () => {
  renderMenu()
  await userEvent.click(accountToggle())

  await userEvent.click(screen.getByRole('link', { name: 'Profile' }))

  expect(accountToggle()).toHaveAttribute('aria-expanded', 'false')
})

it('spaces the menu items apart and centers them on desktop', () => {
  renderMenu()

  expect(screen.getByRole('link', { name: 'Home' }).closest('ul')).toHaveClass(
    'align-items-md-center',
    'gap-md-2',
  )
})
