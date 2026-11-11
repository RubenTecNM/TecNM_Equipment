/**
 * Cliente HTTP base para consumir la API (.NET 10).
 *
 * - sesión en cookies HttpOnly (tecnm_at / tecnm_rt): el JS nunca ve el token
 * - mismo origen: en desarrollo el proxy de Vite manda /api a :5112
 *   (vite.config.ts); por eso BASE_URL es '' por defecto
 * - header X-CSRF-Token (copia de la cookie tecnm_csrf) en métodos que escriben
 * - ante un 401 renueva la sesión (POST /api/auth/refresh) con UN solo refresh
 *   compartido (por pestaña y, con navigator.locks, entre pestañas) y hace UN
 *   reintento de la petición; si el reintento vuelve a dar 401, la sesión
 *   terminó: avisa a AuthProvider y navega al login (una sola vez)
 * - un 429 o un fallo de red al renovar NO cierran la sesión: la petición falla
 *   con su mensaje
 * - errores: lee `detail` de ProblemDetails (.NET); sin mensaje útil, un texto
 *   en español por estado (nunca el title ni los errors automáticos de ASP.NET)
 */

const BASE_URL = import.meta.env.VITE_API_BASE_URL ?? ''

const CSRF_COOKIE = 'tecnm_csrf'
const CSRF_HEADER = 'X-CSRF-Token'
const SAFE_METHODS = ['GET', 'HEAD', 'OPTIONS']

// Rutas que no deben disparar el refresh automático ante un 401
// (son las que abren, renuevan o leen la sesión).
const NO_REFRESH_PATHS = ['/api/auth/login', '/api/auth/refresh', '/api/auth/logout']

// Nombre del lock del navegador que serializa el refresh entre pestañas.
const REFRESH_LOCK = 'tecnm-refresh'

const CONNECTION_MESSAGE = 'No se pudo conectar con el servidor. Revisa tu conexión e inténtalo de nuevo.'
const SESSION_EXPIRED_MESSAGE = 'Tu sesión expiró. Inicia sesión de nuevo.'

// Texto en español por estado cuando el servidor no manda un mensaje útil.
const STATUS_MESSAGES: Record<number, string> = {
  400: 'Los datos enviados no son válidos.',
  401: SESSION_EXPIRED_MESSAGE,
  403: 'No tienes permiso para realizar esta acción.',
  404: 'No se encontró lo que buscas.',
  409: 'Hubo un conflicto con los datos actuales. Recarga e inténtalo de nuevo.',
  413: 'El archivo supera el tamaño máximo permitido.',
}

function rateLimitMessage(retryAfterSeconds?: number): string {
  if (retryAfterSeconds === undefined) {
    return 'Demasiados intentos. Espera unos minutos e inténtalo de nuevo.'
  }
  return `Demasiados intentos. Espera ${retryAfterSeconds} ${retryAfterSeconds === 1 ? 'segundo' : 'segundos'}.`
}

function statusMessage(status: number, retryAfterSeconds?: number): string {
  if (status === 429) return rateLimitMessage(retryAfterSeconds)
  if (status >= 500) return 'Ocurrió un error en el servidor.'
  return STATUS_MESSAGES[status] ?? 'No se pudo completar la solicitud.'
}

