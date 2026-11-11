# Pendientes de la SPA tras el Lote 5 (seguridad)

Tareas del frontend que se desprenden de los cambios del backend. Los códigos y
mensajes son **exactos** (los textos de `detail` los muestra la SPA tal cual).
Las referencias a `http.ts` son a `frontend/src/shared/api/http.ts` (leído, no
modificado en este lote).

Hoy `http.ts` muestra el primer mensaje de `errors` (validación) o, si no hay,
`detail` de un ProblemDetails; si el cuerpo no es JSON, "Error {status}".

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

- **Login:** mostrar el `detail` y deshabilitar el botón el tiempo de `Retry-After`.
- **Refresh: un 429 NO es sesión terminada.** No limpiar el estado ni redirigir.
  Hoy `refreshSession()` (`http.ts:98-110`) devuelve `response.ok`, `send()` reintenta
  (`:133-136`) y el 401 posterior llega a `sessionLost()` (`:184-186`): un 429 en refresh
  cerraría la sesión. **Pendiente:** distinguir el 429, respetar `Retry-After` y no redirigir.

## 2. Un solo refresh concurrente

- Ya existe una renovación en vuelo **por pestaña** (`refreshInFlight`, `http.ts:94-110`).
- **Pendiente: coordinar entre pestañas** (p. ej. `BroadcastChannel` o Web Locks). Reusar
  un refresh ya rotado **después de 5 segundos** (`Auth__RefreshReuseGraceSeconds`) se
  interpreta como robo y **revoca toda la sesión** en todas las pestañas. Dentro de los
  5 segundos no pasa nada y las cookies no se borran.
- No lanzar refresh proactivos ni duplicados desde pestañas distintas.

## 3. Qué significa cada 401

| Petición | Mensaje (`detail`) | Significado | Qué hacer |
|---|---|---|---|
| Cualquier endpoint de negocio | (el 401 de autenticación no trae mensaje) | JWT caducado (15 min) **o** el usuario cambió: desactivado, borrado o con otro rol | **Intentar refresh una vez** y repetir la petición |
| `POST /api/auth/refresh` | "La sesión expiró. Inicia sesión de nuevo." | Sin cookie, token inexistente o caducado | **Sesión terminada**: ir al login |
| `POST /api/auth/refresh` | "La cuenta no está activa." | Usuario desactivado o borrado (cookies borradas) | **Sesión terminada** |
| `POST /api/auth/refresh` | "La sesión ya fue renovada o revocada." | Token ya rotado. Fuera de la gracia de 5 s: cookies borradas y familia revocada | **Sesión terminada**. Dentro de la gracia (otra pestaña acaba de rotar): **no** se borran cookies; repetir la petición original con las cookies nuevas (ya lo hace `http.ts:133-136`) |
| `GET /api/auth/me` | "La cuenta no está activa." | Usuario ya no activo | **Sesión terminada** |
| `POST /api/auth/login` | "Credenciales inválidas." | Mismo mensaje para usuario inexistente, inactivo, borrado, contraseña incorrecta o ambigüedad | Mostrarlo; **no** redirigir |

- Si el refresh devuelve 200 tras un cambio de rol, el token nuevo lleva el rol nuevo:
  **recargar `GET /api/auth/me`** (perfil y permisos) antes de repetir la petición.
- Hasta 30 s después de desactivar un usuario su JWT puede seguir valiendo (caché de estado).

## 4. Subida de documentos (`POST /api/documents`)

**413** (`application/problem+json`), con o sin `Content-Length`:

```json
{ "status": 413, "title": "Solicitud demasiado grande",
  "detail": "El archivo supera el tamaño máximo permitido." }
```

Ocurre si la petición supera 5 MB + 64 KB. **Pendiente:** validar en el cliente
`file.size <= 5242880` (5 MB) y `> 0` antes de enviar.

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
  seguro). Mostrar el `detail`; hoy `download()` (`http.ts:244-261`) ya lo hace.
- `Content-Type` = tipo detectado; `Content-Disposition: attachment` con `filename*` UTF-8
  (`fileNameFrom`, `http.ts:42-50`, lo interpreta).

## 5. Alta de usuarios (`POST /api/auth/register`)

Todos los textos de la tabla llegan por `errors` (400) salvo el 409 y el 403.

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

**Pendiente:** mostrar siempre "No tienes permiso para realizar esta acción." cuando el 403 no
traiga cuerpo o traiga el genérico, y tratar el de CSRF como "recarga la página". La API ya
no revela el rol, el tipo de documento ni el permiso exigido.

## 7. Otros mensajes nuevos

- **400** "La operación no cumple las reglas del sistema." (`title`: "Error de validación"): un
  trigger de la BD rechazó la operación y su texto técnico no se muestra. Mensaje genérico en la SPA.
- Login externo: la SPA ya traduce `/login?error=` con `federado_fallido`, `sin_cuenta`,
  `dominio_no_permitido` y `cuenta_inactiva`. `dominio_no_permitido` ahora también aparece
  cuando un correo de Microsoft no está en la lista de dominios permitidos.
