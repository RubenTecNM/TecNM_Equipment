interface BrandMarkProps {
  className?: string
  // Sobre fondo navy (panel del login) el contraste se invierte.
  inverted?: boolean
}

/** Marca: etiqueta de inventario con su perforación. */
export function BrandMark({ className = 'h-8 w-8', inverted = false }: BrandMarkProps) {
  const fill = inverted ? 'var(--color-card)' : 'var(--color-primary)'
  const ink = inverted ? 'var(--color-primary)' : 'var(--color-card)'

  return (
    <svg viewBox="0 0 32 32" className={className} role="img" aria-label="TecNM Inventario">
      <rect width="32" height="32" rx="7" fill={fill} />
      <circle cx="23.5" cy="8.5" r="2.25" fill={ink} />
      <path d="M8 15h12M8 19.5h16M8 24h9" stroke={ink} strokeWidth="2.25" strokeLinecap="round" />
    </svg>
  )
}
