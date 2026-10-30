import { useEffect, useState } from 'react'
import type { FormEvent } from 'react'
import { Navigate, useLocation, useNavigate, useSearchParams } from 'react-router-dom'
import { authApi } from '../../../shared/api/auth'
import type { ExternalProvider } from '../../../shared/api/auth'
import { ApiError } from '../../../shared/api/http'
import { BrandMark } from '../../../shared/components/BrandMark'
import { useAuth } from '../../../shared/hooks/useAuth'

// Códigos que devuelve el callback federado en /login?error=...
// (ExternalLoginCallbackHandler).
const EXTERNAL_ERRORS: Record<string, string> = {
  sin_cuenta:
    'Tu cuenta institucional no está registrada en el inventario. Pide a un administrador que te dé de alta con ese correo.',
  dominio_no_permitido: 'Usa tu cuenta de Google institucional del TecNM.',
  cuenta_inactiva: 'Tu cuenta está desactivada. Contacta a un administrador.',
  federado_fallido: 'No se pudo completar el inicio de sesión con el proveedor. Intenta de nuevo.',
}

const PROVIDER_LABELS: Record<ExternalProvider, string> = {
  microsoft: 'Continuar con Microsoft',
  google: 'Continuar con Google',
}

export function LoginPage() {
  const { session, login } = useAuth()
  const navigate = useNavigate()
  const location = useLocation()
  const [searchParams] = useSearchParams()

  // Ruta a la que quería ir el usuario antes de que RequireAuth lo mandara aquí.
  const from = (location.state as { from?: string } | null)?.from ?? '/'

  const [usernameOrEmail, setUsernameOrEmail] = useState('')
  const [password, setPassword] = useState('')
  const [error, setError] = useState(() => EXTERNAL_ERRORS[searchParams.get('error') ?? ''] ?? '')
  const [isSubmitting, setIsSubmitting] = useState(false)
  const [providers, setProviders] = useState<ExternalProvider[]>([])

  useEffect(() => {
    authApi.providers().then(setProviders).catch(() => setProviders([]))
  }, [])

  if (session) {
    return <Navigate to={from} replace />
  }

  async function handleSubmit(event: FormEvent<HTMLFormElement>) {
    event.preventDefault()
    setError('')

    if (!usernameOrEmail.trim() || !password) {
      setError('Ingresa tu usuario y contraseña.')
      return
    }

    setIsSubmitting(true)
    try {
      await login(usernameOrEmail.trim(), password)
      navigate(from, { replace: true })
    } catch (err) {
      setError(
        err instanceof ApiError
          ? err.message
          : 'No se pudo conectar con el servidor. Verifica que la API esté activa.',
      )
    } finally {
      setIsSubmitting(false)
    }
  }

  return (
    <div>
      <div className="mb-8 md:hidden">
        <BrandMark className="h-10 w-10" />
      </div>

      <h1 className="font-display text-2xl font-semibold text-foreground">Inicia sesión</h1>
      <p className="mt-1.5 text-sm text-muted-foreground">
        Inventario de activos tecnológicos del TecNM.
      </p>

      {providers.length > 0 && (
        <>
          <div className="mt-8 space-y-3">
            {providers.map((provider) => (
              <a
                key={provider}
                href={authApi.externalLoginUrl(provider, from)}
                className="flex min-h-[48px] w-full items-center justify-center gap-3 rounded-lg border border-border bg-card px-4 text-sm font-semibold text-foreground transition-colors duration-150 hover:border-accent hover:bg-muted"
              >
                <ProviderLogo provider={provider} />
                {PROVIDER_LABELS[provider]}
              </a>
            ))}
          </div>

          <div className="my-6 flex items-center gap-3 text-xs text-muted-foreground" role="separator">
            <span className="h-px flex-1 bg-border" />
            o con tu usuario del sistema
            <span className="h-px flex-1 bg-border" />
          </div>
        </>
      )}

      <form onSubmit={handleSubmit} className={providers.length > 0 ? '' : 'mt-8'} noValidate>
        <div className="mb-4">
          <label htmlFor="username" className="mb-1.5 block text-sm font-medium text-foreground">
            Usuario o correo
          </label>
          <input
            id="username"
            type="text"
            autoComplete="username"
            autoFocus
            value={usernameOrEmail}
            onChange={(event) => setUsernameOrEmail(event.target.value)}
            className="min-h-[48px] w-full rounded-lg border border-border bg-card px-3.5 text-foreground placeholder:text-muted-foreground/70 focus:border-accent"
            placeholder="tu.usuario@tecnm.mx"
          />
        </div>

        <div className="mb-6">
          <label htmlFor="password" className="mb-1.5 block text-sm font-medium text-foreground">
            Contraseña
          </label>
          <input
            id="password"
            type="password"
            autoComplete="current-password"
            value={password}
            onChange={(event) => setPassword(event.target.value)}
            className="min-h-[48px] w-full rounded-lg border border-border bg-card px-3.5 text-foreground placeholder:text-muted-foreground/70 focus:border-accent"
          />
        </div>

        {error && (
          <p role="alert" className="mb-4 rounded-lg bg-destructive/10 px-3.5 py-2.5 text-sm text-destructive">
            {error}
          </p>
        )}

        <button
          type="submit"
          disabled={isSubmitting}
          className="min-h-[48px] w-full cursor-pointer rounded-lg bg-primary font-semibold text-on-primary transition-colors duration-150 hover:bg-secondary disabled:cursor-wait disabled:opacity-60"
        >
          {isSubmitting ? 'Iniciando sesión…' : 'Iniciar sesión'}
        </button>
      </form>
    </div>
  )
}

// Logotipos oficiales simplificados, como piden las guías de marca de
// cada proveedor para sus botones de inicio de sesión.
function ProviderLogo({ provider }: { provider: ExternalProvider }) {
  if (provider === 'microsoft') {
    return (
      <svg viewBox="0 0 21 21" className="h-5 w-5" aria-hidden="true">
        <rect x="1" y="1" width="9" height="9" fill="#f25022" />
        <rect x="11" y="1" width="9" height="9" fill="#7fba00" />
        <rect x="1" y="11" width="9" height="9" fill="#00a4ef" />
        <rect x="11" y="11" width="9" height="9" fill="#ffb900" />
      </svg>
    )
  }

  return (
    <svg viewBox="0 0 48 48" className="h-5 w-5" aria-hidden="true">
      <path fill="#EA4335" d="M24 9.5c3.5 0 6.6 1.2 9 3.5l6.7-6.7C35.6 2.4 30.2 0 24 0 14.6 0 6.6 5.4 2.6 13.3l7.8 6C12.3 13.6 17.6 9.5 24 9.5z" />
      <path fill="#4285F4" d="M46.1 24.5c0-1.6-.1-3.1-.4-4.5H24v9h12.4c-.5 2.9-2.2 5.3-4.6 6.9l7.5 5.8c4.4-4 6.8-10 6.8-17.2z" />
      <path fill="#FBBC05" d="M10.4 28.7c-.5-1.4-.8-3-.8-4.7s.3-3.3.8-4.7l-7.8-6C1 16.5 0 20.1 0 24s1 7.5 2.6 10.7l7.8-6z" />
      <path fill="#34A853" d="M24 48c6.5 0 11.9-2.1 15.9-5.8l-7.5-5.8c-2.1 1.4-4.8 2.3-8.4 2.3-6.4 0-11.7-4.1-13.6-9.8l-7.8 6C6.6 42.6 14.6 48 24 48z" />
    </svg>
  )
}
