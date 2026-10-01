import { useState } from 'react'
import { Link, NavLink } from 'react-router'

/** The main menu: the Frongle brand, links to Home and Map, and Logout. It collapses behind a toggle on a phone. */
export function Menu({ onLogout }: { onLogout: () => void }) {
  const [open, setOpen] = useState(false)
  const close = () => setOpen(false)

  return (
    <nav
      className="navbar navbar-expand-md border-bottom border-2 border-primary"
      aria-label="Main"
    >
      <div className="container-fluid">
        <Link className="navbar-brand d-flex align-items-center gap-2" to="/" onClick={close}>
          <img src="/logo.svg" alt="Frongle logo" width="40" height="40" />
          <h1 className="h3 m-0">Frongle</h1>
        </Link>
        <button
          className="navbar-toggler"
          type="button"
          aria-controls="main-menu"
          aria-expanded={open}
          aria-label="Toggle navigation"
          onClick={() => setOpen(!open)}
        >
          <span className="navbar-toggler-icon" />
        </button>
        <div id="main-menu" className={`navbar-collapse collapse${open ? ' show' : ''}`}>
          <ul className="navbar-nav ms-auto align-items-md-center gap-md-2">
            <li className="nav-item">
              <NavLink className="nav-link" to="/" end onClick={close}>
                Home
              </NavLink>
            </li>
            <li className="nav-item">
              <NavLink className="nav-link" to="/map" onClick={close}>
                Map
              </NavLink>
            </li>
            <li className="nav-item">
              <button
                className="btn btn-outline-primary my-2 my-md-0"
                type="button"
                onClick={onLogout}
              >
                Logout
              </button>
            </li>
          </ul>
        </div>
      </div>
    </nav>
  )
}
