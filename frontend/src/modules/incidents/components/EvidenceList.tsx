import { useEffect, useRef, useState } from 'react'
import { ApiError } from '../../../shared/api/http'
import { formatBytes, formatDate } from '../../../shared/utils/format'
import { UiModal } from '../../../shared/components/UiModal'
import { incidentDocumentsApi } from '../api'
import type { IncidentDocument } from '../types'

interface EvidenceListProps {
  incidentId: number
}

interface ImagePreview {
  url: string
  fileName: string
}

export function EvidenceList({ incidentId }: EvidenceListProps) {
  const [items, setItems] = useState<IncidentDocument[] | null>(null)
  const [error, setError] = useState('')
  const [preview, setPreview] = useState<ImagePreview | null>(null)
  // Blob URLs de los PDFs abiertos en pestaña nueva: se
  // revocan al desmontar (el archivo ya cargó ahí).
  const blobUrls = useRef<string[]>([])

  useEffect(() => {
    let cancelled = false
    incidentDocumentsApi
      .list(incidentId)
      .then((documents) => {
        if (!cancelled) setItems(documents)
      })
      .catch((err) => {
        if (!cancelled) {
          setError(
            err instanceof ApiError
              ? err.message
              : 'No se pudieron cargar las evidencias.',
          )
        }
      })
    return () => {
      cancelled = true
    }
  }, [incidentId])

  useEffect(
    () => () => {
      blobUrls.current.forEach((url) => URL.revokeObjectURL(url))
    },
    [],
  )

  async function handlePreview(file: IncidentDocument) {
    setError('')
    try {
      const { blob, fileName } = await incidentDocumentsApi.download(
        file.documentId,
      )
      const url = URL.createObjectURL(blob)
      if (file.mimeType === 'application/pdf') {
        const newWindow = window.open(url, '_blank')
        if (!newWindow) {
          setError('El navegador bloqueó la pestaña. Usa Descargar.')
          blobUrls.current = blobUrls.current.filter(u => u !== url)
          URL.revokeObjectURL(url)
          return
        }
        blobUrls.current.push(url)
        return
      }
      // Vista previa de imagen: el URL se revoca al cerrar.
      blobUrls.current.push(url)
      setPreview({ url, fileName })
    } catch (err) {
      setError(err instanceof ApiError ? err.message : 'No se pudo abrir la evidencia.')
    }
  }

  function handleClosePreview() {
    if (!preview) return
    URL.revokeObjectURL(preview.url)
    blobUrls.current = blobUrls.current.filter((url) => url !== preview.url)
    setPreview(null)
  }

  async function handleDownload(file: IncidentDocument) {
    setError('')
    try {
      const { blob, fileName } = await incidentDocumentsApi.download(
        file.documentId,
      )
      const url = URL.createObjectURL(blob)
      const anchor = document.createElement('a')
      anchor.href = url
      anchor.download = fileName
      anchor.click()
      // Diferir el revoke en 1s para que el navegador tenga tiempo de iniciar la descarga
      setTimeout(() => URL.revokeObjectURL(url), 1000)
    } catch (err) {
      setError(err instanceof ApiError ? err.message : 'No se pudo descargar la evidencia.')
    }
  }

  return (
    <section aria-labelledby="evidencias" className="rounded-lg border border-border bg-card p-5">
      <h2 id="evidencias" className="mb-3 font-display text-base font-semibold text-foreground">
        Evidencias
      </h2>

      {error && (
        <p
          role="alert"
          className="mb-3 rounded-md bg-destructive/10 px-3 py-2 text-sm text-destructive"
        >
          {error}
        </p>
      )}

      {items === null && !error && (
        <p className="text-sm text-muted-foreground">Cargando evidencias…</p>
      )}

      {items !== null && items.length === 0 && (
        <p className="text-sm text-muted-foreground">Sin evidencias adjuntas.</p>
      )}

      {items !== null && items.length > 0 && (
        <ul className="space-y-2">
          {items.map((file) => (
            <li
              key={file.documentId}
              className="flex flex-wrap items-center gap-x-4 gap-y-1 rounded-md border border-border px-3 py-2.5 text-sm"
            >
              <FileTypeIcon mimeType={file.mimeType} />
              <div className="min-w-0 flex-1">
                <p className="truncate font-medium text-foreground">
                  {file.fileName}
                </p>
                <p className="text-xs text-muted-foreground">
                  {formatBytes(file.sizeBytes)} · {formatDate(file.createdAt)}
                  {file.description ? ` · ${file.description}` : ''}
                </p>
              </div>
              <button
                type="button"
                onClick={() => handlePreview(file)}
                aria-label={`Ver ${file.fileName}`}
                className="min-h-[44px] cursor-pointer rounded-md border border-border px-3 text-sm text-foreground transition-colors duration-150 hover:bg-muted"
              >
                Ver
              </button>
              <button
                type="button"
                onClick={() => handleDownload(file)}
                aria-label={`Descargar ${file.fileName}`}
                className="min-h-[44px] cursor-pointer rounded-md border border-border px-3 text-sm text-foreground transition-colors duration-150 hover:bg-muted"
              >
                Descargar
              </button>
            </li>
          ))}
        </ul>
      )}

      <UiModal
        open={preview !== null}
        onClose={handleClosePreview}
        title={preview?.fileName ?? 'Evidencia'}
      >
        {preview && (
          <img
            src={preview.url}
            alt={preview.fileName}
            className="max-h-[70vh] w-full rounded-md border border-border object-contain"
          />
        )}
      </UiModal>
    </section>
  )
}

function FileTypeIcon({ mimeType }: { mimeType: string }) {
  const isImage = mimeType.startsWith('image/')
  return (
    <svg
      viewBox="0 0 24 24"
      className="h-5 w-5 shrink-0 text-secondary"
      fill="none"
      stroke="currentColor"
      strokeWidth="1.8"
      strokeLinecap="round"
      strokeLinejoin="round"
      aria-hidden="true"
    >
      {isImage ? (
        <>
          <rect x="3" y="4" width="18" height="16" rx="2" />
          <circle cx="9" cy="10" r="1.6" />
          <path d="M4 18l5-5 3 3 4-4 4 4" />
        </>
      ) : (
        <>
          <path d="M7 3h7l4 4v14H7z" />
          <path d="M14 3v4h4" />
          <path d="M10 12h5M10 16h5" />
        </>
      )}
    </svg>
  )
}