// Retry-After en segundos enteros (el backend nunca manda una fecha HTTP).
function readRetryAfter(response: Response): number | undefined {
  const seconds = Number(response.headers.get('Retry-After'))
  return Number.isFinite(seconds) && seconds > 0 ? Math.ceil(seconds) : undefined
}

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
  // Solo en un 429: segundos que indicó el servidor en Retry-After.
  public readonly retryAfterSeconds?: number

  constructor(
    message: string,
    status: number,
    errors?: Record<string, string[]>,
    retryAfterSeconds?: number,
  ) {
    super(message)
    this.name = 'ApiError'
    this.status = status
    this.errors = errors
    this.retryAfterSeconds = retryAfterSeconds
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

// Varias peticiones pueden acabar en "sesión terminada" a la vez: se avisa y
// se navega UNA sola vez (la navegación recarga la SPA y reinicia el aviso).
let sessionLostNotified = false

function sessionLost(): void {
  if (sessionLostNotified) return
  onSessionLost?.()
  if (!window.location.pathname.startsWith('/login')) {
    sessionLostNotified = true
    window.location.href = '/login'
  }
}

// Resultado de POST /api/auth/refresh. Solo 'ok' y 'unauthorized' permiten el
// reintento de la petición original: un 401 puede ser el caso de la gracia (otra
// pestaña acaba de rotar y dejó cookies nuevas), que el cliente no distingue del
// 401 definitivo; lo resuelve el reintento. Un 429, un fallo de red o un error del
// servidor NO cierran la sesión.
type RefreshOutcome =
  | { kind: 'ok' }
  | { kind: 'unauthorized' }
  | { kind: 'rate-limited'; retryAfterSeconds?: number }
  | { kind: 'network' }
  | { kind: 'error'; status: number }

async function callRefresh(): Promise<RefreshOutcome> {
  let response: Response
  try {
    response = await fetch(`${BASE_URL}/api/auth/refresh`, {
      method: 'POST',
      credentials: 'include',
      headers: csrfHeaders('POST'),
    })
  } catch {
    return { kind: 'network' }
  }

  if (response.ok) return { kind: 'ok' }
  if (response.status === 401) return { kind: 'unauthorized' }
  if (response.status === 429) {
    return { kind: 'rate-limited', retryAfterSeconds: readRetryAfter(response) }
  }
  return { kind: 'error', status: response.status }
}

// Entre pestañas: el refresh va dentro de un lock del navegador para que dos
// pestañas no rotén a la vez (reusar un refresh ya rotado revoca la sesión).
// Sin navigator.locks (p. ej. contexto no seguro) queda solo la promesa por pestaña.
function refreshExclusively(): Promise<RefreshOutcome> {
  return typeof navigator !== 'undefined' && 'locks' in navigator
    ? navigator.locks.request(REFRESH_LOCK, callRefresh)
    : callRefresh()
}

// Una sola renovación en vuelo por pestaña: varias peticiones que reciben 401 a
// la vez esperan la misma promesa en lugar de rotar el token N veces.
let refreshInFlight: Promise<RefreshOutcome> | null = null

function refreshSession(): Promise<RefreshOutcome> {
  refreshInFlight ??= refreshExclusively().finally(() => {
    refreshInFlight = null
  })
  return refreshInFlight
}

function csrfHeaders(method: string): Record<string, string> {
  if (SAFE_METHODS.includes(method.toUpperCase())) return {}
  const token = readCookie(CSRF_COOKIE)
  return token ? { [CSRF_HEADER]: token } : {}
}

// fetch con cookies + CSRF. Ante un 401: un refresh (compartido) y UN reintento.
async function send(path: string, init: RequestInit = {}): Promise<Response> {
  const method = init.method ?? 'GET'
  const doFetch = async () => {
    try {
      return await fetch(`${BASE_URL}${path}`, {
        ...init,
        credentials: 'include',
        headers: { ...csrfHeaders(method), ...init.headers },
      })
    } catch {
      throw new ApiError(CONNECTION_MESSAGE, 0)
    }
  }

  const response = await doFetch()
  if (response.status !== 401 || NO_REFRESH_PATHS.includes(path)) {
    return response
  }

  const outcome = await refreshSession()
  if (outcome.kind === 'rate-limited') {
    throw new ApiError(rateLimitMessage(outcome.retryAfterSeconds), 429, undefined, outcome.retryAfterSeconds)
  }
  if (outcome.kind === 'network') {
    throw new ApiError(CONNECTION_MESSAGE, 0)
  }
  if (outcome.kind === 'error') {
    throw new ApiError(statusMessage(outcome.status), outcome.status)
  }

  // 'ok' o 'unauthorized': UN reintento (si otra pestaña ya rotó, funciona).
  // Si vuelve a dar 401 la sesión terminó; nunca un segundo refresh.
  const retry = await doFetch()
  if (retry.status === 401) {
    sessionLost()
    throw new ApiError(SESSION_EXPIRED_MESSAGE, 401)
  }
  return retry
}

interface ProblemDetails {
  detail?: unknown
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
    if (Array.isArray(messages) && typeof messages[0] === 'string') {
      return messages[0]
    }
  }
  return undefined
}

// Mensaje útil = el servidor mandó `detail`. Los errores del backend (ver
// ExceptionHandlingMiddleware) siempre lo traen, en español; los ProblemDetails
// AUTOMÁTICOS de ASP.NET (JSON mal formado, binding) NO lo traen y sus `title` y
// `errors` están en inglés y son técnicos: en ese caso se usa el texto por estado.
// El 429 siempre se arma con Retry-After.
async function toApiError(response: Response): Promise<ApiError> {
  const retryAfterSeconds = readRetryAfter(response)

  // Cuerpo vacío o que no es JSON (403 de una política, error de un proxy): null.
  const problem = await response
    .json()
    .then((body: unknown) => body as ProblemDetails | null)
    .catch(() => null)

  const detail =
    typeof problem?.detail === 'string' && problem.detail.trim() ? problem.detail : undefined

  if (!problem || !detail || response.status === 429) {
    return new ApiError(
      statusMessage(response.status, retryAfterSeconds),
      response.status,
      undefined,
      retryAfterSeconds,
    )
  }

  return new ApiError(
    firstValidationError(problem.errors) ?? detail,
    response.status,
    problem.errors,
    retryAfterSeconds,
  )
}

// El 401 de las peticiones normales lo resuelve send() (refresh + un reintento
// + sesión terminada). Aquí un 401 solo puede venir de las rutas de sesión
// (credenciales inválidas, etc.): es un error normal que la pantalla muestra.
async function handleResponse<T>(response: Response): Promise<T> {
  if (!response.ok) {
    throw await toApiError(response)
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
  return handleResponse<T>(response)
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
  // Descarga binaria (evidencias): misma lógica que el resto del cliente
  // (send: refresh único, un reintento, sesión terminada). El llamador revoca
  // el blob URL tras usarlo.
  async download(path: string): Promise<DownloadedFile> {
    const response = await send(path)

    if (!response.ok) {
      throw await toApiError(response)
    }

    return {
      blob: await response.blob(),
      fileName: fileNameFrom(response.headers.get('content-disposition')),
    }
  },
}
