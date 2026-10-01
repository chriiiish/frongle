import { render, screen } from '@testing-library/react'
import userEvent from '@testing-library/user-event'
import { expect, it, vi } from 'vitest'
import { Menu } from './Menu'

function renderMenu(onLogout = vi.fn()) {
  render(<Menu onLogout={onLogout} />)
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

it('has a Home link to the start page', () => {
  renderMenu()

  const home = screen.getByRole('link', { name: 'Home' })
  expect(home).toHaveAttribute('href', '/')
  expect(home).toHaveClass('nav-link', 'active')
  expect(home).toHaveAttribute('aria-current', 'page')
})

it('has a Logout link that signs the user out', async () => {
  const onLogout = renderMenu()

  await userEvent.click(screen.getByRole('button', { name: 'Logout' }))

  expect(onLogout).toHaveBeenCalledOnce()
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
