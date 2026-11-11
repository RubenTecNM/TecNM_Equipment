# Pendientes de la SPA tras el Lote 5 (seguridad)

Tareas del frontend que se desprenden de los cambios del backend. Los códigos y
mensajes son **exactos**. Las referencias son a `frontend/src/shared/api/http.ts`
(cliente HTTP) y a `http.test.ts` a su lado (`npm test`, Vitest, `fetch` simulado).

## Estado (rama `fix/frontend-seguridad`)

| Tarea | Estado | Dónde |
|---|---|---|
| 429 en login | **Resuelto**: mensaje con los segundos de `Retry-After`, sin reintento | `toApiError`, `rateLimitMessage` |
| 429 en refresh | **Resuelto**: no cierra la sesión, no navega y la petición falla con el mensaje de espera | `send`, `callRefresh` |
| Refresh único por pestaña | **Resuelto** (ya existía) | `refreshSession` |
| Refresh único entre pestañas | **Resuelto**: `navigator.locks.request('tecnm-refresh', …)`; sin `navigator.locks`, solo la promesa por pestaña | `refreshExclusively` |
| 401: un refresh y UN reintento; sesión terminada y navegación una sola vez | **Resuelto** | `send`, `sessionLost` |
| `download()` con la misma lógica de 401 | **Resuelto** (se quitó su rama propia) | `download` |
| Mensajes en español sin `title` ni `errors` automáticos de ASP.NET | **Resuelto** | `toApiError`, `STATUS_MESSAGES` |
| 403 sin cuerpo, 413 y mensajes de Upload/Register | **Resuelto en la capa compartida** | `toApiError` |
| Pantallas de subida de documentos y de alta de usuarios | **Pendiente**: no existen (ver sección 8) | — |

El detalle de cada caso sigue abajo; las notas "**Resuelto**" indican el cambio.

## 1. 429 en login y refresh

| Dónde | Respuesta |
|---|---|
| `POST /api/auth/login` | `429`, cabecera `Retry-After` (segundos) |
| `POST /api/auth/refresh` | `429`, cabecera `Retry-After` (segundos) |

Cuerpo (`application/problem+json`, igual en ambos y en los dos tipos de límite):

```json
{ "status": 429, "title": "Demasiadas solicitudes",
  "detail": "Demasiados intentos. Espera unos minutos e inténtalo de nuevo." }
```

Causas: más de 10 logins por minuto desde una IP; **5 fallos en 15 minutos para el
mismo identificador** (bloquea 15 minutos, **incluso con la contraseña correcta** y
exista o no la cuenta); más de 30 refresh por minuto desde una IP.

