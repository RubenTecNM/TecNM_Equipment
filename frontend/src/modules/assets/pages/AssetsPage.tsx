import { useEffect, useState } from 'react'
import { Link } from 'react-router-dom'
import { assetsApi } from '../api'
import type { AssetSummary, PagedResult } from '../types'

export function AssetsPage() {
  const [result, setResult] = useState<PagedResult<AssetSummary> | null>(null)
  const [page, setPage] = useState(1)
  const [error, setError] = useState('')

  useEffect(() => {
    assetsApi
      .list(page)
      .then((result) => {
        setError('')
        setResult(result)
      })
      .catch(() => setError('No se pudieron cargar los activos.'))
  }, [page])

  return (
    <div className="space-y-4">
      <header className="flex items-center justify-between">
        <h1 className="font-display text-2xl font-semibold text-foreground">
          Activos
        </h1>
        {result && (
          <span className="text-sm text-muted-foreground">
            {result.totalCount} activo{result.totalCount === 1 ? '' : 's'}
          </span>
        )}
      </header>

      {error && (
        <p role="alert" className="rounded-md bg-destructive/10 px-3 py-2 text-sm text-destructive">
          {error}
        </p>
      )}

      {!result && !error && (
        <p className="text-sm text-muted-foreground">Cargando activos…</p>
      )}

      {result && result.items.length === 0 && (
        <div className="rounded-lg border border-dashed border-border p-8 text-center">
          <p className="font-medium text-foreground">Sin activos</p>
          <p className="mt-1 text-sm text-muted-foreground">
            Registra el primer activo del inventario.
          </p>
        </div>
      )}

      {result && result.items.length > 0 && (
        <div className="overflow-x-auto rounded-lg border border-border bg-card">
          <table className="w-full text-left text-sm">
            <thead>
              <tr className="border-b border-border bg-muted">
                <th scope="col" className="px-4 py-2.5 font-semibold text-foreground">
                  Código de inventario
                </th>
                <th scope="col" className="px-4 py-2.5 font-semibold text-foreground">
                  Serie
                </th>
                <th scope="col" className="px-4 py-2.5 font-semibold text-foreground">
                  Hostname
                </th>
              </tr>
            </thead>
            <tbody>
              {result.items.map((asset) => (
                <tr key={asset.assetId} className="border-b border-border last:border-0">
                  <td className="px-4 py-3">
                    <Link
                      to={`/assets/${asset.assetId}`}
                      className="font-medium text-primary underline-offset-2 hover:underline"
                    >
                      {asset.inventoryCode}
                    </Link>
                  </td>
                  <td className="px-4 py-3 text-secondary">
                    {asset.serialNumber ?? '—'}
                  </td>
                  <td className="px-4 py-3 text-secondary">
                    {asset.hostname ?? '—'}
                  </td>
                </tr>
              ))}
            </tbody>
          </table>
        </div>
      )}

      {result && result.totalPages > 1 && (
        <nav
          className="flex items-center justify-between"
          aria-label="Paginación de activos"
        >
          <p className="text-sm text-muted-foreground">
            Página {result.pageNumber} de {result.totalPages}
          </p>
          <div className="flex gap-2">
            <button
              type="button"
              disabled={page <= 1}
              onClick={() => setPage((p) => p - 1)}
              className="min-h-[44px] cursor-pointer rounded-md border border-border bg-card px-4 text-sm text-foreground transition-colors duration-150 hover:bg-muted disabled:cursor-not-allowed disabled:opacity-50"
            >
              Anterior
            </button>
            <button
              type="button"
              disabled={page >= result.totalPages}
              onClick={() => setPage((p) => p + 1)}
              className="min-h-[44px] cursor-pointer rounded-md border border-border bg-card px-4 text-sm text-foreground transition-colors duration-150 hover:bg-muted disabled:cursor-not-allowed disabled:opacity-50"
            >
              Siguiente
            </button>
          </div>
        </nav>
      )}
    </div>
  )
}
