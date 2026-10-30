import { useState } from 'react'
import type { ReactNode } from 'react'
import { NavLink, useNavigate } from 'react-router-dom'
import { BrandMark } from '../../shared/components/BrandMark'
import { useAuth } from '../../shared/hooks/useAuth'

type NavStatus = 'active' | 'planned'

interface NavItem {
  to: string
  label: string
  icon: string[]
  status: NavStatus
  // Permiso que habilita la entrada (GET /api/auth/me). Sin permiso
  // propio, los módulos pendientes solo los ve quien administra roles.
  permission?: string
  note?: string
}

const NAV_ITEMS: NavItem[] = [
  { to: '/', permission: 'dashboard.view', label: 'Panel', icon: ['M4 4h7v7H4zM13 4h7v7h-7zM4 13h7v7H4zM13 13h7v7h-7z'], status: 'active' },
  { to: '/assets', permission: 'assets.view', label: 'Activos', icon: ['M12 3l8 4.5v9L12 21l-8-4.5v-9L12 3z', 'M12 12l8-4.5M12 12v9M12 12L4 7.5'], status: 'active' },
  { to: '/locations', permission: 'locations.manage', label: 'Ubicaciones', icon: ['M12 21s-6-5.2-6-10a6 6 0 1112 0c0 4.8-6 10-6 10z', 'M12 11a2 2 0 100-4 2 2 0 000 4z'], status: 'active' },
  { to: '/incidents', permission: 'incidents.view', label: 'Incidentes', icon: ['M12 4L2.5 20h19L12 4z', 'M12 10v4M12 17v.3'], status: 'active' },
  { to: '/maintenance', permission: 'maintenance.manage', label: 'Mantenimiento', icon: ['M12 12a3 3 0 100-6 3 3 0 000 6z', 'M12 2v3M12 19v3M2 12h3M19 12h3M4.9 4.9L7 7M17 17l2.1 2.1M19.1 4.9L17 7M7 17l-2.1 2.1'], status: 'planned', note: 'FASE 2' },
  { to: '/disposal', label: 'Bajas', icon: ['M4 4h16v4H4z', 'M5 8h14v12H5z', 'M10 12h4'], status: 'planned', note: 'FASE 2' },
  { to: '/bulk-import', label: 'Carga masiva', icon: ['M12 16V4M7 9l5-5 5 5M4 20h16'], status: 'planned', note: 'FASE 2' },
  { to: '/reports', permission: 'reports.view', label: 'Reportes', icon: ['M7 3h7l5 5v13H7z', 'M14 3v5h5M10 13h5M10 17h5'], status: 'planned', note: 'FASE 2' },
  { to: '/software', permission: 'software.manage', label: 'Software', icon: ['M7 7h10v10H7z', 'M10 10h4v4h-4z', 'M9 3v4M15 3v4M9 17v4M15 17v4M3 9h4M3 15h4M17 9h4M17 15h4'], status: 'planned', note: 'FASE 2' },
  { to: '/components', permission: 'components.manage', label: 'Componentes', icon: ['M12 3l9 5-9 5-9-5 9-5z', 'M3 13l9 5 9-5'], status: 'planned', note: 'FASE 2' },
  { to: '/documents', label: 'Documentos', icon: ['M3 6h6l2 2h10v11H3z'], status: 'planned', note: 'FASE 3' },
  { to: '/history', label: 'Historial', icon: ['M12 21a9 9 0 110-18 9 9 0 010 18z', 'M12 7v5l3 2'], status: 'planned', note: 'FASE 3' },
  { to: '/users', permission: 'users.view', label: 'Usuarios', icon: ['M9 11a3 3 0 100-6 3 3 0 000 6z', 'M3 20c0-3.3 2.7-6 6-6s6 2.7 6 6'], status: 'planned', note: 'FASE 3' },
  { to: '/roles', permission: 'roles.manage', label: 'Roles', icon: ['M12 3l7 3v5c0 5-3.5 8-7 10-3.5-2-7-5-7-10V6l7-3z'], status: 'planned', note: 'FASE 3' },
  { to: '/procurement', label: 'Adquisiciones', icon: ['M9 20a1 1 0 100-2 1 1 0 000 2z', 'M17 20a1 1 0 100-2 1 1 0 000 2z', 'M4 5h2l2.5 11h10L21 8H7'], status: 'planned', note: 'FASE 2' },
]

