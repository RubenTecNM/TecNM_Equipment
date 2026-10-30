import { useEffect, useMemo, useState } from 'react'
import type { ReactNode } from 'react'
import { Link, useNavigate, useParams } from 'react-router-dom'
import { ApiError } from '../../../shared/api/http'
import { useAuth } from '../../../shared/hooks/useAuth'
import { UiButton } from '../../../shared/components/UiButton'
import { fetchLocationTypes, locationsApi } from '../api'
import { ConfirmDeleteModal } from '../components/ConfirmDeleteModal'
import { LocationFormModal } from '../components/LocationFormModal'
import type {
  LocationDetail,
  LocationItem,
  LocationTypeItem,
} from '../types'

type LoadState = 'loading' | 'not-found' | 'error' | 'ready'

export function LocationDetailPage() {
  const { locationId } = useParams<{ locationId: string }>()
  const numericId = Number(locationId)
  const isValidId = Number.isInteger(numericId)
  const { hasPermission } = useAuth()
  const navigate = useNavigate()

  // Las acciones de escritura solo las ve ADMIN
  // (locations.manage está asignado a ADMIN).
  const canManage = hasPermission('locations.manage')

  const [state, setState] = useState<LoadState>('loading')
  const [detail, setDetail] = useState<LocationDetail | null>(null)
  const [locations, setLocations] = useState<LocationItem[]>([])
  const [locationTypes, setLocationTypes] = useState<LocationTypeItem[]>([])
  const [detailLoading, setDetailLoading] = useState(false)

  const [formOpen, setFormOpen] = useState(false)
  const [deleteOpen, setDeleteOpen] = useState(false)
  const [deleting, setDeleting] = useState(false)
  const [deleteError, setDeleteError] = useState('')
  // Avisos de página (p. ej. recarga por 409).
  const [pageError, setPageError] = useState('')

  // Recarga de la ficha (tras guardar, o
  // automática ante un 409). Solo se invoca
  // desde manejadores de eventos.
  const loadDetail = async () => {
    setDetailLoading(true)
    try {
      const [d, all, types] = await Promise.all([
        locationsApi.getById(numericId),
        locationsApi.list(),
        fetchLocationTypes(),
      ])
      setDetail(d)
      setLocations(all)
      setLocationTypes(types)
    } catch {
      // Recarga automática: si falla, el usuario
      // recarga a mano (mismo criterio que la
      // ficha del activo).
    } finally {
      setDetailLoading(false)
    }
  }

  // Un 409 significa que otra persona modificó la
  // ubicación: se recargan los datos automáticamente
  // (mismo comportamiento que la ficha del activo).
  function concurrencyConflict(): string {
    loadDetail()
    return 'Otra persona modificó esta ubicación. Recargamos los datos; revísalos e intenta de nuevo.'
  }

  // Carga inicial: setState solo dentro de los
  // callbacks de la promesa (patrón del listado
  // de activos), para evitar renders en cascada
  // desde el efecto.
  useEffect(() => {
    if (!isValidId) return
    Promise.all([
      locationsApi.getById(numericId),
      locationsApi.list(),
      fetchLocationTypes(),
    ])
      .then(([d, all, types]) => {
        setDetail(d)
        setLocations(all)
        setLocationTypes(types)
        setState('ready')
      })
      .catch((err) => {
        setState(
          err instanceof ApiError && err.status === 404
            ? 'not-found'
            : 'error',
        )
      })
  }, [numericId, isValidId])

  // Breadcrumb con enlaces: el backend devuelve solo
  // nombres, así que los ids se reconstruyen subiendo
  // por parentLocationId desde la lista completa.
  const breadcrumb = useMemo(() => {
    if (!detail) return []
    const byId = new Map(locations.map((item) => [item.locationId, item]))
    const trail: LocationItem[] = []
    let current: LocationItem | undefined = byId.get(detail.locationId)
    let guard = 0
    while (current && guard++ < 20) {
      trail.unshift(current)
      current =
        current.parentLocationId !== null
          ? byId.get(current.parentLocationId)
          : undefined
    }
    return trail
  }, [detail, locations])

  const typeName = useMemo(
    () =>
      locationTypes.find(
        (type) => type.code === detail?.locationTypeCode,
      )?.name ?? detail?.locationTypeCode ?? '',
    [locationTypes, detail],
  )

  const parentLocation = useMemo(
    () =>
      detail?.parentLocationId !== null && detail
        ? locations.find(
            (item) => item.locationId === detail.parentLocationId,
          ) ?? null
        : null,
    [detail, locations],
  )

  // Bloquea las acciones mientras hay una operación o
  // una recarga de la ficha en curso (evita enviar
  // con la versión de antes de la recarga).
  const busy = deleting || detailLoading

  function handleSaved() {
    // Tras guardar: recarga para mostrar la
    // versión y el nombre nuevos.
    loadDetail()
  }

  function handleConcurrencyConflict() {
    setPageError(concurrencyConflict())
  }

  async function handleDelete() {
    if (!detail) return
    setDeleting(true)
    setDeleteError('')
    try {
      await locationsApi.remove(detail.locationId, detail.version)
      setDeleteOpen(false)
      const parentId = detail.parentLocationId
      navigate(parentId !== null ? `/locations/${parentId}` : '/locations')
    } catch (err) {
      if (err instanceof ApiError && err.status === 409) {
        setDeleteOpen(false)
        setPageError(concurrencyConflict())
        return
      }
      // 400 del backend: tiene sub-ubicaciones o
      // activos asignados; se muestra tal cual.
      setDeleteError(
        err instanceof ApiError
          ? err.message
          : 'No se pudo eliminar la ubicación.',
      )
    } finally {
      setDeleting(false)
    }
  }

  if (isValidId && state === 'loading') {
    return <p className="text-sm text-muted-foreground">Cargando ubicación…</p>
  }

  if (!isValidId || state === 'not-found') {
    return (
      <div className="space-y-3">
        <p className="font-medium text-foreground">Ubicación no encontrada</p>
        <Link
          to="/locations"
          className="text-sm text-primary underline-offset-2 hover:underline"
        >
          Volver a ubicaciones
        </Link>
      </div>
    )
  }

  if (state === 'error' || !detail) {
    return (
      <p role="alert" className="text-sm text-destructive">
        No se pudo cargar la ubicación.
      </p>
    )
  }

  return (
    <div className="space-y-6">
      <header className="space-y-1">
        <nav aria-label="Ruta de la ubicación" className="text-sm">
          <Link
            to="/locations"
            className="text-secondary underline-offset-2 hover:underline"
          >
            Ubicaciones
          </Link>
          {breadcrumb.map((item, index) => (
            <span key={item.locationId}>
              <span className="mx-1.5 text-muted-foreground" aria-hidden="true">
                ›
              </span>
              {index < breadcrumb.length - 1 ? (
                <Link
                  to={`/locations/${item.locationId}`}
                  className="text-secondary underline-offset-2 hover:underline"
                >
                  {item.name}
                </Link>
              ) : (
                <span className="font-medium text-foreground">
                  {item.name}
                </span>
              )}
            </span>
          ))}
        </nav>
        <div className="flex flex-wrap items-center gap-3">
          <h1 className="font-display text-2xl font-semibold text-foreground">
            {detail.name}
          </h1>
          <span className="rounded-full bg-muted px-2.5 py-0.5 text-xs font-medium text-secondary">
            {typeName}
          </span>
          <span className="text-sm text-muted-foreground">{detail.code}</span>
          {canManage && (
            <span className="flex gap-2">
              <UiButton
                variant="secondary"
                onClick={() => setFormOpen(true)}
                disabled={busy}
              >
                Editar
              </UiButton>
              <UiButton
                variant="danger"
                onClick={() => {
                  setDeleteError('')
                  setDeleteOpen(true)
                }}
                disabled={busy}
              >
                Eliminar
              </UiButton>
            </span>
          )}
        </div>
      </header>

      {pageError && (
        <p
          role="alert"
          className="rounded-md bg-destructive/10 px-3 py-2 text-sm text-destructive"
        >
          {pageError}
        </p>
      )}

      <section
        aria-labelledby="datos-generales"
        className="rounded-lg border border-border bg-card p-5"
      >
        <h2
          id="datos-generales"
          className="mb-3 font-display text-base font-semibold text-foreground"
        >
          Datos generales
        </h2>
        <dl className="grid gap-x-8 gap-y-3 sm:grid-cols-2">
          <Field label="Código" value={detail.code} />
          <Field label="Tipo" value={typeName} />
          <div>
            <dt className="text-xs font-medium text-muted-foreground">
              Ubicación padre
            </dt>
            <dd className="mt-0.5 text-sm text-foreground">
              {parentLocation ? (
                <Link
                  to={`/locations/${parentLocation.locationId}`}
                  className="text-primary underline-offset-2 hover:underline"
                >
                  {parentLocation.name} ({parentLocation.code})
                </Link>
              ) : (
                '— (raíz)'
              )}
            </dd>
          </div>
          <Field label="Versión" value={String(detail.version)} />
        </dl>
      </section>

      <LocationFormModal
        open={formOpen}
        mode="edit"
        onClose={() => setFormOpen(false)}
        parentLocationId={detail.parentLocationId}
        initial={detail}
        locations={locations}
        onSaved={() => handleSaved()}
        onConcurrencyConflict={handleConcurrencyConflict}
      />

      <ConfirmDeleteModal
        open={deleteOpen}
        location={detail}
        deleting={deleting}
        error={deleteError}
        onCancel={() => setDeleteOpen(false)}
        onConfirm={handleDelete}
      />
    </div>
  )
}

function Field({
  label,
  value,
}: {
  label: string
  value: ReactNode
}): ReactNode {
  return (
    <div>
      <dt className="text-xs font-medium text-muted-foreground">{label}</dt>
      <dd className="mt-0.5 text-sm text-foreground">{value || '—'}</dd>
    </div>
  )
}
