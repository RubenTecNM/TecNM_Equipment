import type { AuthSession } from '../api/http'

/**
 * Acciones de incidentes (solo UX; el servidor manda).
 * Se resuelven con los permisos efectivos de GET /api/auth/me,
 * ya no con una tabla por rol en el cliente.
 * 'resolve' cubre asignar técnico, cambiar estado y resolver.
 */
export type IncidentAction = 'view' | 'create' | 'resolve' | 'delete'

export function can(session: AuthSession | null, action: IncidentAction): boolean {
  return session?.permissions.includes(`incidents.${action}`) ?? false
}
