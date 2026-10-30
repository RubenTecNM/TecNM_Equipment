import { useEffect, useMemo, useState } from 'react'
import type { ReactNode } from 'react'
import { Link } from 'react-router-dom'
import { useAuth } from '../../../shared/hooks/useAuth'
import { UiButton } from '../../../shared/components/UiButton'
import { LocationFormModal } from '../components/LocationFormModal'
import { fetchLocationTypes, locationsApi } from '../api'
import type { LocationItem, LocationTypeItem } from '../types'

type LoadState = 'loading' | 'error' | 'ready'

export function LocationsPage() {
  const [state, setState] = useState<LoadState>('loading')
  const [locations, setLocations] = useState<LocationItem[]>([])
  const [locationTypes, setLocationTypes] = useState<LocationTypeItem[]>([])
  const [search, setSearch] = useState('')
  const [debouncedSearch, setDebouncedSearch] = useState('')
  const [expanded, setExpanded] = useState<Set<number>>(new Set())
  // Avisos no fatales (p. ej. fallo al recargar
  // la lista tras guardar).
  const [error, setError] = useState('')
  const [createForm, setCreateForm] = useState<{
    open: boolean
    parentLocationId: number | null
  }>({ open: false, parentLocationId: null })

  // Las acciones de escritura solo las ve ADMIN
  // (locations.manage está asignado a ese rol).
  const { hasPermission } = useAuth()
  const canManage = hasPermission('locations.manage')

  useEffect(() => {
    locationsApi
      .list()
      .then((items) => {
        setLocations(items)
        // Raíces expandidas por defecto para ver la estructura
        // del plantel de inmediato.
        setExpanded(
          new Set(
            items
              .filter((item) => item.parentLocationId === null)
              .map((item) => item.locationId),
          ),
        )
        setState('ready')
      })
      .catch(() => setState('error'))

    fetchLocationTypes()
      .then(setLocationTypes)
      .catch(() => setLocationTypes([]))
  }, [])

  // Búsqueda con debounce (patrón del selector de usuarios
  // en la ficha del activo): evita re-filtrar en cada tecla.
  useEffect(() => {
    const timer = setTimeout(() => {
      setDebouncedSearch(search.trim().toLowerCase())
    }, 200)
    return () => clearTimeout(timer)
  }, [search])

  const typeNameByCode = useMemo(() => {
    const map = new Map<string, string>()
    for (const type of locationTypes) {
      map.set(type.code, type.name)
    }
    return map
  }, [locationTypes])

  // Lista plana -> árbol padre/hijo (la API ya ordena por code,
  // así que el mapa conserva ese orden).
  const childrenByParent = useMemo(() => {
    const map = new Map<number, LocationItem[]>()
    for (const item of locations) {
      const parentId = item.parentLocationId
      if (parentId === null) continue
      const siblings = map.get(parentId)
      if (siblings) {
        siblings.push(item)
      } else {
        map.set(parentId, [item])
      }
    }
    return map
  }, [locations])

  const roots = useMemo(
    () => locations.filter((item) => item.parentLocationId === null),
    [locations],
  )

  // Con búsqueda activa: se muestran las coincidencias más sus
  // ancestros (todos expandidos, sin importar el estado de
  // expandir/colapsar del usuario), y el resto se oculta.
  const searchResult = useMemo(() => {
    if (!debouncedSearch) return null
    const byId = new Map(locations.map((item) => [item.locationId, item]))
    const visibleIds = new Set<number>()
    for (const item of locations) {
      const matches =
        item.name.toLowerCase().includes(debouncedSearch) ||
        item.code.toLowerCase().includes(debouncedSearch) ||
        (typeNameByCode.get(item.locationTypeCode) ?? item.locationTypeCode)
          .toLowerCase()
          .includes(debouncedSearch)
      if (!matches) continue
      // Sube por los padres para mantener el contexto del árbol.
      let current: LocationItem | undefined = item
      let guard = 0
      while (current && guard++ < 20) {
        visibleIds.add(current.locationId)
        current =
          current.parentLocationId !== null
            ? byId.get(current.parentLocationId)
            : undefined
      }
    }
    return visibleIds
  }, [locations, debouncedSearch, typeNameByCode])

  // Tras guardar: recarga la lista y expande el
  // padre del nodo guardado para que el cambio
  // sea visible de inmediato.
  function handleSaved(
    _locationId: number,
    parentLocationId: number | null,
  ) {
    locationsApi
      .list()
      .then((items) => {
        setLocations(items)
        if (parentLocationId !== null) {
          setExpanded((previous) => {
            const next = new Set(previous)
            next.add(parentLocationId)
            return next
          })
        }
      })
      .catch(() => setError('No se pudieron recargar las ubicaciones.'))
  }

  function toggle(locationId: number) {
    setExpanded((previous) => {
      const next = new Set(previous)
      if (next.has(locationId)) {
        next.delete(locationId)
      } else {
        next.add(locationId)
      }
      return next
    })
  }

  function renderNode(item: LocationItem, depth: number): ReactNode {
    const children = childrenByParent.get(item.locationId) ?? []
    const hasChildren = children.length > 0
    const isExpanded = searchResult !== null || expanded.has(item.locationId)
    const typeName =
      typeNameByCode.get(item.locationTypeCode) ?? item.locationTypeCode

    return (
      <li key={item.locationId}>
        <div
          className="flex min-h-[44px] items-center gap-1.5 rounded-md px-2 text-sm transition-colors duration-150 hover:bg-muted"
          style={{ paddingLeft: `${depth * 1.5 + 0.5}rem` }}
        >
          {hasChildren ? (
            <button
              type="button"
              onClick={() => toggle(item.locationId)}
              disabled={searchResult !== null}
              aria-expanded={isExpanded}
              aria-label={`${isExpanded ? 'Contraer' : 'Expandir'} ${item.name}`}
              title={
                searchResult !== null
                  ? 'Expandido por la búsqueda'
                  : isExpanded
                    ? 'Contraer'
                    : 'Expandir'
              }
              className="min-h-[44px] min-w-[44px] shrink-0 cursor-pointer rounded-md px-0 text-secondary transition-colors duration-150 hover:bg-muted hover:text-foreground disabled:cursor-default disabled:opacity-50"
            >
              <svg
                viewBox="0 0 24 24"
                className={`h-5 w-5 transition-transform duration-150 ${
                  isExpanded ? 'rotate-90' : ''
                }`}
                fill="none"
                stroke="currentColor"
                strokeWidth="2"
                strokeLinecap="round"
                strokeLinejoin="round"
                aria-hidden="true"
              >
                <path d="M9 6l6 6-6 6" />
              </svg>
            </button>
          ) : (
            <span className="w-[44px] shrink-0" aria-hidden="true" />
          )}
          <Link
            to={`/locations/${item.locationId}`}
            className="font-medium text-foreground underline-offset-2 hover:underline"
          >
            {item.name}
          </Link>
          <span className="text-muted-foreground">{item.code}</span>
          <span className="ml-auto rounded-full bg-muted px-2.5 py-0.5 text-xs font-medium text-secondary">
            {typeName}
          </span>
          {canManage && (
            <button
              type="button"
              onClick={() =>
                setCreateForm({
                  open: true,
                  parentLocationId: item.locationId,
                })
              }
              aria-label={`Agregar sub-ubicación a ${item.name}`}
              title="Agregar sub-ubicación"
              className="min-h-[44px] min-w-[44px] shrink-0 cursor-pointer rounded-md px-0 text-secondary transition-colors duration-150 hover:bg-muted hover:text-foreground"
            >
              <svg
                viewBox="0 0 24 24"
                className="h-5 w-5"
                fill="none"
                stroke="currentColor"
                strokeWidth="2"
                strokeLinecap="round"
                aria-hidden="true"
              >
                <path d="M12 5v14M5 12h14" />
              </svg>
            </button>
          )}
        </div>
        {hasChildren && isExpanded && (
          <ul className="border-l border-border" style={{ marginLeft: `${depth * 1.5 + 1.625}rem` }}>
            {children.map((child) => renderNode(child, depth + 1))}
          </ul>
        )}
      </li>
    )
  }

  return (
    <div className="space-y-4">
      <header className="flex flex-wrap items-center justify-between gap-2">
        <h1 className="font-display text-2xl font-semibold text-foreground">
          Ubicaciones
        </h1>
        <div className="flex items-center gap-3">
          {state === 'ready' && (
            <span className="text-sm text-muted-foreground">
              {locations.length} ubicación
              {locations.length === 1 ? '' : 'es'}
            </span>
          )}
          {canManage && state === 'ready' && (
            <UiButton
              variant="primary"
              onClick={() =>
                setCreateForm({ open: true, parentLocationId: null })
              }
            >
              Nueva ubicación
            </UiButton>
          )}
        </div>
      </header>

      {error && (
        <p
          role="alert"
          className="rounded-md bg-destructive/10 px-3 py-2 text-sm text-destructive"
        >
          {error}
        </p>
      )}

      {state === 'error' && (
        <p role="alert" className="rounded-md bg-destructive/10 px-3 py-2 text-sm text-destructive">
          No se pudieron cargar las ubicaciones.
        </p>
      )}

      {state === 'loading' && (
        <p className="text-sm text-muted-foreground">Cargando ubicaciones…</p>
      )}

      {state === 'ready' && locations.length === 0 && (
        <div className="rounded-lg border border-dashed border-border p-8 text-center">
          <p className="font-medium text-foreground">Sin ubicaciones</p>
          <p className="mt-1 text-sm text-muted-foreground">
            Registra el primer edificio del plantel.
          </p>
        </div>
      )}

      {state === 'ready' && locations.length > 0 && (
        <div className="space-y-3">
          <div>
            <label
              htmlFor="location-search"
              className="mb-1 block text-sm font-medium text-foreground"
            >
              Buscar
            </label>
            <input
              id="location-search"
              type="search"
              value={search}
              onChange={(event) => setSearch(event.target.value)}
              placeholder="Nombre, código o tipo de ubicación"
              className="min-h-[44px] w-full max-w-md rounded-md border border-border bg-background px-3 text-foreground"
            />
          </div>

          {searchResult !== null && searchResult.size === 0 && (
            <p className="rounded-md border border-dashed border-border px-3 py-2 text-sm text-muted-foreground">
              Sin resultados para «{search.trim()}».
            </p>
          )}

          {searchResult === null || searchResult.size > 0 ? (
            <ul className="space-y-0.5">
              {roots
                .filter((root) => searchResult === null || searchResult.has(root.locationId))
                .map((root) => renderNode(root, 0))}
            </ul>
          ) : null}
        </div>
      )}

      <LocationFormModal
        open={createForm.open}
        mode="create"
        onClose={() => setCreateForm((form) => ({ ...form, open: false }))}
        parentLocationId={createForm.parentLocationId}
        locations={locations}
        onSaved={handleSaved}
        onConcurrencyConflict={() =>
          setError(
            'Otra persona modificó las ubicaciones. Recarga la página.',
          )
        }
      />
    </div>
  )
}
