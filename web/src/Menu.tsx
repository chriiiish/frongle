import { useState } from 'react'

/** The main menu: the Frongle brand, a Home link, and Logout. It collapses behind a toggle on a phone. */
export function Menu({ onLogout }: { onLogout: () => void }) {
  const [open, setOpen] = useState(false)

  return (
    <nav
      className="navbar navbar-expand-md border-bottom border-2 border-primary"
      aria-label="Main"
    >
      <div className="container">
        <a className="navbar-brand d-flex align-items-center gap-2" href="/">
          <img src="/logo.svg" alt="Frongle logo" width="40" height="40" />
          <h1 className="h3 m-0">Frongle</h1>
        </a>
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
          <ul className="navbar-nav ms-auto">
            <li className="nav-item">
              <a className="nav-link active" aria-current="page" href="/">
                Home
              </a>
            </li>
            <li className="nav-item">
              <button className="nav-link" type="button" onClick={onLogout}>
                Logout
              </button>
            </li>
          </ul>
        </div>
      </div>
    </nav>
  )
}
