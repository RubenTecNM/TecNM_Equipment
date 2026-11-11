# Despliegue (backend)

Guía operativa del Lote 5 (seguridad). Todo valor real (dominios, IPs, secretos)
se define en el servidor por variables de entorno o user-secrets; **nada de eso
va al repositorio** (regla 18). Los nombres de variable usan `__` donde la
configuración usa `:` (p. ej. `Auth:CookieSecure` → `Auth__CookieSecure`).

Plantilla de referencia: `backend/.env.example`.

## 1. Variables obligatorias fuera de Development

Si falta alguna de las de la primera tabla, **la API no arranca**
(`SecurityStartupValidator` y `Program.cs`). Es deliberado: una configuración
insegura debe fallar al arrancar, no en la primera petición.

| Variable | Condición | Motivo |
|---|---|---|
| `Jwt__Secret` | siempre | Firma de los JWT. Mínimo 32 bytes UTF-8 y sin valores de ejemplo (`changeme`, `<…>`, etc.). Genera uno con `openssl rand -base64 48`. |
| `Jwt__Issuer`, `Jwt__Audience` | siempre | `Program.cs` falla si faltan. |
| `Auth__CookieSecure` | por defecto `true` | Con `false` fuera de Development no arranca: las cookies de sesión no pueden viajar por HTTP. |
| `AllowedHosts` | siempre | No puede ser `*` ni estar vacío. Lista los nombres del servidor separados por `;`. Motivo: el login federado arma la `redirect_uri` con la cabecera Host. |
| `Auth__Entra__TenantId` | si Entra está configurado (hay `Auth__Entra__ClientId`) | No puede ser `common`, `organizations` ni `consumers`: aceptarían cuentas de otras organizaciones. Usa el Id del tenant institucional. |
| `Auth__AllowedEmailDomains__0` (`__1`, …) | si Entra está configurado | Dominios de correo permitidos (igualdad exacta, sin `@`). Vacío = ningún correo de Microsoft entra. |
| `Auth__Google__HostedDomain` | si Google está configurado (hay `Auth__Google__ClientId`) | Sin él, cualquier cuenta de Google podría iniciar sesión. |

Además, para operar:

| Variable | Uso |
|---|---|
| `ConnectionStrings__Default` | Conexión a PostgreSQL. Es un secreto: variable de entorno o user-secrets. No hay validación de arranque propia. |
| `Auth__Entra__ClientId` / `Auth__Entra__ClientSecret`, `Auth__Google__ClientId` / `Auth__Google__ClientSecret` | Solo si se usa login federado. Un proveedor sin `ClientId` no se registra y no se exige nada de él. Los `ClientSecret` son secretos. |
| `Auth__SpaBaseUrl` | Origen de la SPA, destino de las redirecciones del login federado. |
| `Storage__LocalPath` | Carpeta de documentos. Relativa se resuelve contra el directorio de la aplicación; fuera de cualquier carpeta servida públicamente. |

## 2. Proxy inverso e IP real

Sin configuración, la API solo confía en el proxy que esté en loopback. Detrás
de un proxy en otra dirección, la IP de cada petición sería la del proxy y el
**límite por IP afectaría a todos los usuarios a la vez**; además `created_ip` de
los refresh tokens guardaría la IP del proxy.

```
ForwardedHeaders__KnownProxies__0=<ip-del-proxy>
ForwardedHeaders__KnownNetworks__0=<red-del-proxy-en-CIDR>   # alternativa a la IP
```

- Se leen `X-Forwarded-For` y `X-Forwarded-Proto`, con un solo salto (`ForwardLimit = 1`).
- Una IP o red mal escrita impide arrancar.
- El proxy debe **sobrescribir** esas cabeceras, no concatenar las del cliente.
- Nunca se limpia la lista de proxies conocidos para "confiar en todo".

## 3. HTTPS y HSTS

- La API aplica `UseHttpsRedirection` siempre y `UseHsts()` solo fuera de Development
  (valores por defecto de ASP.NET Core; el navegador solo recibe la cabecera sobre HTTPS).
- Si el TLS termina en el proxy, el proxy debe enviar `X-Forwarded-Proto: https` y estar
  en `KnownProxies`/`KnownNetworks`; si no, la API cree que la petición es HTTP.
- Las cookies de sesión son `Secure`: el navegador debe llegar por HTTPS.
- En Development aparece el aviso "Failed to determine the https port for redirect" porque
  el perfil `http` no define puerto HTTPS; es inofensivo.

## 4. Puertos por variable de entorno

`Properties/launchSettings.json` solo aplica a `dotnet run` en desarrollo. En el
servidor el puerto se asigna por entorno, por ejemplo:

```
ASPNETCORE_URLS=http://127.0.0.1:<puerto>
```

(o `ASPNETCORE_HTTP_PORTS` / `ASPNETCORE_HTTPS_PORTS`). La API y la SPA deben verse bajo
el **mismo origen** (la SPA sirve `/api` por proxy): no hay CORS configurado y las
cookies son de primera parte.

