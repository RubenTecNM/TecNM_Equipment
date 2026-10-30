import type { ReactNode } from 'react'
import { BrandMark } from '../../shared/components/BrandMark'

/**
 * Layout para pantallas fuera de la aplicación autenticada (login,
 * recuperación de acceso): panel institucional a la izquierda con la
 * etiqueta de inventario, formulario a la derecha. En móvil solo queda
 * el formulario.
 */
export function AuthLayout({ children }: { children: ReactNode }) {
  return (
    <div className="grid min-h-screen bg-card md:grid-cols-[minmax(0,5fr)_minmax(0,6fr)]">
      <aside className="relative hidden overflow-hidden bg-primary text-on-primary md:flex md:flex-col md:justify-between md:p-10 lg:p-14">
        <div className="flex items-center gap-3">
          <BrandMark inverted />
          <span className="font-display text-lg font-semibold">TecNM Inventario</span>
        </div>

        <div className="my-12 flex justify-center">
          <AssetTag />
        </div>

        <div className="max-w-md">
          <p className="font-display text-2xl font-semibold leading-snug lg:text-3xl">
            Cada equipo del campus, con su historia completa.
          </p>
          <p className="mt-3 text-base leading-relaxed text-on-primary/75">
            Alta, ubicación, resguardo, incidentes y baja de los activos
            tecnológicos, en un solo registro auditado.
          </p>
        </div>
      </aside>

      <main className="flex items-center justify-center px-4 py-10 sm:px-8">
        <div className="w-full max-w-sm">{children}</div>
      </main>
    </div>
  )
}

// Barras del código de barras: anchos fijos (no aleatorios) para que la
// etiqueta se vea igual en cada carga.
const BARS = [2, 1, 3, 1, 1, 2, 4, 1, 2, 1, 1, 3, 2, 1, 4, 1, 1, 2, 3, 1, 2, 2, 1, 1, 3, 1, 2, 4, 1, 1, 2, 1, 3]

/** Etiqueta de inventario impresa: el objeto que lleva cada activo. */
function AssetTag() {
  return (
    <figure
      aria-hidden="true"
      className="asset-tag relative w-full max-w-[22rem] rounded-xl bg-card p-6 text-foreground"
    >
      <span className="absolute right-5 top-5 h-3.5 w-3.5 rounded-full border-2 border-border bg-primary" />
      <p className="text-xs font-medium text-muted-foreground">Tecnológico Nacional de México</p>
      <p className="mt-1 font-display text-3xl font-semibold tracking-tight text-primary">TEC-LAB-0142</p>

      <dl className="mt-5 grid grid-cols-[auto_1fr] gap-x-4 gap-y-1.5 text-sm">
        <dt className="text-muted-foreground">Equipo</dt>
        <dd className="font-medium">Computadora de escritorio</dd>
        <dt className="text-muted-foreground">Ubicación</dt>
        <dd className="font-medium">Edificio B, Laboratorio de redes</dd>
        <dt className="text-muted-foreground">Estado</dt>
        <dd>
          <span className="rounded-full bg-muted px-2 py-0.5 text-xs font-semibold text-accent">Asignado</span>
        </dd>
      </dl>

      <div className="mt-6 flex h-12 items-stretch gap-[2px]">
        {BARS.map((width, index) => (
          <span
            key={index}
            className={index % 2 === 0 ? 'bg-foreground' : 'bg-transparent'}
            style={{ width: `${width * 2}px` }}
          />
        ))}
      </div>
    </figure>
  )
}
