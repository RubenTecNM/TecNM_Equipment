import { useEffect, useMemo, useState } from 'react'
import { Link } from 'react-router-dom'
import { ApiError } from '../../../shared/api/http'
import { useAuth } from '../../../shared/hooks/useAuth'
import { can } from '../../../shared/utils/permissions'
import { formatDate } from '../../../shared/utils/format'
import { incidentsApi } from '../api'
import { StatusChip } from '../components/StatusChip'
import type {
  IncidentCatalogs,
  IncidentFilters,
  IncidentSummary,
  PagedResult,
  UserItem,
} from '../types'

export function IncidentsPage() {
  const { session } = useAuth()
  const viewGranted = can(session, 'view')

  const [result, setResult] = useState<PagedResult<IncidentSummary> | null>(null)
  const [catalogs, setCatalogs] = useState<IncidentCatalogs | null>(null)
  const [technicians, setTechnicians] = useState<UserItem[]>([])
  const [page, setPage] = useState(1)
  const [loading, setLoading] = useState(true)
  const [error, setError] = useState('')

  const [search, setSearch] = useState('')
  const [debouncedSearch, setDebouncedSearch] = useState('')
  const [statusFilter, setStatusFilter] = useState('')
  const [priorityFilter, setPriorityFilter] = useState('')
  const [typeFilter, setTypeFilter] = useState('')
  const [technicianFilter, setTechnicianFilter] = useState('')
  const [onlyOpen, setOnlyOpen] = useState(false)

  // Búsqueda con debounce (patrón del buscador de usuarios
  // en la ficha del activo): no disparar por cada tecla.
  useEffect(() => {
    const timer = setTimeout(() => setDebouncedSearch(search.trim()), 200)
    return () => clearTimeout(timer)
  }, [search])

  const filters: IncidentFilters = useMemo(
    () => ({
      status: statusFilter || undefined,
      priority: priorityFilter || undefined,
      incidentTypeId: typeFilter ? Number(typeFilter) : undefined,
      assignedTo: technicianFilter ? Number(technicianFilter) : undefined,
      onlyOpen: onlyOpen || undefined,
      search: debouncedSearch || undefined,
    }),
    [
      statusFilter,
      priorityFilter,
      typeFilter,
      technicianFilter,
      onlyOpen,
      debouncedSearch,
    ],
  )

  const hasFilters = Boolean(
    statusFilter || priorityFilter || typeFilter || technicianFilter || onlyOpen || debouncedSearch,
  )

  // Catálogos (desplegables de los filtros) y técnicos
  // (filtro "asignado a"): se cargan una sola vez.
  useEffect(() => {
    if (!viewGranted) return
    incidentsApi
      .catalogs()
      .then(setCatalogs)
      .catch(() => setCatalogs(null))
    incidentsApi
      .technicians()
      .then(setTechnicians)
      .catch(() => setTechnicians([]))
  }, [viewGranted])

  useEffect(() => {
    if (!viewGranted) return
    let cancelled = false
    incidentsApi
      .list(filters, page)
      .then((listResult) => {
        if (cancelled) return
        setError('')
        setResult(listResult)
      })
      .catch((err) => {
        if (cancelled) return
        setError(
          err instanceof ApiError
            ? err.message
            : 'No se pudieron cargar los incidentes.',
        )
      })
      .finally(() => {
        if (!cancelled) setLoading(false)
      })
    return () => {
      cancelled = true
    }
  }, [filters, page, viewGranted])

  function clearFilters() {
    setSearch('')
    setDebouncedSearch('')
    setStatusFilter('')
    setPriorityFilter('')
    setTypeFilter('')
    setTechnicianFilter('')
    setOnlyOpen(false)
    setPage(1)
  }

  if (!viewGranted) {
    return (
      <div className="space-y-3">
        <h1 className="font-display text-2xl font-semibold text-foreground">
          Incidentes
        </h1>
        <p
          role="alert"
          className="rounded-md bg-destructive/10 px-3 py-2 text-sm text-destructive"
        >
          No tienes permiso para ver el módulo de incidentes.
        </p>
      </div>
    )
  }

  return (
    <div className="space-y-4">
      <header className="flex items-center justify-between">
        <h1 className="font-display text-2xl font-semibold text-foreground">
          Incidentes
        </h1>
        {result && (
          <span role="status" className="text-sm text-muted-foreground">
            {result.totalCount} incidente{result.totalCount === 1 ? '' : 's'}
          </span>
        )}
      </header>

      <form
        role="search"
        aria-label="Filtros de incidentes"
        className="rounded-lg border border-border bg-card p-4"
        onSubmit={(event) => event.preventDefault()}
      >
        <div className="grid gap-3 sm:grid-cols-2 lg:grid-cols-4">
          <div>
            <label
              htmlFor="incident-search"
              className="mb-1 block text-sm font-medium text-foreground"
            >
              Buscar
            </label>
            <input
              id="incident-search"
              type="search"
              value={search}
              onChange={(event) => {
                setSearch(event.target.value)
                setPage(1)
              }}
              placeholder="Título del incidente"
              className="min-h-[44px] w-full rounded-md border border-border bg-background px-3 text-foreground"
            />
          </div>
          <div>
            <label
              htmlFor="status-filter"
              className="mb-1 block text-sm font-medium text-foreground"
            >
              Estado
            </label>
            <select
              id="status-filter"
              value={statusFilter}
              onChange={(event) => {
                setStatusFilter(event.target.value)
                setPage(1)
              }}
              className="min-h-[44px] w-full cursor-pointer rounded-md border border-border bg-background px-2.5 text-foreground"
            >
              <option value="">Todos</option>
              {(catalogs?.incidentStatuses ?? []).map((status) => (
                <option key={status.id} value={status.code}>
                  {status.name}
                </option>
              ))}
            </select>
          </div>
          <div>
            <label
              htmlFor="priority-filter"
              className="mb-1 block text-sm font-medium text-foreground"
            >
              Prioridad
            </label>
            <select
              id="priority-filter"
              value={priorityFilter}
              onChange={(event) => {
                setPriorityFilter(event.target.value)
                setPage(1)
              }}
              className="min-h-[44px] w-full cursor-pointer rounded-md border border-border bg-background px-2.5 text-foreground"
            >
              <option value="">Todas</option>
              {(catalogs?.incidentPriorities ?? []).map((priority) => (
                <option key={priority.id} value={priority.code}>
                  {priority.name}
                </option>
              ))}
            </select>
          </div>
          <div>
            <label
              htmlFor="type-filter"
              className="mb-1 block text-sm font-medium text-foreground"
            >
              Tipo de incidente
            </label>
            <select
              id="type-filter"
              value={typeFilter}
              onChange={(event) => {
                setTypeFilter(event.target.value)
                setPage(1)
              }}
              className="min-h-[44px] w-full cursor-pointer rounded-md border border-border bg-background px-2.5 text-foreground"
            >
              <option value="">Todos</option>
              {(catalogs?.incidentTypes ?? []).map((type) => (
                <option key={type.id} value={type.id}>
                  {type.name}
                </option>
              ))}
            </select>
          </div>
          <div>
            <label
              htmlFor="technician-filter"
              className="mb-1 block text-sm font-medium text-foreground"
            >
              Técnico asignado
            </label>
            <select
              id="technician-filter"
              value={technicianFilter}
              onChange={(event) => {
                setTechnicianFilter(event.target.value)
                setPage(1)
              }}
              className="min-h-[44px] w-full cursor-pointer rounded-md border border-border bg-background px-2.5 text-foreground"
            >
              <option value="">Todos</option>
              {technicians.map((technician) => (
                <option key={technician.userId} value={technician.userId}>
                  {technician.firstName} {technician.lastName} (
                  {technician.username})
                </option>
              ))}
            </select>
          </div>
          <div className="flex items-end">
            <label className="flex min-h-[44px] cursor-pointer items-center gap-2 text-sm font-medium text-foreground">
              <input
                type="checkbox"
                checked={onlyOpen}
                onChange={(event) => {
                  setOnlyOpen(event.target.checked)
                  setPage(1)
                }}
                className="h-4 w-4 cursor-pointer"
              />
              Solo abiertos
            </label>
          </div>
        </div>
        {hasFilters && (
          <button
            type="button"
            onClick={clearFilters}
            className="mt-3 min-h-[44px] cursor-pointer rounded-md border border-border px-3 text-sm text-foreground transition-colors duration-150 hover:bg-muted"
          >
            Limpiar filtros
          </button>
        )}
      </form>

      {error && (
        <p
          role="alert"
          className="rounded-md bg-destructive/10 px-3 py-2 text-sm text-destructive"
        >
          {error}
        </p>
      )}

      {loading && !result && (
        <p className="text-sm text-muted-foreground">Cargando incidentes…</p>
      )}

      {!loading && !error && result && result.items.length === 0 && (
        <div className="rounded-lg border border-dashed border-border p-8 text-center">
          <p className="font-medium text-foreground">
            {hasFilters ? 'Sin resultados' : 'Sin incidentes'}
          </p>
          <p className="mt-1 text-sm text-muted-foreground">
            {hasFilters
              ? 'Prueba con otros filtros.'
              : 'Los incidentes reportados sobre activos aparecen aquí.'}
          </p>
        </div>
      )}

      {result && result.items.length > 0 && (
        <div className="overflow-x-auto rounded-lg border border-border bg-card">
          <table className="w-full text-left text-sm">
            <thead>
              <tr className="border-b border-border bg-muted">
                <th scope="col" className="px-4 py-2.5 font-semibold text-foreground">
                  Incidente
                </th>
                <th scope="col" className="px-4 py-2.5 font-semibold text-foreground">
                  Activo
                </th>
                <th scope="col" className="px-4 py-2.5 font-semibold text-foreground">
                  Prioridad
                </th>
                <th scope="col" className="px-4 py-2.5 font-semibold text-foreground">
                  Estado
                </th>
                <th scope="col" className="px-4 py-2.5 font-semibold text-foreground">
                  Reportado por
                </th>
                <th scope="col" className="px-4 py-2.5 font-semibold text-foreground">
                  Asignado a
                </th>
                <th scope="col" className="px-4 py-2.5 font-semibold text-foreground">
                  Fecha
                </th>
              </tr>
            </thead>
            <tbody>
              {result.items.map((incident) => (
                <tr key={incident.incidentId} className="border-b border-border last:border-0">
                  <td className="px-4 py-3">
                    <Link
                      to={`/incidents/${incident.incidentId}`}
                      className="font-medium text-primary underline-offset-2 hover:underline"
                    >
                      {incident.title}
                    </Link>
                    <p className="text-xs text-muted-foreground">
                      {incident.typeName}
                    </p>
                  </td>
                  <td className="px-4 py-3">
                    <Link
                      to={`/assets/${incident.assetId}`}
                      className="text-primary underline-offset-2 hover:underline"
                    >
                      {incident.assetInventoryCode}
                    </Link>
                  </td>
                  <td className="px-4 py-3 text-secondary">
                    {incident.priorityName}
                  </td>
                  <td className="px-4 py-3">
                    <StatusChip
                      statusCode={incident.statusCode}
                      statusName={incident.statusName}
                    />
                  </td>
                  <td className="px-4 py-3 text-secondary">
                    {incident.reportedByUsername}
                  </td>
                  <td className="px-4 py-3 text-secondary">
                    {incident.assignedToUsername ?? '—'}
                  </td>
                  <td className="px-4 py-3 text-secondary">
                    {formatDate(incident.reportedAt)}
                  </td>
                </tr>
              ))}
            </tbody>
          </table>
        </div>
      )}

      {result && result.totalPages > 1 && (
        <nav
          className="flex items-center justify-between"
          aria-label="Paginación de incidentes"
        >
          <p className="text-sm text-muted-foreground">
            Página {result.pageNumber} de {result.totalPages}
          </p>
          <div className="flex gap-2">
            <button
              type="button"
              disabled={page <= 1}
              onClick={() => setPage((p) => p - 1)}
              className="min-h-[44px] cursor-pointer rounded-md border border-border bg-card px-4 text-sm text-foreground transition-colors duration-150 hover:bg-muted disabled:cursor-not-allowed disabled:opacity-50"
            >
              Anterior
            </button>
            <button
              type="button"
              disabled={page >= result.totalPages}
              onClick={() => setPage((p) => p + 1)}
              className="min-h-[44px] cursor-pointer rounded-md border border-border bg-card px-4 text-sm text-foreground transition-colors duration-150 hover:bg-muted disabled:cursor-not-allowed disabled:opacity-50"
            >
              Siguiente
            </button>
          </div>
        </nav>
      )}
    </div>
  )
}
