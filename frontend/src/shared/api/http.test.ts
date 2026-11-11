import { afterEach, beforeEach, describe, expect, it, vi } from 'vitest'

// ─────────────────────────────────────────────────────────────────────────────
// Cliente HTTP (http.ts) con fetch simulado: sin backend, sin DOM real.
// Cada test carga una copia NUEVA del módulo (vi.resetModules) para que el
// estado interno (refresh en vuelo, aviso de sesión perdida) no se arrastre.
// Los cuerpos de error imitan los que serializa el backend (ver
// docs/FRONTEND-PENDIENTES-SEGURIDAD.md); los datos son ficticios.
// ─────────────────────────────────────────────────────────────────────────────

type Handler = (url: string, init: RequestInit) => Response | Promise<Response>

interface Harness {
  http: (typeof import('./http'))['http']
  authApi: (typeof import('./auth'))['authApi']
  ApiError: (typeof import('./http'))['ApiError']
  setSessionLostHandler: (typeof import('./http'))['setSessionLostHandler']
  /** URLs pedidas, en orden. */
  calls: string[]
  /** Valores asignados a window.location.href (navegaciones). */
  navigations: string[]
  sessionLost: ReturnType<typeof vi.fn>
}

function json(status: number, body: unknown, headers: Record<string, string> = {}): Response {
  return new Response(JSON.stringify(body), {
    status,
    headers: { 'Content-Type': 'application/json', ...headers },
  })
}

function empty(status: number, headers: Record<string, string> = {}): Response {
  return new Response(null, { status, headers })
}

async function setup(handler: Handler, pathname = '/'): Promise<Harness> {
  vi.resetModules()

  const calls: string[] = []
  const navigations: string[] = []

  vi.stubGlobal('document', { cookie: 'tecnm_csrf=csrf-ficticio' })
  vi.stubGlobal('window', {
    location: {
      pathname,
      get href() {
        return navigations.at(-1) ?? ''
      },
      set href(value: string) {
        navigations.push(value)
      },
    },
  })
  vi.stubGlobal(
    'fetch',
    vi.fn(async (url: string, init: RequestInit = {}) => {
      calls.push(url)
      return handler(url, init)
    }),
  )

  const httpModule = await import('./http')
  const authModule = await import('./auth')
  const sessionLost = vi.fn()
  httpModule.setSessionLostHandler(sessionLost)

  return {
    http: httpModule.http,
    authApi: authModule.authApi,
    ApiError: httpModule.ApiError,
    setSessionLostHandler: httpModule.setSessionLostHandler,
    calls,
    navigations,
    sessionLost,
  }
}

const count = (calls: string[], path: string) => calls.filter((c) => c === path).length
const delay = (ms: number) => new Promise((resolve) => setTimeout(resolve, ms))

/** Captura el error de una promesa que debe rechazar. */
async function failure(promise: Promise<unknown>): Promise<Error> {
  try {
    await promise
  } catch (error) {
    return error as Error
  }
  throw new Error('La promesa debía rechazar y se resolvió.')
}

beforeEach(() => {
  vi.unstubAllGlobals()
})

afterEach(() => {
  vi.unstubAllGlobals()
})

