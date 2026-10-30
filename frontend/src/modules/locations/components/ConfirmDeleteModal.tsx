import { UiButton } from '../../../shared/components/UiButton'
import { UiModal } from '../../../shared/components/UiModal'
import type { LocationItem } from '../types'

interface ConfirmDeleteModalProps {
  open: boolean
  location: LocationItem | null
  deleting: boolean
  // Errores de negocio del backend (400: tiene
  // sub-ubicaciones o activos asignados), tal cual llegan.
  error: string
  onCancel: () => void
  onConfirm: () => void
}

export function ConfirmDeleteModal({
  open,
  location,
  deleting,
  error,
  onCancel,
  onConfirm,
}: ConfirmDeleteModalProps) {
  return (
    <UiModal open={open} onClose={onCancel} title="Eliminar ubicación">
      {location && (
        <div className="space-y-4">
          <p className="text-sm text-secondary">
            ¿Eliminar{' '}
            <span className="font-medium text-foreground">{location.name}</span>{' '}
            ({location.code})? La ubicación dejará de estar disponible para
            movimientos y asignaciones.
          </p>

          {error && (
            <p
              role="alert"
              className="rounded-md bg-destructive/10 px-3 py-2 text-sm text-destructive"
            >
              {error}
            </p>
          )}

          <div className="flex justify-end gap-2">
            <UiButton
              type="button"
              variant="secondary"
              onClick={onCancel}
              disabled={deleting}
            >
              Cancelar
            </UiButton>
            <UiButton
              type="button"
              variant="danger"
              onClick={onConfirm}
              disabled={deleting}
            >
              {deleting ? 'Eliminando…' : 'Eliminar'}
            </UiButton>
          </div>
        </div>
      )}
    </UiModal>
  )
}
