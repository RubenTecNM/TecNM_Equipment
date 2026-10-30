import { useEffect, useState } from 'react'
import type { FormEvent, ReactNode } from 'react'
import { Link, useParams } from 'react-router-dom'
import { ApiError } from '../../../shared/api/http'
import { formatDate, formatDateTime } from '../../../shared/utils/format'
import { assetsApi, catalogName } from '../api'
import type {
  AssetAssignment,
  AssetDetail,
  AssetMovement,
  GetCatalogsResponse,
  LocationItem,
  PagedResult,
  UserItem,
} from '../types'

type LoadState = 'loading' | 'not-found' | 'error' | 'ready'

export function AssetDetailPage() {
  const { assetId } = useParams<{ assetId: string }>()
  const numericId = Number(assetId)
  const isValidId = Number.isInteger(numericId)

  const [state, setState] = useState<LoadState>('loading')
  const [detail, setDetail] = useState<AssetDetail | null>(null)
  const [catalogs, setCatalogs] = useState<GetCatalogsResponse | null>(null)
  const [locations, setLocations] = useState<LocationItem[]>([])
  const [movements, setMovements] = useState<PagedResult<AssetMovement> | null>(null)
  const [movementsPage, setMovementsPage] = useState(1)
  const [assignments, setAssignments] = useState<AssetAssignment[]>([])

  const [movementForm, setMovementForm] = useState({ toLocationId: '', movementReasonId: '', notes: '' })
  const [assignmentForm, setAssignmentForm] = useState({ assignedTo: '', assignmentTypeId: '', locationId: '', notes: '' })
  const [userSearch, setUserSearch] = useState('')
  const [users, setUsers] = useState<UserItem[]>([])
  const [submitting, setSubmitting] = useState<'movement' | 'assignment' | null>(null)
  const [formError, setFormError] = useState('')
  const [detailLoading, setDetailLoading] = useState(false)

  const loadDetail = async () => {
    setDetailLoading(true)
    try {
      const [d, c, l] = await Promise.all([
        assetsApi.getById(numericId),
        assetsApi.catalogs(),
        assetsApi.locations(),
      ])
      setDetail(d)
      setCatalogs(c)
      setLocations(l)
    } finally {
      setDetailLoading(false)
    }
  }

  // Un 409 significa que otra persona modificó el activo: se
  // recargan los datos automáticamente. Los botones quedan
  // deshabilitados mientras la recarga termina, para no reenviar
  // con la versión anterior.
  function operationError(err: unknown, fallback: string): string {
    if (err instanceof ApiError && err.status === 409) {
      loadDetail().catch(() => {
        // La recarga es automática; si falla, el usuario recarga a mano.
      })
      return 'Otra persona modificó este activo. Recargamos los datos; revísalos e intenta de nuevo.'
    }
    return err instanceof ApiError ? err.message : fallback
  }

  // Bloquea los formularios mientras hay una operación o una
  // recarga de la ficha en curso (evita un falso 409 por enviar
  // con la versión de antes de la recarga).
  const busy = submitting !== null || detailLoading

  const loadMovements = async () => {
    const m = await assetsApi.getMovements(numericId, movementsPage)
    setMovements(m)
  }

  const loadAssignments = async () => {
    setAssignments(await assetsApi.getAssignments(numericId))
  }

  // Carga inicial: setState solo dentro de los
  // callbacks de la promesa (patrón del listado
  // de activos), para evitar renders en cascada
  // desde el efecto. La recarga de la ficha
  // (loadDetail) se invoca desde manejadores.
  useEffect(() => {
    if (!isValidId) return
    Promise.all([
      assetsApi.getById(numericId),
      assetsApi.catalogs(),
      assetsApi.locations(),
    ])
      .then(([d, c, l]) => {
        setDetail(d)
        setCatalogs(c)
        setLocations(l)
        setState('ready')
      })
      .catch((err) => {
        setState(err instanceof ApiError && err.status === 404 ? 'not-found' : 'error')
      })
  }, [numericId, isValidId])

  // Movimientos y asignaciones: cada llamada va
  // por su cuenta (un fallo no vacía la otra),
  // igual que antes; setState solo en callbacks.
  useEffect(() => {
    if (state !== 'ready') return
    assetsApi
      .getMovements(numericId, movementsPage)
      .then(setMovements)
      .catch(() => setMovements(null))
    assetsApi
      .getAssignments(numericId)
      .then(setAssignments)
      .catch(() => setAssignments([]))
  }, [state, movementsPage, numericId])

  useEffect(() => {
    const timer = setTimeout(() => {
      assetsApi.users(userSearch).then(setUsers).catch(() => setUsers([]))
    }, 200)
    return () => clearTimeout(timer)
  }, [userSearch])

  if (isValidId && state === 'loading') {
    return <p className="text-sm text-muted-foreground">Cargando ficha del activo…</p>
  }

  if (!isValidId || state === 'not-found') {
    return (
      <div className="space-y-3">
        <p className="font-medium text-foreground">Activo no encontrado</p>
        <Link to="/assets" className="text-sm text-primary underline-offset-2 hover:underline">
          Volver a activos
        </Link>
      </div>
    )
  }

  if (state === 'error' || !detail) {
    return <p role="alert" className="text-sm text-destructive">No se pudo cargar la ficha del activo.</p>
  }

  const currentLocationName =
    locations.find((l) => l.locationId === detail.currentLocationId)?.name ?? '—'
  const assetTypeName = catalogName(catalogs?.assetTypes, detail.assetTypeCode)
  const assetStatusName = catalogName(catalogs?.assetStatuses, detail.assetStatusCode)
  // Versión del activo que se muestra en pantalla: es la que se
  // envía como expectedVersion en movimiento, asignación y devolución.
  const currentVersion = detail.version

  async function handleMovementSubmit(event: FormEvent<HTMLFormElement>) {
    event.preventDefault()
    setFormError('')
    if (!movementForm.toLocationId || !movementForm.movementReasonId) {
      setFormError('Selecciona ubicación de destino y motivo.')
      return
    }
    setSubmitting('movement')
    try {
      await assetsApi.createMovement(numericId, currentVersion, {
        toLocationId: Number(movementForm.toLocationId),
        movementReasonId: Number(movementForm.movementReasonId),
        notes: movementForm.notes.trim() || undefined,
      })
      setMovementForm({ toLocationId: '', movementReasonId: '', notes: '' })
      await Promise.all([loadDetail(), loadMovements()])
    } catch (err) {
      setFormError(operationError(err, 'No se pudo registrar el movimiento.'))
    } finally {
      setSubmitting(null)
    }
  }

  async function handleAssignmentSubmit(event: FormEvent<HTMLFormElement>) {
    event.preventDefault()
    setFormError('')
    if (!assignmentForm.assignedTo || !assignmentForm.assignmentTypeId) {
      setFormError('Selecciona usuario y tipo de asignación.')
      return
    }
    setSubmitting('assignment')
    try {
      await assetsApi.createAssignment(numericId, currentVersion, {
        assignedTo: Number(assignmentForm.assignedTo),
        assignmentTypeId: Number(assignmentForm.assignmentTypeId),
        locationId: assignmentForm.locationId ? Number(assignmentForm.locationId) : null,
        notes: assignmentForm.notes.trim() || undefined,
      })
      setAssignmentForm({ assignedTo: '', assignmentTypeId: '', locationId: '', notes: '' })
      await Promise.all([loadDetail(), loadAssignments()])
    } catch (err) {
      setFormError(operationError(err, 'No se pudo registrar la asignación.'))
    } finally {
      setSubmitting(null)
    }
  }

  async function handleReturn(assignmentId: number) {
    setFormError('')
    try {
      await assetsApi.returnAssignment(numericId, assignmentId, currentVersion)
      await Promise.all([loadDetail(), loadAssignments()])
    } catch (err) {
      setFormError(operationError(err, 'No se pudo devolver la asignación.'))
    }
  }

  return (
    <div className="space-y-6">
      <header className="space-y-1">
        <Link to="/assets" className="text-sm text-secondary underline-offset-2 hover:underline">
          Activos
        </Link>
        <div className="flex flex-wrap items-center gap-3">
          <h1 className="font-display text-2xl font-semibold text-foreground">
            {detail.inventoryCode}
          </h1>
          <span className="rounded-full bg-muted px-2.5 py-0.5 text-xs font-medium text-secondary">
            {assetTypeName}
          </span>
          <span className="rounded-full bg-success/10 px-2.5 py-0.5 text-xs font-medium text-success">
            {assetStatusName}
          </span>
        </div>
      </header>

      {formError && (
        <p role="alert" className="rounded-md bg-destructive/10 px-3 py-2 text-sm text-destructive">
          {formError}
        </p>
      )}

      <section aria-labelledby="datos-generales" className="rounded-lg border border-border bg-card p-5">
        <h2 id="datos-generales" className="mb-3 font-display text-base font-semibold text-foreground">
          Datos generales
        </h2>
        <dl className="grid gap-x-8 gap-y-3 sm:grid-cols-2">
          <Field label="Fabricante" value={detail.manufacturerName} />
          <Field label="Modelo" value={detail.modelName} />
          <Field label="Serie" value={detail.serialNumber} />
          <Field label="Hostname" value={detail.hostname} />
          <div>
            <dt className="text-xs font-medium text-muted-foreground">
              Ubicación actual
            </dt>
            <dd className="mt-0.5 text-sm text-foreground">
              {detail.currentLocationId !== null ? (
                <Link
                  to={`/locations/${detail.currentLocationId}`}
                  className="text-primary underline-offset-2 hover:underline"
                >
                  {currentLocationName}
                </Link>
              ) : (
                '—'
              )}
            </dd>
          </div>
          <Field label="Versión" value={String(detail.version)} />
        </dl>
        {detail.notes && (
          <div className="mt-3 border-t border-border pt-3">
            <dt className="text-xs font-medium text-muted-foreground">Notas</dt>
            <dd className="mt-0.5 text-sm text-foreground">{detail.notes}</dd>
          </div>
        )}
      </section>

      <section aria-labelledby="movimientos" className="rounded-lg border border-border bg-card p-5">
        <h2 id="movimientos" className="mb-3 font-display text-base font-semibold text-foreground">
          Movimientos de ubicación
        </h2>

        {movements && movements.items.length > 0 && (
          <div className="mb-5 overflow-x-auto rounded-md border border-border">
            <table className="w-full text-left text-sm">
              <thead>
                <tr className="border-b border-border bg-muted">
                  <th scope="col" className="px-3 py-2 font-semibold text-foreground">Fecha</th>
                  <th scope="col" className="px-3 py-2 font-semibold text-foreground">Origen</th>
                  <th scope="col" className="px-3 py-2 font-semibold text-foreground">Destino</th>
                  <th scope="col" className="px-3 py-2 font-semibold text-foreground">Motivo</th>
                  <th scope="col" className="px-3 py-2 font-semibold text-foreground">Por</th>
                </tr>
              </thead>
              <tbody>
                {movements.items.map((movement) => (
                  <tr key={movement.assetMovementId} className="border-b border-border last:border-0">
                    <td className="px-3 py-2.5 text-secondary">{formatDateTime(movement.movedAt)}</td>
                    <td className="px-3 py-2.5 text-secondary">{movement.fromLocationName ?? '—'}</td>
                    <td className="px-3 py-2.5 font-medium text-foreground">{movement.toLocationName}</td>
                    <td className="px-3 py-2.5 text-secondary">{movement.movementReasonName}</td>
                    <td className="px-3 py-2.5 text-secondary">{movement.movedByUsername}</td>
                  </tr>
                ))}
              </tbody>
            </table>
          </div>
        )}

        {movements && movements.items.length === 0 && (
          <p className="mb-5 text-sm text-muted-foreground">Sin movimientos registrados.</p>
        )}

        {movements && movements.totalPages > 1 && (
          <nav className="mb-5 flex items-center gap-2" aria-label="Paginación de movimientos">
            <button
              type="button"
              disabled={movementsPage <= 1}
              onClick={() => setMovementsPage((p) => p - 1)}
              className="min-h-[44px] cursor-pointer rounded-md border border-border px-3 text-sm hover:bg-muted disabled:cursor-not-allowed disabled:opacity-50"
            >
              Anterior
            </button>
            <span className="text-sm text-muted-foreground">
              Página {movements.pageNumber} de {movements.totalPages}
            </span>
            <button
              type="button"
              disabled={movementsPage >= movements.totalPages}
              onClick={() => setMovementsPage((p) => p + 1)}
              className="min-h-[44px] cursor-pointer rounded-md border border-border px-3 text-sm hover:bg-muted disabled:cursor-not-allowed disabled:opacity-50"
            >
              Siguiente
            </button>
          </nav>
        )}

        <h3 className="mb-2 text-sm font-semibold text-foreground">Registrar movimiento</h3>
        <form onSubmit={handleMovementSubmit} className="grid gap-3 sm:grid-cols-2" noValidate>
          <div>
            <label htmlFor="to-location" className="mb-1 block text-sm font-medium text-foreground">
              Ubicación de destino
            </label>
            <select
              id="to-location"
              value={movementForm.toLocationId}
              onChange={(e) => setMovementForm({ ...movementForm, toLocationId: e.target.value })}
              className="min-h-[44px] w-full cursor-pointer rounded-md border border-border bg-background px-2.5 text-foreground"
            >
              <option value="">Selecciona una ubicación</option>
              {locations.map((location) => (
                <option key={location.locationId} value={location.locationId}>
                  {location.name} ({location.code})
                </option>
              ))}
            </select>
          </div>
          <div>
            <label htmlFor="movement-reason" className="mb-1 block text-sm font-medium text-foreground">
              Motivo
            </label>
            <select
              id="movement-reason"
              value={movementForm.movementReasonId}
              onChange={(e) => setMovementForm({ ...movementForm, movementReasonId: e.target.value })}
              className="min-h-[44px] w-full cursor-pointer rounded-md border border-border bg-background px-2.5 text-foreground"
            >
              <option value="">Selecciona un motivo</option>
              {(catalogs?.movementReasons ?? []).map((reason) => (
                <option key={reason.id} value={reason.id}>
                  {reason.name}
                </option>
              ))}
            </select>
          </div>
          <div className="sm:col-span-2">
            <label htmlFor="movement-notes" className="mb-1 block text-sm font-medium text-foreground">
              Notas
            </label>
            <input
              id="movement-notes"
              type="text"
              value={movementForm.notes}
              onChange={(e) => setMovementForm({ ...movementForm, notes: e.target.value })}
              className="min-h-[44px] w-full rounded-md border border-border bg-background px-3 text-foreground"
              placeholder="Observaciones del movimiento (opcional)"
            />
          </div>
          <div className="sm:col-span-2">
            <button
              type="submit"
              disabled={busy}
              className="min-h-[44px] cursor-pointer rounded-md bg-primary px-5 font-semibold text-on-primary transition-colors duration-150 hover:bg-secondary disabled:cursor-wait disabled:opacity-60"
            >
              {submitting === 'movement' ? 'Registrando…' : 'Registrar movimiento'}
            </button>
          </div>
        </form>
      </section>

      <section aria-labelledby="asignaciones" className="rounded-lg border border-border bg-card p-5">
        <h2 id="asignaciones" className="mb-3 font-display text-base font-semibold text-foreground">
          Asignaciones
        </h2>

        {assignments.length === 0 && (
          <p className="mb-5 text-sm text-muted-foreground">Sin asignaciones registradas.</p>
        )}

        {assignments.length > 0 && (
          <ul className="mb-5 space-y-2">
            {assignments.map((assignment) => (
              <li
                key={assignment.assetAssignmentId}
                className="flex flex-wrap items-center gap-x-4 gap-y-1 rounded-md border border-border px-3 py-2.5 text-sm"
              >
                <span className="font-medium text-foreground">{assignment.assignedToUsername}</span>
                <span className="text-secondary">{assignment.assignmentTypeName}</span>
                <span className="text-secondary">
                  {assignment.locationName ?? 'Sin ubicación de entrega'}
                </span>
                <span className="text-muted-foreground">
                  {formatDate(assignment.assignedAt)}
                  {assignment.returnedAt && ` → ${formatDate(assignment.returnedAt)}`}
                </span>
                {assignment.isActive ? (
                  <span className="rounded-full bg-success/10 px-2 py-0.5 text-xs font-medium text-success">
                    Activa
                  </span>
                ) : (
                  <span className="rounded-full bg-muted px-2 py-0.5 text-xs font-medium text-muted-foreground">
                    Devuelta
                  </span>
                )}
                {assignment.isActive && (
                  <button
                    type="button"
                    onClick={() => handleReturn(assignment.assetAssignmentId)}
                    disabled={busy}
                    className="ml-auto min-h-[44px] cursor-pointer rounded-md border border-border px-3 text-sm text-foreground transition-colors duration-150 hover:bg-muted disabled:cursor-wait disabled:opacity-60"
                  >
                    Devolver
                  </button>
                )}
              </li>
            ))}
          </ul>
        )}

        <h3 className="mb-2 text-sm font-semibold text-foreground">Asignar activo</h3>
        <form onSubmit={handleAssignmentSubmit} className="grid gap-3 sm:grid-cols-2" noValidate>
          <div>
            <label htmlFor="assigned-to" className="mb-1 block text-sm font-medium text-foreground">
              Usuario
            </label>
            <input
              id="assigned-to-search"
              type="search"
              value={userSearch}
              onChange={(e) => setUserSearch(e.target.value)}
              placeholder="Busca por nombre o usuario"
              className="mb-1.5 min-h-[44px] w-full rounded-md border border-border bg-background px-3 text-foreground"
              aria-label="Buscar usuario"
            />
            <select
              id="assigned-to"
              value={assignmentForm.assignedTo}
              onChange={(e) => setAssignmentForm({ ...assignmentForm, assignedTo: e.target.value })}
              className="min-h-[44px] w-full cursor-pointer rounded-md border border-border bg-background px-2.5 text-foreground"
            >
              <option value="">Selecciona un usuario</option>
              {users.map((user) => (
                <option key={user.userId} value={user.userId}>
                  {user.firstName} {user.lastName} ({user.username})
                </option>
              ))}
            </select>
          </div>
          <div>
            <label htmlFor="assignment-type" className="mb-1 block text-sm font-medium text-foreground">
              Tipo de asignación
            </label>
            <select
              id="assignment-type"
              value={assignmentForm.assignmentTypeId}
              onChange={(e) => setAssignmentForm({ ...assignmentForm, assignmentTypeId: e.target.value })}
              className="min-h-[44px] w-full cursor-pointer rounded-md border border-border bg-background px-2.5 text-foreground"
            >
              <option value="">Selecciona un tipo</option>
              {(catalogs?.assignmentTypes ?? []).map((type) => (
                <option key={type.id} value={type.id}>
                  {type.name}
                </option>
              ))}
            </select>
          </div>
          <div>
            <label htmlFor="assignment-location" className="mb-1 block text-sm font-medium text-foreground">
              Ubicación de entrega
            </label>
            <select
              id="assignment-location"
              value={assignmentForm.locationId}
              onChange={(e) => setAssignmentForm({ ...assignmentForm, locationId: e.target.value })}
              className="min-h-[44px] w-full cursor-pointer rounded-md border border-border bg-background px-2.5 text-foreground"
            >
              <option value="">Ninguna</option>
              {locations.map((location) => (
                <option key={location.locationId} value={location.locationId}>
                  {location.name}
                </option>
              ))}
            </select>
          </div>
          <div>
            <label htmlFor="assignment-notes" className="mb-1 block text-sm font-medium text-foreground">
              Notas
            </label>
            <input
              id="assignment-notes"
              type="text"
              value={assignmentForm.notes}
              onChange={(e) => setAssignmentForm({ ...assignmentForm, notes: e.target.value })}
              className="min-h-[44px] w-full rounded-md border border-border bg-background px-3 text-foreground"
              placeholder="Observaciones (opcional)"
            />
          </div>
          <div className="sm:col-span-2">
            <button
              type="submit"
              disabled={busy}
              className="min-h-[44px] cursor-pointer rounded-md bg-primary px-5 font-semibold text-on-primary transition-colors duration-150 hover:bg-secondary disabled:cursor-wait disabled:opacity-60"
            >
              {submitting === 'assignment' ? 'Asignando…' : 'Asignar'}
            </button>
          </div>
        </form>
      </section>
    </div>
  )
}

function Field({ label, value }: { label: string; value: ReactNode }) {
  return (
    <div>
      <dt className="text-xs font-medium text-muted-foreground">{label}</dt>
      <dd className="mt-0.5 text-sm text-foreground">{value || '—'}</dd>
    </div>
  )
}