describe('429 (FE1)', () => {
  it('A1: un 429 en refresh no limpia la sesión ni navega a /login', async () => {
    const h = await setup((url) => {
      if (url === '/api/auth/refresh') return empty(429, { 'Retry-After': '30' })
      return empty(401)
    })

    await failure(h.http.get('/api/assets'))

    expect(h.sessionLost).not.toHaveBeenCalled()
    expect(h.navigations).toEqual([])
  })

  it('A2: un 429 en login muestra los segundos de Retry-After y no reintenta', async () => {
    const h = await setup(() =>
      json(
        429,
        {
          status: 429,
          title: 'Demasiadas solicitudes',
          detail: 'Demasiados intentos. Espera unos minutos e inténtalo de nuevo.',
        },
        { 'Retry-After': '42' },
      ),
    )

    const error = await failure(h.authApi.login('usuario.ficticio', 'clave-ficticia'))

    expect(error.message).toBe('Demasiados intentos. Espera 42 segundos.')
    expect(error).toMatchObject({ status: 429, retryAfterSeconds: 42 })
    expect(count(h.calls, '/api/auth/login')).toBe(1)
    expect(h.sessionLost).not.toHaveBeenCalled()
    expect(h.navigations).toEqual([])
  })

  it('A8: refresh 429 -> la petición falla con el mensaje de espera, sin reintento y sin limpiar sesión', async () => {
    const h = await setup((url) => {
      if (url === '/api/auth/refresh') return empty(429, { 'Retry-After': '7' })
      return empty(401)
    })

    const error = await failure(h.http.get('/api/assets'))

    expect(error.message).toBe('Demasiados intentos. Espera 7 segundos.')
    expect(error).toMatchObject({ status: 429 })
    expect(count(h.calls, '/api/assets')).toBe(1) // sin reintento
    expect(count(h.calls, '/api/auth/refresh')).toBe(1)
    expect(h.sessionLost).not.toHaveBeenCalled()
    expect(h.navigations).toEqual([])
  })

  it('A8b: refresh con error de red -> la petición falla con mensaje de conexión y la sesión sigue', async () => {
    const h = await setup((url) => {
      if (url === '/api/auth/refresh') throw new TypeError('fetch failed')
      return empty(401)
    })

    const error = await failure(h.http.get('/api/assets'))

    expect(error.message).toBe('No se pudo conectar con el servidor. Revisa tu conexión e inténtalo de nuevo.')
    expect(count(h.calls, '/api/assets')).toBe(1)
    expect(h.sessionLost).not.toHaveBeenCalled()
    expect(h.navigations).toEqual([])
  })
})

