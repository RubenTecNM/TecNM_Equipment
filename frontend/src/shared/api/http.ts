/**
 * Cliente HTTP base para consumir la API (.NET 10).
 *
 * - sesión en cookies HttpOnly (tecnm_at / tecnm_rt): el JS nunca ve el token
 * - mismo origen: en desarrollo el proxy de Vite manda /api a :5112
 *   (vite.config.ts); por eso BASE_URL es '' por defecto
 * - header X-CSRF-Token (copia de la cookie tecnm_csrf) en métodos que escriben
 * - ante un 401 renueva la sesión una sola vez (POST /api/auth/refresh) y
 *   reintenta; si no se puede, avisa a AuthProvider y redirige al login
 * - errores: lee `detail` de ProblemDetails (.NET)
 */

const BASE_URL = import.meta.env.VITE_API_BASE_URL ?? ''

const CSRF_COOKIE = 'tecnm_csrf'
const CSRF_HEADER = 'X-CSRF-Token'
const SAFE_METHODS = ['GET', 'HEAD', 'OPTIONS']

// Rutas que no deben disparar el refresh automático ante un 401
// (son las que abren, renuevan o leen la sesión).
const NO_REFRESH_PATHS = ['/api/auth/login', '/api/auth/refresh', '/api/auth/logout']

/** Perfil de la sesión (GET /api/auth/me); no incluye el token. */
export interface AuthSession {
  userId: number
  username: string
  email: string
  firstName: string
  lastName: string
  roleCode: string
  roleName: string
  permissions: string[]
}

export interface DownloadedFile {
  blob: Blob
  fileName: string
}

// Nombre de archivo del header Content-Disposition
// ("attachment; filename=foto.jpg"); "documento" si no viene.
function fileNameFrom(disposition: string | null): string {
  const match = /filename\*?=(?:UTF-\d''|")?([^";]+)/i.exec(disposition ?? '')
  if (!match?.[1]) return 'documento'
  try {
    return decodeURIComponent(match[1])
  } catch {
    return match[1]
  }
}

export class ApiError extends Error {
  public readonly status: number
  // Errores de validación por campo. FluentValidation
  // los devuelve en la extensión "errors" de
  // ProblemDetails (el "detail" en ese caso es un
  // texto genérico: "Se produjeron uno o más errores
  // de validación.").
  public readonly errors?: Record<string, string[]>

  constructor(
    message: string,
    status: number,
    errors?: Record<string, string[]>,
  ) {
    super(message)
    this.name = 'ApiError'
    this.status = status
    this.errors = errors
  }
}

function readCookie(name: string): string | null {
  const prefix = `${name}=`
  const entry = document.cookie.split('; ').find((c) => c.startsWith(prefix))
  return entry ? decodeURIComponent(entry.slice(prefix.length)) : null
}

// AuthProvider registra aquí cómo limpiar su estado cuando la sesión
// se pierde (refresh fallido), sin que este módulo dependa de React.
let onSessionLost: (() => void) | null = null

export function setSessionLostHandler(handler: (() => void) | null): void {
  onSessionLost = handler
}

function sessionLost(): void {
  onSessionLost?.()
  if (!window.location.pathname.startsWith('/login')) {
    window.location.href = '/login'
  }
}

// Una sola renovación en vuelo: varias peticiones que reciben 401 a la
// vez esperan el mismo refresh en lugar de rotar el token N veces.
let refreshInFlight: Promise<boolean> | null = null

function refreshSession(): Promise<boolean> {
  refreshInFlight ??= fetch(`${BASE_URL}/api/auth/refresh`, {
    method: 'POST',
    credentials: 'include',
    headers: csrfHeaders('POST'),
  })
    .then((response) => response.ok)
    .catch(() => false)
    .finally(() => {
      refreshInFlight = null
    })
  return refreshInFlight
}

function csrfHeaders(method: string): Record<string, string> {
  if (SAFE_METHODS.includes(method.toUpperCase())) return {}
  const token = readCookie(CSRF_COOKIE)
  return token ? { [CSRF_HEADER]: token } : {}
}