- **Login — resuelto.** `ApiError.message` = "Demasiados intentos. Espera N segundos." (N de
  `Retry-After`; "1 segundo" en singular; sin la cabecera, "…Espera unos minutos e inténtalo de
  nuevo."). `ApiError.retryAfterSeconds` lleva el número. No hay reintento automático.
  **Pendiente (UI):** deshabilitar el botón de `Login.tsx` el tiempo de `retryAfterSeconds`
  (hoy solo se muestra el mensaje).
- **Refresh — resuelto.** Un 429 en refresh NO es sesión terminada: `send()` no reintenta la
  petición, no llama a `sessionLost()` y la petición falla con el mensaje de espera.
  (Antes, `refreshSession()` devolvía `response.ok`, `send()` reintentaba y el 401 siguiente cerraba
  la sesión.) Un fallo de red o un error del servidor al renovar tampoco cierran la sesión: la
  petición falla con "No se pudo conectar con el servidor. Revisa tu conexión e inténtalo de nuevo."
  o con el texto de su estado.

## 2. Un solo refresh concurrente

- **Resuelto.** Una sola promesa de refresh por pestaña (`refreshSession`) y, entre pestañas,
  la llamada va dentro de `navigator.locks.request('tecnm-refresh', …)` (`refreshExclusively`).
  Reusar un refresh ya rotado **después de 5 segundos** (`Auth__RefreshReuseGraceSeconds`) se
  interpreta como robo y **revoca toda la sesión**; dentro de los 5 segundos no pasa nada.
- Límite conocido: `navigator.locks` solo existe en contextos seguros (HTTPS o `localhost`). Sin él
  queda solo la promesa por pestaña. Además, el lock **serializa** los refresh, no los fusiona: la
  segunda pestaña hace el suyo después (rota otra vez con la cookie ya nueva, lo cual es válido).
- No lanzar refresh proactivos ni duplicados desde pestañas distintas (hoy no hay ninguno).

## 3. Qué significa cada 401

| Petición | Mensaje (`detail`) | Significado | Qué hacer |
|---|---|---|---|
| Cualquier endpoint de negocio | (el 401 de autenticación no trae mensaje) | JWT caducado (15 min) **o** el usuario cambió: desactivado, borrado o con otro rol | **Intentar refresh una vez** y repetir la petición |
| `POST /api/auth/refresh` | "La sesión expiró. Inicia sesión de nuevo." | Sin cookie, token inexistente o caducado | **Sesión terminada**: ir al login |
| `POST /api/auth/refresh` | "La cuenta no está activa." | Usuario desactivado o borrado (cookies borradas) | **Sesión terminada** |
| `POST /api/auth/refresh` | "La sesión ya fue renovada o revocada." | Token ya rotado. Fuera de la gracia de 5 s: cookies borradas y familia revocada | **Sesión terminada**. Dentro de la gracia (otra pestaña acaba de rotar): **no** se borran cookies; repetir la petición original con las cookies nuevas (ya lo hace `http.ts:133-136`) |
| `GET /api/auth/me` | "La cuenta no está activa." | Usuario ya no activo | **Sesión terminada** |
| `POST /api/auth/login` | "Credenciales inválidas." | Mismo mensaje para usuario inexistente, inactivo, borrado, contraseña incorrecta o ambigüedad | Mostrarlo; **no** redirigir |

**Resuelto** en `send()`: un 401 en una petición normal hace **un** refresh (compartido) y **un**
reintento. Tras un refresh `200` **o** `401` se reintenta la petición (el 401 del refresh puede ser
el caso de la gracia, que el cliente no distingue del definitivo). Si el reintento da `401`:
sesión terminada (se limpia el estado de `AuthProvider` y se navega a `/login`, **una sola vez**
aunque varias peticiones fallen a la vez) y **nunca** un segundo refresh. Un `429`, un fallo de red
o un error del servidor en el refresh hacen fallar la petición sin tocar la sesión.
`/api/auth/login` y `/api/auth/logout` no disparan refresh: su 401 se muestra con su `detail`.

- **Pendiente:** si el refresh devuelve 200 tras un cambio de rol, el token nuevo lleva el rol
  nuevo; **recargar `GET /api/auth/me`** (perfil y permisos) en ese caso. Hoy no se hace.
- Hasta 30 s después de desactivar un usuario su JWT puede seguir valiendo (caché de estado).

## 4. Subida de documentos (`POST /api/documents`)

**413** (`application/problem+json`), con o sin `Content-Length`:

```json
{ "status": 413, "title": "Solicitud demasiado grande",
  "detail": "El archivo supera el tamaño máximo permitido." }
```

Ocurre si la petición supera 5 MB + 64 KB. **Resuelto** en la capa compartida: un 413 muestra su
`detail`; sin cuerpo (p. ej. un proxy) muestra "El archivo supera el tamaño máximo permitido."
**Pendiente (cuando exista la pantalla):** validar en el cliente `file.size <= 5242880` (5 MB) y
`> 0` antes de enviar.

**400** (validación; llegan en `errors` y `http.ts` muestra el primero):

- "Debe adjuntar un archivo."
- "El archivo está vacío."
- "El archivo excede el límite de 5 MB."
- "Tipo de archivo no permitido. Formatos aceptados: PDF, JPEG y PNG."
- "El nombre del archivo no puede exceder 255 caracteres."
- "OwnerType debe ser uno de: INCIDENT, MAINTENANCE, ASSET_DISPOSAL, ACQUISITION."

**400** de regla de negocio (`detail`):

- "El elemento al que se quiere adjuntar el documento no existe o fue eliminado."
  (dueño inexistente, borrado, o incidente/mantenimiento cuyo **activo** está borrado)

**Comportamiento nuevo:**

- El tipo lo decide el **contenido**, no la extensión: un `.pdf` que no es PDF se rechaza,
  y un `.jpg` que en realidad es PNG se guarda como PNG. No confiar en `file.type`.
- La respuesta devuelve `fileName` **sanitizado** (sin rutas) y `mimeType` **detectado**:
  usar esos valores, no los del `File` local.

**Descarga** (`GET /api/documents/{id}/download`):

- **404** "El archivo no está disponible." (el registro existe pero el archivo falta o no es
  seguro). **Resuelto:** `download()` muestra el `detail` (y usa la misma lógica de 401 que el resto).
- `Content-Type` = tipo detectado; `Content-Disposition: attachment` con `filename*` UTF-8
  (`fileNameFrom` lo interpreta).

## 5. Alta de usuarios (`POST /api/auth/register`)

Todos los textos de la tabla llegan por `errors` (400) salvo el 409 y el 403. **Resuelto** en la
capa compartida: se muestra el primer mensaje de campo (los errores de validación del backend
traen `detail`, ver sección 7). **Pendiente:** no existe pantalla de alta de usuarios.

| Caso | Mensaje |
|---|---|
| Usuario con acentos, ñ, espacios u otros símbolos | "El usuario solo puede contener letras sin acentos, números, punto, guion y guion bajo." |
| Usuario con `@` | "El usuario no puede contener '@'." |
| Usuario repetido (sin distinguir mayúsculas) | "Ese nombre de usuario ya está en uso." |
| Correo con acentos o ñ | "El correo solo puede contener caracteres ASCII (sin acentos ni ñ)." |
| Correo con formato no válido | "El correo no tiene un formato válido." |
| Correo repetido (sin distinguir mayúsculas) | "Ese correo ya está registrado." |
| Contraseña corta | "La contraseña debe tener al menos 8 caracteres." |
| Contraseña > 72 bytes | "La contraseña no puede superar 72 bytes (las letras con acento, la ñ y los símbolos ocupan más de un byte)." |
| **409**: dos altas simultáneas con el mismo nombre | "Ya existe un usuario con ese nombre." |
| **409**: dos altas simultáneas con el mismo correo | "Ya existe un usuario con ese correo." |
| **403**: sin permiso `users.manage` | ver sección 6 |

- El usuario y el correo se **guardan en minúsculas**; la respuesta devuelve el valor normalizado.
- Login: es indiferente a mayúsculas. Un valor con `@` se busca solo como correo. **400** si el
  identificador supera 254 caracteres ("El usuario o correo no puede superar 254 caracteres.") o la
  contraseña supera 1024 bytes ("La contraseña no puede superar 1024 bytes."): limitar en el cliente.

## 6. 403 genérico

Hay **tres** formas de 403; la SPA debe tratarlas sin depender del texto del rol o del permiso:

| Origen | Cuerpo |
|---|---|
| Regla de negocio (p. ej. documentos por tipo) | `{ "status": 403, "title": "Acceso prohibido", "detail": "No tienes permiso para realizar esta acción." }` |
| Política de permiso de un endpoint (`[Authorize(Policy …)]`) | **Sin cuerpo** (verificado). `http.ts` mostraría "Error 403". |
| CSRF | `{ "status": 403, "title": "Acceso prohibido", "detail": "Token CSRF ausente o inválido. Recarga la página e intenta de nuevo." }` |

**Resuelto:** un 403 sin cuerpo muestra "No tienes permiso para realizar esta acción."; el 403 con
el genérico muestra su `detail`; el de CSRF muestra el suyo ("…Recarga la página e intenta de
nuevo."). La API ya no revela el rol, el tipo de documento ni el permiso exigido.

## 7. Cómo elige el cliente el mensaje (`toApiError`)

Nunca se usa el `title` ni se vuelca el JSON. El discriminador es **la presencia de `detail`**:

| Origen | Forma real | Mensaje |
|---|---|---|
| Error del backend (middleware) | `{"title":"Error de validación","status":400,"detail":"Se produjeron…","instance":"…","errors":{"Username":["…"]}}`, `Content-Type: application/json`, **sin `type`** | Primer mensaje de `errors` si existe; si no, `detail` |
| ProblemDetails automático de ASP.NET (JSON mal formado, binding) | `{"type":"https://tools.ietf.org/html/rfc9110#section-15.5.1","title":"One or more validation errors occurred.","status":400,"errors":{"$":["Expected depth…"]},"traceId":"…"}`, `application/problem+json`, **sin `detail`** | Texto por estado (400: "Los datos enviados no son válidos."); `errors` se descarta (inglés, técnico) |
| Sin cuerpo o no JSON (403 de política, proxy) | — | Texto por estado |
| 429 | — | "Demasiados intentos. Espera N segundos." con `Retry-After` |

Textos por estado: 400 "Los datos enviados no son válidos.", 403 "No tienes permiso para realizar esta
acción.", 404 "No se encontró lo que buscas.", 409 "Hubo un conflicto con los datos actuales.
Recarga e inténtalo de nuevo.", 413 "El archivo supera el tamaño máximo permitido.", 500 o más
"Ocurrió un error en el servidor.", resto "No se pudo completar la solicitud.". Un fallo de red al
pedir (no al renovar) también da el mensaje de conexión en lugar de "Failed to fetch".

Serialización en el backend: `ExceptionHandlingMiddleware.cs` (`catch (ValidationException)` →
`WriteProblemDetailsAsync`, que fija `Status`, `Title`, `Detail`, `Instance` y la extensión `errors`).

## 8. Pantallas pendientes

No existen pantallas de subida de documentos ni de alta de usuarios (`modules/documents` y
`modules/users` solo tienen un `README.md`; nada llama a `postForm` ni a `/api/auth/register`).
Cuando se construyan deben usar `http.postForm`/`http.post`, mostrar `ApiError.message` y validar
el tamaño del archivo en el cliente (sección 4).

## 9. Otros mensajes nuevos

- **400** "La operación no cumple las reglas del sistema." (`title`: "Error de validación"): un
  trigger de la BD rechazó la operación y su texto técnico no se muestra. Mensaje genérico en la SPA.
- Login externo: la SPA ya traduce `/login?error=` con `federado_fallido`, `sin_cuenta`,
  `dominio_no_permitido` y `cuenta_inactiva`. `dominio_no_permitido` ahora también aparece
  cuando un correo de Microsoft no está en la lista de dominios permitidos.