describe('refresh único y sesión terminada (FE2, FE3)', () => {
  it('A3: tres peticiones con 401 simultáneas -> UN solo refresh y tres reintentos', async () => {
    let refreshed = false
    const h = await setup(async (url) => {
      if (url === '/api/auth/refresh') {
        await delay(15) // da tiempo a que las tres peticiones lleguen al refresh
        refreshed = true
        return json(200, { userId: 1 })
      }
      return refreshed ? json(200, { ok: true }) : empty(401)
    })

    const results = await Promise.all([
      h.http.get('/api/assets'),
      h.http.get('/api/assets'),
      h.http.get('/api/assets'),
    ])

    expect(results).toHaveLength(3)
    expect(count(h.calls, '/api/auth/refresh')).toBe(1)
    expect(count(h.calls, '/api/assets')).toBe(6) // 3 iniciales + 3 reintentos
    expect(h.sessionLost).not.toHaveBeenCalled()
  })

  it('A4: refresh 401 + reintento 401 -> sesión terminada y UNA sola navegación a /login', async () => {
    const h = await setup(() => empty(401))

    const error = await failure(h.http.get('/api/assets'))

    expect(error).toMatchObject({ status: 401 })
    expect(error.message).toBe('Tu sesión expiró. Inicia sesión de nuevo.')
    expect(count(h.calls, '/api/auth/refresh')).toBe(1)
    expect(count(h.calls, '/api/assets')).toBe(2) // inicial + UN reintento
    expect(h.sessionLost).toHaveBeenCalledTimes(1)
    expect(h.navigations).toEqual(['/login'])
  })

  it('A4b: refresh 401 + reintento 200 (otra pestaña ya rotó: gracia) -> éxito, sin navegar', async () => {
    let assetCalls = 0
    const h = await setup((url) => {
      if (url === '/api/auth/refresh') return json(401, { detail: 'La sesión ya fue renovada o revocada.' })
      assetCalls += 1
      return assetCalls === 1 ? empty(401) : json(200, { ok: true })
    })

    await expect(h.http.get('/api/assets')).resolves.toEqual({ ok: true })

    expect(count(h.calls, '/api/auth/refresh')).toBe(1)
    expect(count(h.calls, '/api/assets')).toBe(2)
    expect(h.sessionLost).not.toHaveBeenCalled()
    expect(h.navigations).toEqual([])
  })

  it('A5: el reintento vuelve a dar 401 -> sesión terminada, sin segundo refresh', async () => {
    const h = await setup((url) => {
      if (url === '/api/auth/refresh') return json(200, { userId: 1 })
      return empty(401)
    })

    const error = await failure(h.http.get('/api/assets'))

    expect(error).toMatchObject({ status: 401 })
    expect(count(h.calls, '/api/auth/refresh')).toBe(1) // NO hay segundo refresh
    expect(count(h.calls, '/api/assets')).toBe(2)
    expect(h.sessionLost).toHaveBeenCalledTimes(1)
    expect(h.navigations).toEqual(['/login'])
  })

  it('A6: con navigator.locks la llamada de refresh va DENTRO del lock "tecnm-refresh"', async () => {
    const events: string[] = []
    vi.stubGlobal('navigator', {
      locks: {
        request: vi.fn(async (name: string, callback: () => Promise<unknown>) => {
          events.push(`lock:enter:${name}`)
          try {
            return await callback()
          } finally {
            events.push('lock:exit')
          }
        }),
      },
    })
    let refreshed = false
    const h = await setup((url) => {
      if (url === '/api/auth/refresh') {
        events.push('fetch:refresh')
        refreshed = true
        return json(200, { userId: 1 })
      }
      return refreshed ? json(200, { ok: true }) : empty(401)
    })
    await h.http.get('/api/assets')

    expect(events).toEqual(['lock:enter:tecnm-refresh', 'fetch:refresh', 'lock:exit'])
  })

  it('A6b: sin navigator.locks sigue funcionando con la promesa por pestaña', async () => {
    let refreshed = false
    const h = await setup((url) => {
      if (url === '/api/auth/refresh') {
        refreshed = true
        return json(200, { userId: 1 })
      }
      return refreshed ? json(200, { ok: true }) : empty(401)
    })

    await expect(h.http.get('/api/assets')).resolves.toEqual({ ok: true })
    expect(count(h.calls, '/api/auth/refresh')).toBe(1)
  })

  it('A12: download() usa la misma lógica (un refresh, un reintento, una navegación)', async () => {
    let refreshed = false
    const ok = await setup((url) => {
      if (url === '/api/auth/refresh') {
        refreshed = true
        return json(200, { userId: 1 })
      }
      return refreshed
        ? new Response('contenido', { status: 200, headers: { 'Content-Disposition': 'attachment; filename=informe.pdf' } })
        : empty(401)
    })

    const file = await ok.http.download('/api/documents/1/download')
    expect(file.fileName).toBe('informe.pdf')
    expect(count(ok.calls, '/api/auth/refresh')).toBe(1)

    const lost = await setup(() => empty(401))
    const error = await failure(lost.http.download('/api/documents/1/download'))
    expect(error).toMatchObject({ status: 401 })
    expect(count(lost.calls, '/api/auth/refresh')).toBe(1)
    expect(count(lost.calls, '/api/documents/1/download')).toBe(2)
    expect(lost.navigations).toEqual(['/login'])
  })
})

