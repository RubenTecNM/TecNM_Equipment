# Spec — Autenticación y Autorización (Auth)

## Propósito

Autenticar usuarios del sistema (JWT) y autorizar cada operación mediante RBAC con permisos granulares (`modulo.accion`).

## Endpoints

| Método | Ruta | Permiso | Descripción |
| --- | --- | --- | --- |
| `POST` | `/api/auth/login` | público | Valida credenciales, pone cookies de sesión y devuelve el perfil |
| `POST` | `/api/auth/refresh` | público (cookie `tecnm_rt`) | Rota el refresh token y renueva el JWT |
| `POST` | `/api/auth/logout` | público | Revoca el refresh token y borra cookies (idempotente) |
| `GET` | `/api/auth/me` | autenticado | Perfil y **permisos efectivos** del rol |
| `GET` | `/api/auth/providers` | público | Proveedores federados configurados |
| `GET` | `/api/auth/external/{microsoft\|google}?returnUrl=` | público | Inicia el challenge OIDC |
| `GET` | `/api/auth/external/callback` | público | Vincula identidad, abre sesión y redirige a la SPA |
| `POST` | `/api/auth/register` | rol `ADMIN` | Crea un usuario con hash BCrypt |

## Sesión por cookies

| Cookie | Contenido | Flags | Vida |
| --- | --- | --- | --- |
| `tecnm_at` | JWT de acceso (solo rol) | HttpOnly, SameSite=Lax, Path=/api | `Jwt:ExpirationMinutes` (15) |
| `tecnm_rt` | Refresh opaco (en BD solo SHA-256) | HttpOnly, SameSite=Strict, Path=/api/auth | `Auth:RefreshTokenDays` (7) |
| `tecnm_csrf` | Token CSRF double-submit | legible por JS, SameSite=Strict | igual que refresh |

- `Secure` en todas salvo `Auth:CookieSecure=false` (solo Development sobre HTTP).
- JwtBearer lee el token de la cookie; el header `Authorization: Bearer` sigue valiendo (curl/tests).
- `CsrfMiddleware`: POST/PUT/PATCH/DELETE autenticados por cookie exigen `X-CSRF-Token` = cookie `tecnm_csrf` (403 si no).
- Rotación: cada refresh revoca el anterior (`replaced_by_id`). Reusar un token revocado fuera de 30 s de gracia revoca toda la familia.
- SPA y API en el mismo origen (proxy de Vite en desarrollo, reverse proxy en producción); sin CORS.

## Login federado (Microsoft Entra / Google)

- OIDC (`Microsoft.AspNetCore.Authentication.OpenIdConnect`), code + PKCE, `response_mode=query`. Un proveedor sin `ClientId` no se registra.
- **Sin auto-alta**: el primer login vincula por email verificado con un `app_users` activo (INSERT auditado en `user_external_logins`); los siguientes, por `oid` (Entra) o `sub` (Google).
- Entra restringido al tenant (`Auth:Entra:TenantId`); Google al dominio `Auth:Google:HostedDomain` (claim `hd`, validado en servidor).
- Errores → `/login?error=sin_cuenta|dominio_no_permitido|cuenta_inactiva|federado_fallido`.
- Secrets (user-secrets): `Auth:Entra:{TenantId,ClientId,ClientSecret}`, `Auth:Google:{ClientId,ClientSecret,HostedDomain}`.
- Redirect URIs a registrar: `{origen SPA}/api/auth/signin-microsoft` y `{origen SPA}/api/auth/signin-google` (desarrollo: `http://localhost:5173/...`).

## Reglas de negocio

- Contraseñas hasheadas con **BCrypt** (`PasswordHash` en `app_user`); utilidad `backend/tools/PasswordHasher` para sembrar usuarios.
- JWT simétrico: `Jwt:Secret`, `Jwt:Issuer`, `Jwt:Audience` en **user-secrets** (nunca en appsettings). `ClockSkew` de 1 minuto; claims no se mapean automáticamente (`MapInboundClaims = false`).
- `app_user.status` controla habilitación de cuenta.
- Todo el API exige autenticación por defecto (`FallbackPolicy`); solo `login` es `[AllowAnonymous]`.
- Autorización por **políticas dinámicas** resueltas desde `permission.code` (ej. `assets.view`), no por roles hardcodeados — excepción: `register` exige rol `ADMIN`.

## Datos

- `app_users` (user_id, username, email, password_hash, first_name, last_name, role_id, status, auditoría + soft-delete)
- `refresh_tokens`, `user_external_logins` (`scripts/add-auth-sessions.sql`)
- `role` (role_id, code, name) ↔ `permission` (permission_id, code) vía tabla n:m
- `audit_log` registra acciones relevantes (inmutable)

## Criterios de aceptación

- [x] Login con credenciales válidas devuelve token JWT
- [x] Login inválido rechaza sin filtrar información
- [x] Register solo con rol ADMIN; contraseña hasheada
- [x] Endpoint protegido sin token → 401; con token sin permiso → 403
- [x] Permisos editables en caliente (asignación a roles sin redeploy)
- [x] Token en cookie HttpOnly; nunca en localStorage ni en el cuerpo de la respuesta
- [x] Refresh rotativo con detección de reutilización
- [x] CSRF double-submit en escrituras con cookie
- [x] `GET /me` con permisos efectivos; la UI filtra por permiso
- [ ] Login federado probado contra app registrations reales de Entra y Google

## Implementación

- Feature: `backend/Features/Auth/` (Login, Refresh, Logout, GetMe, GetProviders, ExternalLogin, Register; `Shared/AuthSessionIssuer`)
- Cross-cutting: `Common/Security/{AuthCookieWriter,AuthOptions}`, `Common/Middleware/CsrfMiddleware`, `Common/Security/JwtTokenGenerator`, `Common/Authorization/PermissionPolicyProvider`, `Common/Authorization/RolePermissionService`