export function MainLayout({ children }: { children: ReactNode }) {
  const { session, logout, hasPermission } = useAuth()
  const navigate = useNavigate()
  const [menuOpen, setMenuOpen] = useState(false)

  // Menú por permisos efectivos del servidor (solo UX; la API valida).
  const visibleItems = NAV_ITEMS.filter((item) => hasPermission(item.permission ?? 'roles.manage'))

  async function handleLogout() {
    await logout()
    navigate('/login', { replace: true })
  }

  return (
    <div className="flex min-h-screen">
      {/* Barra móvil */}
      <header className="fixed inset-x-0 top-0 z-10 flex items-center justify-between border-b border-border bg-card px-4 py-2.5 md:hidden">
        <span className="flex items-center gap-2 font-display text-base font-semibold text-primary">
          <BrandMark className="h-7 w-7" />
          TecNM Inventario
        </span>
        <button
          type="button"
          onClick={() => setMenuOpen((open) => !open)}
          aria-expanded={menuOpen}
          aria-label="Abrir menú"
          className="min-h-[44px] min-w-[44px] cursor-pointer rounded-md px-3 text-primary"
        >
          <svg viewBox="0 0 24 24" className="h-6 w-6" fill="none" stroke="currentColor" strokeWidth="2" strokeLinecap="round" aria-hidden="true">
            <path d="M4 6h16M4 12h16M4 18h16" />
          </svg>
        </button>
      </header>

      {/* Barra lateral */}
      <aside
        className={`fixed inset-y-0 left-0 z-20 w-60 flex-col border-r border-border bg-card md:static md:flex ${
          menuOpen ? 'flex pt-14' : 'hidden'
        }`}
      >
        <div className="flex items-center gap-2.5 px-5 py-5">
          <BrandMark />
          <span className="font-display text-base font-semibold text-primary">TecNM Inventario</span>
        </div>

        <nav className="flex-1 overflow-y-auto px-2 py-2" aria-label="Navegación principal">
          <ul className="space-y-0.5">
            {visibleItems.map((item) =>
              item.status === 'active' ? (
                <li key={item.to}>
                  <NavLink
                    to={item.to}
                    end={item.to === '/'}
                    onClick={() => setMenuOpen(false)}
                    className={({ isActive }) =>
                      `relative flex min-h-[44px] items-center gap-3 rounded-lg px-3 text-sm transition-colors duration-150 ${
                        isActive
                          ? 'bg-muted font-semibold text-primary before:absolute before:inset-y-2.5 before:left-0 before:w-[3px] before:rounded-full before:bg-accent'
                          : 'text-muted-foreground hover:bg-muted hover:text-primary'
                      }`
                    }
                  >
                    <NavIcon paths={item.icon} />
                    {item.label}
                  </NavLink>
                </li>
              ) : (
                <li key={item.to}>
                  <div
                    title={`Pendiente (${item.note})`}
                    className="flex min-h-[44px] cursor-default items-center gap-3 rounded-lg px-3 text-sm text-muted-foreground/60"
                  >
                    <NavIcon paths={item.icon} />
                    {item.label}
                    <span className="ml-auto rounded-full bg-muted px-2 py-0.5 text-[11px] font-medium text-muted-foreground">
                      {item.note}
                    </span>
                  </div>
                </li>
              ),
            )}
          </ul>
        </nav>

        <div className="border-t border-border p-3">
          <div className="mb-2 flex items-center gap-2.5 px-1">
            <div className="flex h-9 w-9 items-center justify-center rounded-full bg-muted font-display text-sm font-semibold text-primary" aria-hidden="true">
              {(session?.firstName || session?.username || '?').charAt(0).toUpperCase()}
            </div>
            <div className="min-w-0 flex-1">
              <p className="truncate text-sm font-medium text-foreground">
                {session ? `${session.firstName} ${session.lastName}` : ''}
              </p>
              <p className="truncate text-xs text-muted-foreground">{session?.roleName}</p>
            </div>
          </div>
          <button
            type="button"
            onClick={handleLogout}
            className="min-h-[44px] w-full cursor-pointer rounded-lg px-3 text-left text-sm text-muted-foreground transition-colors duration-150 hover:bg-muted hover:text-primary"
          >
            Cerrar sesión
          </button>
        </div>
      </aside>

      <main className="min-w-0 flex-1 p-4 pt-16 md:p-6 md:pt-6">{children}</main>
    </div>
  )
}

function NavIcon({ paths }: { paths: string[] }) {
  return (
    <svg
      viewBox="0 0 24 24"
      className="h-5 w-5 shrink-0"
      fill="none"
      stroke="currentColor"
      strokeWidth="1.8"
      strokeLinecap="round"
      strokeLinejoin="round"
      aria-hidden="true"
    >
      {paths.map((d) => (
        <path key={d} d={d} />
      ))}
    </svg>
  )
}