describe('mensajes (FE4)', () => {
  it('A7: 403 sin cuerpo -> mensaje genérico', async () => {
    const h = await setup(() => empty(403))

    const error = await failure(h.http.post('/api/auth/register', {}))

    expect(error.message).toBe('No tienes permiso para realizar esta acción.')
    expect(h.sessionLost).not.toHaveBeenCalled()
  })

  it.each([
    [413, { status: 413, title: 'Solicitud demasiado grande', detail: 'El archivo supera el tamaño máximo permitido.' }],
    [400, { status: 400, title: 'Regla de negocio violada', detail: 'El elemento al que se quiere adjuntar el documento no existe o fue eliminado.' }],
    [409, { status: 409, title: 'Conflicto', detail: 'Ya existe un usuario con ese nombre.' }],
  ])('A7: %i con ProblemDetails -> su detail', async (status, body) => {
    const h = await setup(() => json(status, body))

    const error = await failure(h.http.post('/api/documents', {}))

    expect(error.message).toBe(body.detail)
    expect(error).toMatchObject({ status })
  })

  it.each([
    [400, 'Los datos enviados no son válidos.'],
    [403, 'No tienes permiso para realizar esta acción.'],
    [404, 'No se encontró lo que buscas.'],
    [409, 'Hubo un conflicto con los datos actuales. Recarga e inténtalo de nuevo.'],
    [413, 'El archivo supera el tamaño máximo permitido.'],
    [500, 'Ocurrió un error en el servidor.'],
    [503, 'Ocurrió un error en el servidor.'],
  ])('A7b: %i sin mensaje útil -> texto en español por estado', async (status, expected) => {
    const h = await setup(() => empty(status))

    const error = await failure(h.http.get('/api/assets'))

    expect(error.message).toBe(expected)
  })

  it('A9: ProblemDetails AUTOMÁTICO de ASP.NET (title en inglés, errors automáticos) -> mensaje en español', async () => {
    // Cuerpo real de un JSON mal formado en POST /api/auth/login.
    const h = await setup(() =>
      json(400, {
        type: 'https://tools.ietf.org/html/rfc9110#section-15.5.1',
        title: 'One or more validation errors occurred.',
        status: 400,
        errors: {
          $: ['Expected depth to be zero at the end of the JSON payload. Path: $ | LineNumber: 0 | BytePositionInLine: 1.'],
          command: ['The command field is required.'],
        },
        traceId: '00-00000000000000000000000000000000-0000000000000000-00',
      }),
    )

    const error = await failure(h.http.post('/api/auth/register', {}))

    expect(error.message).toBe('Los datos enviados no son válidos.')
    expect(error.message).not.toMatch(/command|JSON|validation errors/i)
    expect((error as { errors?: unknown }).errors).toBeUndefined()
  })

  it('A10: error de validación NUESTRO (FluentValidation) -> el primer mensaje de campo', async () => {
    // Cuerpo real de POST /api/auth/login con campos vacíos.
    const body = {
      title: 'Error de validación',
      status: 400,
      detail: 'Se produjeron uno o más errores de validación.',
      instance: '/api/auth/register',
      errors: {
        Username: ['El usuario solo puede contener letras sin acentos, números, punto, guion y guion bajo.'],
        Email: ['El correo no tiene un formato válido.'],
      },
    }
    const h = await setup(() => json(400, body))

    const error = await failure(h.http.post('/api/auth/register', {}))

    expect(error.message).toBe(body.errors.Username[0])
    expect((error as { errors?: unknown }).errors).toEqual(body.errors)
  })

  it('un error de red en la petición inicial muestra un mensaje de conexión en español', async () => {
    const h = await setup(() => {
      throw new TypeError('Failed to fetch')
    })

    const error = await failure(h.http.get('/api/assets'))

    expect(error.message).toBe('No se pudo conectar con el servidor. Revisa tu conexión e inténtalo de nuevo.')
  })

  it('el 401 de login conserva su mensaje y no dispara refresh ni navegación', async () => {
    const h = await setup(() => json(401, { status: 401, title: 'No autorizado', detail: 'Credenciales inválidas.' }))

    const error = await failure(h.authApi.login('usuario.ficticio', 'mala'))

    expect(error.message).toBe('Credenciales inválidas.')
    expect(count(h.calls, '/api/auth/refresh')).toBe(0)
    expect(h.navigations).toEqual([])
  })
})
