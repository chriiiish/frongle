import { Link } from 'react-router'
import { useAuth } from './auth/AuthContext'
import type { Me } from './useMe'

const MANAGER_ROLE = 'maintenance-manager'
const WORK_TEAM_ROLE = 'work-team'

// These are the colours of the markers on the map.
const STATUSES = [
  {
    colour: '#f0ad4e',
    name: 'Pending installation',
    meaning: 'on the map, nobody has installed it yet',
  },
  { colour: '#198754', name: 'In service', meaning: 'installed and in use' },
  { colour: '#6c757d', name: 'Removed', meaning: 'taken away from the street' },
]

function roleLine(roles: string[]) {
  if (roles.includes(MANAGER_ROLE))
    return 'You draw Areas, watch the status of every Asset, and keep the records straight.'
  return 'You install, check, repair, and remove Assets. Every job goes in the history of the Asset.'
}

function Card({ title, text, action }: { title: string; text: string; action: string }) {
  return (
    <div className="col">
      <article className="card h-100 shadow-sm">
        <div className="card-body d-flex flex-column">
          <h3 className="h5 card-title">{title}</h3>
          <p className="card-text">{text}</p>
          <Link className="btn btn-primary mt-auto align-self-start" to="/map">
            {action}
          </Link>
        </div>
      </article>
    </div>
  )
}

/** The start page: it welcomes the signed-in user by name, says what they can do, and explains the map colours. */
export function Welcome({ me, error }: { me: Me | undefined; error: string | undefined }) {
  const { roles } = useAuth()
  if (error) return <p role="alert">{error}</p>
  if (!me) return <p role="status">Loading…</p>

  const isManager = roles.includes(MANAGER_ROLE)
  if (!isManager && !roles.includes(WORK_TEAM_ROLE))
    return (
      <div>
        <header className="py-3 py-md-4">
          <p className="text-uppercase fw-semibold small mb-1">{me.tenant}</p>
          <h2 className="display-5">Welcome, {me.name}</h2>
        </header>
        <p className="alert alert-info" role="status">
          Your account has no Frongle role yet. Ask a Maintenance Manager to give you one.
        </p>
      </div>
    )

  return (
    <div>
      <header className="py-3 py-md-4">
        <p className="text-uppercase fw-semibold small mb-1">{me.tenant}</p>
        <h2 className="display-5">Welcome, {me.name}</h2>
        <p className="lead mb-0">{roleLine(roles)}</p>
      </header>

      <div className="row row-cols-1 row-cols-md-3 g-3 mb-4">
        <Card
          title="See the map"
          text="Every Area and every Asset in view, with its status. Search by Friendly Id, or zoom in."
          action="Open the map"
        />
        <Card
          title="Add an Asset"
          text="Click the map where a light-post, street sign, telephone pole, or traffic light stands."
          action="Add an Asset"
        />
        {isManager ? (
          <Card
            title="Draw an Area"
            text="Outline part of the city and give it a two-letter code. Assets in it take that code in their Friendly Id."
            action="Draw an Area"
          />
        ) : (
          <Card
            title="Record work"
            text="Open an Asset to log a check, a repair, or a removal, and to attach photos."
            action="Find an Asset"
          />
        )}
      </div>

      <section aria-labelledby="status-legend">
        <h3 className="h5" id="status-legend">
          What the colours mean
        </h3>
        <ul className="list-unstyled">
          {STATUSES.map(({ colour, name, meaning }) => (
            <li key={name} className="d-flex align-items-center gap-2 mb-1">
              <span
                className="d-inline-block rounded-circle flex-shrink-0"
                style={{ width: '0.9rem', height: '0.9rem', background: colour }}
                aria-hidden="true"
              />
              <span>
                {name}: {meaning}
              </span>
            </li>
          ))}
        </ul>
      </section>
    </div>
  )
}
