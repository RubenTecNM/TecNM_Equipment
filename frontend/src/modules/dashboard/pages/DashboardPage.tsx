import { useAuth } from '../../../shared/hooks/useAuth'

export function DashboardPage() {
  const { session } = useAuth()

  return (
    <div className="space-y-6">
      <header>
        <h1 className="font-display text-2xl font-semibold text-foreground">
          Panel general
        </h1>
        <p className="mt-1 text-sm text-muted-foreground">
          Hola, {session?.firstName ?? session?.username ?? 'usuario'}
        </p>
      </header>

      <div className="rounded-lg border border-dashed border-border p-8 text-center">
        <p className="font-medium text-foreground">Métricas del inventario</p>
        <p className="mt-1 text-sm text-muted-foreground">
          Pendiente: endpoint de agregados en <code>Features/Dashboard</code> (FASE 2).
        </p>
      </div>
    </div>
  )
}
