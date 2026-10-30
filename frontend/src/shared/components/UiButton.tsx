import type { ButtonHTMLAttributes } from 'react'

type UiButtonVariant = 'primary' | 'secondary' | 'danger'

interface UiButtonProps extends ButtonHTMLAttributes<HTMLButtonElement> {
  variant?: UiButtonVariant
}

const variantClasses: Record<UiButtonVariant, string> = {
  primary:
    'bg-primary font-semibold text-on-primary hover:bg-secondary disabled:cursor-wait disabled:opacity-60',
  secondary:
    'border border-border bg-card text-foreground hover:bg-muted disabled:cursor-not-allowed disabled:opacity-50',
  danger:
    'bg-destructive font-semibold text-on-destructive hover:bg-destructive/90 disabled:cursor-not-allowed disabled:opacity-50',
}

export function UiButton({
  variant = 'secondary',
  className = '',
  ...rest
}: UiButtonProps) {
  return (
    <button
      type="button"
      className={`min-h-[44px] cursor-pointer rounded-md px-4 text-sm transition-colors duration-150 ${variantClasses[variant]} ${className}`}
      {...rest}
    />
  )
}
