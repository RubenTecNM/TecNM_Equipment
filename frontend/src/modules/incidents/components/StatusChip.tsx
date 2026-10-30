interface StatusChipProps {
  statusCode: string
  statusName?: string
}

// Chip de estado del incidente. Colores del design system
// (blanco y azul institucional): rojo = falla
// activa (ABIERTO), verde = resuelto, azul tenue = en proceso
// (diagnóstico/reparación) o cerrado.
export function StatusChip({ statusCode, statusName }: StatusChipProps) {
  const chipClass = (() => {
    switch (statusCode) {
      case 'ABIERTO':
        return 'bg-destructive/10 text-destructive'
      case 'RESUELTO':
        return 'bg-success/10 text-success'
      case 'CERRADO':
        return 'bg-muted text-muted-foreground'
      default:
        return 'bg-muted text-secondary'
    }
  })()

  return (
    <span
      className={`rounded-full px-2.5 py-0.5 text-xs font-medium ${chipClass}`}
    >
      {statusName ?? statusCode}
    </span>
  )
}
