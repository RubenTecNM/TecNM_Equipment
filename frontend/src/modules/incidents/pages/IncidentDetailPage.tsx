import { useEffect, useState } from 'react'
import type { ReactNode } from 'react'
import { Link, useParams } from 'react-router-dom'
import { ApiError } from '../../../shared/api/http'
import { formatDateTime } from '../../../shared/utils/format'
import { incidentsApi } from '../api'
import { EvidenceList } from '../components/EvidenceList'
import { StatusChip } from '../components/StatusChip'
import type { IncidentDetail } from '../types'

type LoadState = 'loading' | 'not-found' | 'error' | 'ready'

// Incidentes "vivos": mientras uno de estos esté abierto
// el activo no se libera (IncidentStatusCodes.Open).
const OPEN_STATUSES = ['ABIERTO', 'EN_DIAGNOSTICO', 'EN_REPARACION']

export function IncidentDetailPage() {
  const { incidentId } = useParams<{ incidentId: string }>()
  const numericId = Number(incidentId)
  const isValidId = Number.isInteger(numericId)

  const [state, setState] = useState<LoadState>('loading')
  const [detail, setDetail] = useState<IncidentDetail | null>(null)

  useEffect(() => {
    if (!isValidId) return
    incidentsApi
      .getById(numericId)
      .then((incident) => {
        setDetail(incident)
        setState('ready')
      })
      .catch((err) => {
        setState(
          err instanceof ApiError && err.status === 404 ? 'not-found' : 'error',
        )
      })
  }, [numericId, isValidId])

  if (isValidId && state === 'loading') {
    return <p className="text-sm text-muted-foreground">Cargando incidente…</p>
  }

  if (!isValidId || state === 'not-found') {
    return (
      <div className="space-y-3">
        <p className="font-medium text-foreground">Incidente no encontrado</p>
        <Link
          to="/incidents"
          className="text-sm text-primary underline-offset-2 hover:underline"
        >
          Volver a incidentes
        </Link>
      </div>
    )
  }

  if (state === 'error' || !detail) {
    return (
      <p role="alert" className="text-sm text-destructive">
        No se pudo cargar el incidente.
      </p>
    )
  }

  const open = OPEN_STATUSES.includes(detail.statusCode)
  const assetOut =
    detail.assetStatusCode === 'CON_FALLA' ||
    detail.assetStatusCode === 'EN_MANTENIMIENTO'

  // Efecto del incidente sobre el estado del activo:
  // mientras haya un incidente abierto el activo no se
  // libera; al resolver el último vuelve a circulación.
  let assetNote = ''
  if (open && assetOut) {
    assetNote =
      'El activo está fuera de circulación mientras este incidente esté abierto.'
  } else if (!open && assetOut) {
    assetNote =
      'El activo sigue fuera de circulación: hay otros incidentes abiertos sobre él.'
  } else if (!open && !assetOut) {
    assetNote = 'El activo volvió a circulación tras la resolución de este incidente.'
  }

  return (
    <div className="space-y-6">
      <header className="space-y-1">
        <Link
          to="/incidents"
          className="text-sm text-secondary underline-offset-2 hover:underline"
        >
          Incidentes
        </Link>
        <div className="flex flex-wrap items-center gap-3">
          <h1 className="font-display text-2xl font-semibold text-foreground">
            {detail.title}
          </h1>
          <StatusChip
            statusCode={detail.statusCode}
            statusName={detail.statusName}
          />
          <span className="rounded-full bg-muted px-2.5 py-0.5 text-xs font-medium text-secondary">
            {detail.priorityName}
          </span>
          <span className="rounded-full bg-muted px-2.5 py-0.5 text-xs font-medium text-secondary">
            {detail.typeName}
          </span>
        </div>
      </header>

      <section
        aria-labelledby="activo-afectado"
        className="rounded-lg border border-border bg-card p-5"
      >
        <h2
          id="activo-afectado"
          className="mb-3 font-display text-base font-semibold text-foreground"
        >
          Activo afectado
        </h2>
        <div className="flex flex-wrap items-center gap-3">
          <Link
            to={`/assets/${detail.assetId}`}
            className="font-medium text-primary underline-offset-2 hover:underline"
          >
            {detail.assetInventoryCode}
          </Link>
          <span
            className={`rounded-full px-2.5 py-0.5 text-xs font-medium ${assetStatusClass(
              detail.assetStatusCode,
            )}`}
          >
            {assetStatusLabel(detail.assetStatusCode)}
          </span>
        </div>
        {assetNote && (
          <p className="mt-2 text-sm text-muted-foreground">{assetNote}</p>
        )}
      </section>

      <section
        aria-labelledby="detalles"
        className="rounded-lg border border-border bg-card p-5"
      >
        <h2
          id="detalles"
          className="mb-3 font-display text-base font-semibold text-foreground"
        >
          Detalles
        </h2>
        <dl className="grid gap-x-8 gap-y-3 sm:grid-cols-2">
          <Field label="Descripción" value={detail.description} />
        </dl>
      </section>

      <section
        aria-labelledby="seguimiento"
        className="rounded-lg border border-border bg-card p-5"
      >
        <h2
          id="seguimiento"
          className="mb-3 font-display text-base font-semibold text-foreground"
        >
          Seguimiento
        </h2>
        <dl className="grid gap-x-8 gap-y-3 sm:grid-cols-2">
          <Field
            label="Reportado por"
            value={`${detail.reportedByName} (${detail.reportedByUsername})`}
          />
          <Field label="Fecha de reporte" value={formatDateTime(detail.reportedAt)} />
          <Field
            label="Asignado a"
            value={
              detail.assignedToName
                ? `${detail.assignedToName} (${detail.assignedToUsername})`
                : 'Sin asignar'
            }
          />
          <Field
            label="Fecha de resolución"
            value={formatDateTime(detail.resolvedAt)}
          />
          <Field label="Diagnóstico" value={detail.diagnosis} />
          <Field label="Solución" value={detail.solution} />
        </dl>
      </section>

      <EvidenceList incidentId={numericId} />
    </div>
  )
}

function Field({ label, value }: { label: string; value: ReactNode }) {
  return (
    <div>
      <dt className="text-xs font-medium text-muted-foreground">{label}</dt>
      <dd className="mt-0.5 text-sm whitespace-pre-wrap text-foreground">
        {value || '—'}
      </dd>
    </div>
  )
}

function assetStatusLabel(statusCode: string): string {
  switch (statusCode) {
    case 'DISPONIBLE':
      return 'Disponible'
    case 'ASIGNADO':
      return 'Asignado'
    case 'EN_MANTENIMIENTO':
      return 'En mantenimiento'
    case 'CON_FALLA':
      return 'Con falla'
    case 'DADO_DE_BAJA':
      return 'Dado de baja'
    default:
      return statusCode
  }
}

function assetStatusClass(statusCode: string): string {
  switch (statusCode) {
    case 'CON_FALLA':
      return 'bg-destructive/10 text-destructive'
    case 'DISPONIBLE':
      return 'bg-success/10 text-success'
    case 'EN_MANTENIMIENTO':
    case 'ASIGNADO':
      return 'bg-muted text-secondary'
    default:
      return 'bg-muted text-muted-foreground'
  }
}
