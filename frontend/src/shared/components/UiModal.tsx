import { useEffect, useId, useRef } from 'react'
import type { ReactNode } from 'react'

interface UiModalProps {
  open: boolean
  onClose: () => void
  title: string
  children: ReactNode
}

// Diálogo nativo <dialog>: el navegador aporta el bloqueo
// de foco, Escape y el backdrop (::backdrop se estiliza en
// shared/styles/index.css), sin librerías.
export function UiModal({ open, onClose, title, children }: UiModalProps) {
  const dialogRef = useRef<HTMLDialogElement>(null)
  const titleId = useId()

  useEffect(() => {
    const dialog = dialogRef.current
    if (!dialog) return
    if (open && !dialog.open) {
      dialog.showModal()
    } else if (!open && dialog.open) {
      dialog.close()
    }
  }, [open])

  return (
    <dialog
      ref={dialogRef}
      onClose={onClose}
      onClick={(event) => {
        // Clic en el backdrop (el target es el propio diálogo)
        if (event.target === dialogRef.current) {
          onClose()
        }
      }}
      aria-labelledby={titleId}
      className="ui-modal"
    >
      <div className="flex items-start justify-between gap-4">
        <h2 id={titleId} className="font-display text-lg font-semibold text-foreground">
          {title}
        </h2>
        <button
          type="button"
          onClick={onClose}
          aria-label="Cerrar"
          className="min-h-[44px] min-w-[44px] cursor-pointer rounded-md px-2 text-secondary transition-colors duration-150 hover:bg-muted hover:text-foreground"
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
            <path d="M6 6l12 12M18 6L6 18" />
          </svg>
        </button>
      </div>
      <div className="mt-3">{children}</div>
    </dialog>
  )
}