// fetch con cookies + CSRF y un reintento tras renovar la sesión.
async function send(path: string, init: RequestInit = {}): Promise<Response> {
  const method = init.method ?? 'GET'
  const doFetch = () =>
    fetch(`${BASE_URL}${path}`, {
      ...init,
      credentials: 'include',
      headers: { ...csrfHeaders(method), ...init.headers },
    })

  const response = await doFetch()
  if (response.status !== 401 || NO_REFRESH_PATHS.includes(path)) {
    return response
  }

  // Aunque el refresh falle se reintenta una vez: otra pestaña pudo
  // haber rotado el token y dejado ya cookies nuevas.
  await refreshSession()
  return doFetch()
}

interface ProblemDetails {
  detail?: string
  title?: string
  errors?: Record<string, string[]>
}

// El primer mensaje de validación es el más útil
// para mostrar en pantalla; sin esto, el usuario
// vería el texto genérico de FluentValidation.
function firstValidationError(
  errors?: Record<string, string[]>,
): string | undefined {
  if (!errors) return undefined
  for (const messages of Object.values(errors)) {
    if (messages.length > 0) {
      return messages[0]
    }
  }
  return undefined
}

async function extractProblem(
  response: Response,
): Promise<{
  message: string
  errors?: Record<string, string[]>
}> {
  try {
    const problem = (await response.json()) as ProblemDetails
    return {
      message:
        firstValidationError(problem.errors) ??
        problem.detail ??
        problem.title ??
        `Error ${response.status}`,
      errors: problem.errors,
    }
  } catch {
    return { message: `Error ${response.status}` }
  }
}

// En las rutas de sesión un 401 es un error normal (credenciales
// inválidas, refresh vencido) que la pantalla muestra; no se redirige.
async function handleResponse<T>(response: Response, path = ''): Promise<T> {
  if (response.status === 401 && !NO_REFRESH_PATHS.includes(path)) {
    sessionLost()
    throw new ApiError('Tu sesión expiró. Inicia sesión de nuevo.', response.status)
  }

  if (!response.ok) {
    const problem = await extractProblem(response)
    throw new ApiError(problem.message, response.status, problem.errors)
  }

  if (response.status === 204) {
    return undefined as T
  }

  return (await response.json()) as T
}

async function request<T>(path: string, init?: RequestInit): Promise<T> {
  const response = await send(path, {
    ...init,
    headers: { 'Content-Type': 'application/json', ...init?.headers },
  })
  return handleResponse<T>(response, path)
}

export const http = {
  get<T>(path: string): Promise<T> {
    return request<T>(path)
  },
  post<T>(path: string, body?: unknown): Promise<T> {
    return request<T>(path, {
      method: 'POST',
      body: body === undefined ? undefined : JSON.stringify(body),
    })
  },
  put<T>(path: string, body?: unknown): Promise<T> {
    return request<T>(path, {
      method: 'PUT',
      body: body === undefined ? undefined : JSON.stringify(body),
    })
  },
  // PATCH (cambios de estado): mismo cuerpo JSON que PUT.
  patch<T>(path: string, body?: unknown): Promise<T> {
    return request<T>(path, {
      method: 'PATCH',
      body: body === undefined ? undefined : JSON.stringify(body),
    })
  },
  delete<T>(path: string): Promise<T> {
    return request<T>(path, { method: 'DELETE' })
  },
  // POST multipart (evidencias): el navegador fija Content-Type
  // con el boundary del FormData, así que no se envía desde aquí.
  postForm<T>(path: string, formData: FormData): Promise<T> {
    return send(path, { method: 'POST', body: formData }).then((response) =>
      handleResponse<T>(response),
    )
  },
  // Descarga binaria (evidencias): mismo manejo de 401/403 que el
  // resto del cliente. El llamador revoca el blob URL tras usarlo.
  async download(path: string): Promise<DownloadedFile> {
    const response = await send(path)

    if (response.status === 401) {
      sessionLost()
      throw new ApiError('Tu sesión expiró. Inicia sesión de nuevo.', response.status)
    }

    if (!response.ok) {
      const problem = await extractProblem(response)
      throw new ApiError(problem.message, response.status, problem.errors)
    }

    return {
      blob: await response.blob(),
      fileName: fileNameFrom(response.headers.get('content-disposition')),
    }
  },
}