## 5. Subida de documentos

- Máximo por archivo: **5 MB** (`Storage__MaxFileSizeBytes`, por defecto `5242880`).
  Solo se puede **bajar**: la BD tiene `CHECK (size_bytes <= 5242880)` en `documents`
  (`scripts/seed-documents.sql`) y el límite de la petición está fijado en código.
- Límite de la petición: 5 MB + 64 KB de margen para el formulario
  (`DocumentUploadLimits`). Superarlo da 413, con o sin `Content-Length`.
- Si hay un proxy con su propio límite de cuerpo, debe permitir al menos 5 MB + 64 KB;
  si no, el proxy responderá 413 antes que la API.
- Los archivos **nunca se borran físicamente** (regla 20): un documento "borrado" es un
  borrado lógico y su archivo se conserva. Incluye la carpeta en los respaldos.
- Documentos subidos antes del Lote 5: se siguen descargando. Su tipo nunca se verificó
  por contenido y su nombre pudo guardarse crudo; la descarga sanitiza el nombre.

## 6. Scripts SQL añadidos en el Lote 5

Se ejecutan **después** de los scripts base, en este orden (ver `scripts/README.md`):

```bash
psql "$DATABASE_URL" -v ON_ERROR_STOP=1 -f add-users-manage-permission.sql   # permiso users.manage (alta de usuarios)
psql "$DATABASE_URL" -v ON_ERROR_STOP=1 -f add-app-users-lower-indexes.sql   # unicidad sin mayúsculas
```

- `add-users-manage-permission.sql` necesita `roles` y `permissions` ya sembrados
  (`seed-permissions.sql`). Sin él, **Register responde 403 incluso a ADMIN**.
- `add-app-users-lower-indexes.sql` falla, sin cambiar nada, si ya hay usuarios o correos
  que solo difieren en mayúsculas. Antes, comprueba:
  ```sql
  SELECT lower(username), count(*) FROM inventario.app_users GROUP BY 1 HAVING count(*) > 1;
  SELECT lower(email),    count(*) FROM inventario.app_users GROUP BY 1 HAVING count(*) > 1;
  ```
  Ambas deben devolver 0 filas. Son idempotentes: se pueden repetir.

## 7. Cachés: cuándo reiniciar la API

- **Permisos por rol: 5 minutos.** Solo se invalida al asignar permisos desde la API. Tras
  aplicar un script que cambia permisos (como `add-users-manage-permission.sql`) o cualquier
  cambio de permisos por SQL, **reinicia la API** o espera 5 minutos.
- **Estado del usuario por petición: 30 segundos** (`Security__SessionValidation__CacheSeconds`).
  Es lo máximo que un JWT vigente sigue funcionando tras desactivar, borrar o cambiar el rol
  de un usuario. Con `0` se consulta en cada petición.
- Los contadores de intentos de login y las cachés están **en memoria del proceso**: se
  pierden al reiniciar y no se comparten entre instancias. Con varias instancias, los límites
  se aplican por instancia.

## 8. Configuración nueva del Lote 5 (valores por defecto)

| Clave | Por defecto |
|---|---|
| `Security__RateLimiting__LoginPermitLimit` / `LoginWindowSeconds` | 10 / 60 |
| `Security__RateLimiting__RefreshPermitLimit` / `RefreshWindowSeconds` | 30 / 60 |
| `Security__RateLimiting__AccountMaxFailures` | 5 |
| `Security__RateLimiting__AccountFailureWindowMinutes` / `AccountLockoutMinutes` | 15 / 15 |
| `Security__SessionValidation__CacheSeconds` | 30 |
| `Auth__RefreshReuseGraceSeconds` | 5 |
| `Storage__MaxFileSizeBytes` | 5242880 |
| `ForwardedHeaders__KnownProxies__N`, `ForwardedHeaders__KnownNetworks__N` | vacío (solo loopback) |
| `Auth__AllowedEmailDomains__N` | vacío |

## 9. Problemas conocidos de compilación

**`error MSB3030: No se pudo copiar el archivo ".../MvcTestingAppManifest.json"`**
en `dotnet build` o `dotnet test` **incrementales** del proyecto de tests
(`Microsoft.AspNetCore.Mvc.Testing`). No es un fallo del código. Solución: limpiar y
restaurar, y volver a ejecutar:

```bash
find backend -type d \( -name bin -o -name obj \) -prune -exec rm -rf {} +
dotnet restore backend/TecnmInventario.slnx
```

## 10. Comprobación tras desplegar

```bash
curl -sI https://<servidor>/api/auth/providers
```

Debe incluir `X-Content-Type-Options: nosniff`, `X-Frame-Options: DENY`,
`Referrer-Policy: no-referrer`, `Cache-Control: no-store` y, por HTTPS,
`Strict-Transport-Security`.
