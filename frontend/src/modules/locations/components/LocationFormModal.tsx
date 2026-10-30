import { useEffect, useMemo, useState } from 'react'
import type { FormEvent } from 'react'
import { useId } from 'react'
import { ApiError } from '../../../shared/api/http'
import { UiButton } from '../../../shared/components/UiButton'
import { UiModal } from '../../../shared/components/UiModal'
import { fetchLocationTypes, locationsApi } from '../api'
import type { LocationItem, LocationTypeItem } from '../types'

interface LocationFormModalProps {
  open: boolean
  mode: 'create' | 'edit'
  onClose: () => void
  // Padre preseleccionado al crear (nulo = raíz).
  parentLocationId: number | null
  // Datos a editar; solo se usa en modo 'edit'.
  initial?: LocationItem | null
  // Lista plana del árbol, para el selector de padre.
  locations: LocationItem[]
  // Éxito: recibe el id guardado y su padre (para
  // recargar la lista y expandir el padre).
  onSaved: (locationId: number, parentLocationId: number | null) => void
  // 409: la página recarga los datos y avisa.
  onConcurrencyConflict: () => void
}

export function LocationFormModal({
  open,
  mode,
  onClose,
  parentLocationId,
  initial,
  locations,
  onSaved,
  onConcurrencyConflict,
}: LocationFormModalProps) {
  const codeFieldId = useId()
  const nameFieldId = useId()
  const typeFieldId = useId()
  const parentFieldId = useId()

  const [code, setCode] = useState('')
  const [name, setName] = useState('')
  const [locationTypeId, setLocationTypeId] = useState('')
  const [parent, setParent] = useState('')
  const [locationTypes, setLocationTypes] = useState<LocationTypeItem[]>([])
  const [formError, setFormError] = useState('')
  const [submitting, setSubmitting] = useState(false)

  // Al abrir: carga el catálogo de tipos y resetea el
  // formulario (con el padre preseleccionado al crear,
  // o los valores actuales al editar).
  useEffect(() => {
    if (!open) return
    let cancelled = false
    fetchLocationTypes()
      .then((types) => {
        if (cancelled) return
        setLocationTypes(types)
        setFormError('')
        setSubmitting(false)
        if (mode === 'edit' && initial) {
          setCode(initial.code)
          setName(initial.name)
          const match = types.find(
            (type) => type.code === initial.locationTypeCode,
          )
          setLocationTypeId(match ? String(match.id) : '')
          setParent(
            initial.parentLocationId !== null
              ? String(initial.parentLocationId)
              : '',
          )
        } else {
          setCode('')
          setName('')
          setLocationTypeId('')
          setParent(
            parentLocationId !== null ? String(parentLocationId) : '',
          )
        }
      })
      .catch(() => {
        if (!cancelled) setLocationTypes([])
      })
    return () => {
      cancelled = true
    }
  }, [open, mode, initial, parentLocationId])

  // En edición, el padre no puede ser la propia ubicación
  // ni ninguna de sus sub-ubicaciones (el backend rechaza
  // ciclos; aquí se evitan antes de enviar).
  const parentOptions = useMemo(() => {
    if (mode !== 'edit' || !initial) return locations

    const childrenByParent = new Map<number, LocationItem[]>()
    for (const item of locations) {
      if (item.parentLocationId === null) continue
      const siblings = childrenByParent.get(item.parentLocationId)
      if (siblings) {
        siblings.push(item)
      } else {
        childrenByParent.set(item.parentLocationId, [item])
      }
    }

    const excluded = new Set<number>([initial.locationId])
    let queue = [initial.locationId]
    while (queue.length > 0) {
      const [current, ...rest] = queue
      queue = rest
      for (const child of childrenByParent.get(current) ?? []) {
        if (!excluded.has(child.locationId)) {
          excluded.add(child.locationId)
          queue.push(child.locationId)
        }
      }
    }

    return locations.filter((item) => !excluded.has(item.locationId))
  }, [mode, initial, locations])

  async function handleSubmit(event: FormEvent<HTMLFormElement>) {
    event.preventDefault()
    setFormError('')

    if (!name.trim() || !locationTypeId) {
      setFormError(
        mode === 'create'
          ? 'Código, nombre y tipo de ubicación son obligatorios.'
          : 'Nombre y tipo de ubicación son obligatorios.',
      )
      return
    }
    if (mode === 'create' && !code.trim()) {
      setFormError('Código, nombre y tipo de ubicación son obligatorios.')
      return
    }

    const resolvedParent = parent === '' ? null : Number(parent)
    setSubmitting(true)
    try {
      if (mode === 'create') {
        const created = await locationsApi.create({
          code: code.trim(),
          name: name.trim(),
          locationTypeId: Number(locationTypeId),
          parentLocationId: resolvedParent,
        })
        onSaved(created.locationId, resolvedParent)
      } else {
        if (!initial) return
        await locationsApi.update(initial.locationId, initial.version, {
          name: name.trim(),
          locationTypeId: Number(locationTypeId),
          parentLocationId: resolvedParent,
        })
        onSaved(initial.locationId, resolvedParent)
      }
      onClose()
    } catch (err) {
      if (err instanceof ApiError && err.status === 409) {
        // Otra persona modificó la ubicación: se cierran
        // los formularios y la página recarga (mismo aviso
        // que en la ficha del activo).
        onClose()
        onConcurrencyConflict()
        return
      }
      // Errores de negocio (400: código duplicado, ciclo,
      // tipo/padre inexistente) se muestran tal cual llegan.
      setFormError(
        err instanceof ApiError
          ? err.message
          : 'No se pudo guardar la ubicación.',
      )
    } finally {
      setSubmitting(false)
    }
  }

  return (
    <UiModal
      open={open}
      onClose={onClose}
      title={mode === 'create' ? 'Nueva ubicación' : 'Editar ubicación'}
    >
      <form onSubmit={handleSubmit} noValidate className="space-y-3">
        {formError && (
          <p
            role="alert"
            className="rounded-md bg-destructive/10 px-3 py-2 text-sm text-destructive"
          >
            {formError}
          </p>
        )}

        {mode === 'create' ? (
          <div>
            <label
              htmlFor={codeFieldId}
              className="mb-1 block text-sm font-medium text-foreground"
            >
              Código
            </label>
            <input
              id={codeFieldId}
              type="text"
              value={code}
              onChange={(event) => setCode(event.target.value)}
              maxLength={40}
              placeholder="Ej. ED-B-P1-LAB"
              className="min-h-[44px] w-full rounded-md border border-border bg-background px-3 text-foreground"
            />
            <p className="mt-1 text-xs text-muted-foreground">
              Identificador estable: no se puede cambiar después de crear.
            </p>
          </div>
        ) : (
          <div>
            <span className="mb-1 block text-xs font-medium text-muted-foreground">
              Código
            </span>
            <p className="text-sm text-foreground">{initial?.code}</p>
            <p className="mt-1 text-xs text-muted-foreground">
              El código no se puede editar.
            </p>
          </div>
        )}

        <div>
          <label
            htmlFor={nameFieldId}
            className="mb-1 block text-sm font-medium text-foreground"
          >
            Nombre
          </label>
          <input
            id={nameFieldId}
            type="text"
            value={name}
            onChange={(event) => setName(event.target.value)}
            maxLength={150}
            placeholder="Ej. Laboratorio de redes"
            className="min-h-[44px] w-full rounded-md border border-border bg-background px-3 text-foreground"
          />
        </div>

        <div>
          <label
            htmlFor={typeFieldId}
            className="mb-1 block text-sm font-medium text-foreground"
          >
            Tipo de ubicación
          </label>
          <select
            id={typeFieldId}
            value={locationTypeId}
            onChange={(event) => setLocationTypeId(event.target.value)}
            className="min-h-[44px] w-full cursor-pointer rounded-md border border-border bg-background px-2.5 text-foreground"
          >
            <option value="">Selecciona un tipo</option>
            {locationTypes.map((type) => (
              <option key={type.id} value={String(type.id)}>
                {type.name}
              </option>
            ))}
          </select>
        </div>

        <div>
          <label
            htmlFor={parentFieldId}
            className="mb-1 block text-sm font-medium text-foreground"
          >
            Ubicación padre
          </label>
          <select
            id={parentFieldId}
            value={parent}
            onChange={(event) => setParent(event.target.value)}
            className="min-h-[44px] w-full cursor-pointer rounded-md border border-border bg-background px-2.5 text-foreground"
          >
            <option value="">Ninguna (raíz)</option>
            {parentOptions.map((item) => (
              <option key={item.locationId} value={String(item.locationId)}>
                {item.name} ({item.code})
              </option>
            ))}
          </select>
        </div>

        <div className="flex justify-end gap-2 pt-1">
          <UiButton
            type="button"
            variant="secondary"
            onClick={onClose}
            disabled={submitting}
          >
            Cancelar
          </UiButton>
          <UiButton type="submit" variant="primary" disabled={submitting}>
            {submitting ? 'Guardando…' : 'Guardar'}
          </UiButton>
        </div>
      </form>
    </UiModal>
  )
}
