# TecNM Assets Management

Sistema web profesional para la **gestión, control, seguimiento e inventario de activos tecnológicos** del Tecnológico Nacional de México.

Monorrepo modular: API REST (.NET 10) + SPA (React + TypeScript + Vite) + specs y planes de desarrollo en la raíz.

## Stack

| Capa | Tecnología |
| --- | --- |
| Backend | C#, .NET 10, ASP.NET Core Web API, EF Core (PostgreSQL), MediatR, FluentValidation |
| Frontend | React 19, TypeScript, Vite 8, Tailwind CSS 4 |
| Base de datos | PostgreSQL 18 (Docker) |
| Autenticación | JWT + cookies HttpOnly (Bearer para curl/tests) y RBAC por permisos (`modulo.accion`) |

## Arranque rápido

```bash
# 1. Base de datos
cd database
cp .env.example .env          # edita .env y define POSTGRES_PASSWORD
#   openssl rand -base64 24    # genera la contraseña
docker compose up -d

# 2. Esquema y seeds (desde la raíz del repo)
export PGPASSWORD='<TU_PASSWORD>'
psql -h 127.0.0.1 -p 5433 -U tecnm_dev -d tecnm_inventario -f database.sql
psql -h 127.0.0.1 -p 5433 -U tecnm_dev -d tecnm_inventario -f scripts/seed-database.sql
psql -h 127.0.0.1 -p 5433 -U tecnm_dev -d tecnm_inventario -f scripts/seed-permissions.sql
psql -h 127.0.0.1 -p 5433 -U tecnm_dev -d tecnm_inventario -f scripts/seed-documents.sql
psql -h 127.0.0.1 -p 5433 -U tecnm_dev -d tecnm_inventario -f scripts/add-procurement-permission.sql
psql -h 127.0.0.1 -p 5433 -U tecnm_dev -d tecnm_inventario -f scripts/add-incidents-permissions.sql
psql -h 127.0.0.1 -p 5433 -U tecnm_dev -d tecnm_inventario -f scripts/seed-users-view-permission.sql
psql -h 127.0.0.1 -p 5433 -U tecnm_dev -d tecnm_inventario -f scripts/add-auth-sessions.sql

# 3. Backend (los secretos van en user-secrets, ver "Variables de entorno")
cd backend
dotnet user-secrets set "Jwt:Secret" "$(openssl rand -base64 48)"
dotnet user-secrets set "Jwt:Issuer" "TecnmInventario"
dotnet user-secrets set "Jwt:Audience" "TecnmInventarioClients"
dotnet user-secrets set "ConnectionStrings:Default" "Host=127.0.0.1;Port=5433;Database=tecnm_inventario;Username=tecnm_dev;Password=<TU_PASSWORD>"
dotnet run --project TecnmInventario.Api.csproj

# 4. Frontend
cd ../frontend
npm install
npm run dev
```

`database/.env`, los user-secrets y cualquier archivo `.env` local **no se versionan**.

## Variables de entorno

La API lee la configuración de `appsettings.json`, de user-secrets (desarrollo) y de
variables de entorno (formato `__` para secciones). Nunca escribas valores reales en
el repositorio.

| Variable | Dónde se define | Obligatoria | Ejemplo |
| --- | --- | --- | --- |
| `Jwt:Secret` (`Jwt__Secret`) | user-secrets / variable de entorno | Sí (mín. 32 bytes UTF-8) | `<generado con openssl rand -base64 48>` |
| `Jwt:Issuer` (`Jwt__Issuer`) | user-secrets / variable de entorno | Sí | `TecnmInventario` |
| `Jwt:Audience` (`Jwt__Audience`) | user-secrets / variable de entorno | Sí | `TecnmInventarioClients` |
| `ConnectionStrings:Default` (`ConnectionStrings__Default`) | user-secrets / variable de entorno | Sí | `Host=127.0.0.1;Port=5433;Database=tecnm_inventario;Username=tecnm_dev;Password=<TU_PASSWORD>` |
| `Storage:LocalPath` (`Storage__LocalPath`) | appsettings / variable de entorno | No (por defecto `storage/documents`) | `storage/documents` (relativa al ContentRoot) |
| `Auth:SpaBaseUrl` (`Auth__SpaBaseUrl`) | appsettings / variable de entorno | Sí para login federado | `http://localhost:5173` |
| `Auth:CookieSecure` (`Auth__CookieSecure`) | appsettings / variable de entorno | Sí fuera de Development (debe ser `true`) | `true` |
| `Auth:RefreshTokenDays` (`Auth__RefreshTokenDays`) | appsettings | No | `7` |
| `Auth:Entra:TenantId` (`Auth__Entra__TenantId`) | user-secrets / variable de entorno | No (OIDC opcional) | `<tenant-id>` |
| `Auth:Entra:ClientId` (`Auth__Entra__ClientId`) | user-secrets / variable de entorno | No (OIDC opcional) | `<client-id>` |
| `Auth:Entra:ClientSecret` (`Auth__Entra__ClientSecret`) | user-secrets / variable de entorno | No (OIDC opcional) | `<client-secret>` |
| `Auth:Google:ClientId` (`Auth__Google__ClientId`) | user-secrets / variable de entorno | No (OIDC opcional) | `<client-id>.apps.googleusercontent.com` |
| `Auth:Google:ClientSecret` (`Auth__Google__ClientSecret`) | user-secrets / variable de entorno | No (OIDC opcional) | `<client-secret>` |
| `Auth:Google:HostedDomain` (`Auth__Google__HostedDomain`) | appsettings / variable de entorno | No | `example.edu.mx` |
| `VITE_API_BASE_URL` | `frontend/.env` | No (vacío = mismo origen) | `` |

Comandos de user-secrets (desde `backend/`):

```bash
dotnet user-secrets set "Jwt:Secret" "$(openssl rand -base64 48)"
dotnet user-secrets set "Jwt:Issuer" "TecnmInventario"
dotnet user-secrets set "Jwt:Audience" "TecnmInventarioClients"
dotnet user-secrets set "ConnectionStrings:Default" "Host=127.0.0.1;Port=5433;Database=tecnm_inventario;Username=tecnm_dev;Password=<TU_PASSWORD>"
dotnet user-secrets list   # comprobar claves (evita compartir la salida)
```

Genera secretos aleatorios con `openssl rand -base64 48` (JWT) y `openssl rand -base64 24`
(contraseña de PostgreSQL). No commitees `.env`, `database/.env` ni los user-secrets
(`~/.microsoft/usersecrets` queda fuera del repo).

## Tests

```bash
# Suite rápida (sin Docker; InMemory)
dotnet test backend/TecnmInventario.slnx --filter "Category!=Integration"

# Integración con PostgreSQL real vía Testcontainers (requiere Docker)
dotnet test backend/TecnmInventario.slnx --filter "Category=Integration"
```

Con `REQUIRE_DOCKER_TESTS=1` los tests de integración fallan (en vez de omitirse) si
Docker no está disponible; es lo que usa el CI. Más detalle en `AGENTS.md`.

## Hook de seguridad

El repo incluye un hook de `pre-commit` que ejecuta gitleaks sobre lo que se va a
commitear (si gitleaks no está instalado, avisa y no bloquea; el CI sí bloquea):

```bash
git config core.hooksPath .githooks
```

## Reporte de vulnerabilidades

No abras un issue público. Usa **Report a vulnerability** en la pestaña *Security*
de GitHub (GitHub Security Advisories) del repositorio. Ver `SECURITY.md`.

## Estructura

```text
├── backend/        # API REST — Arquitectura Vertical Slice (Core / Data / Features / Common)
├── frontend/       # SPA React — modular (app / modules / shared)
├── database/       # docker-compose de PostgreSQL (infraestructura de BD)
├── scripts/        # Scripts SQL de seed e inicialización
├── specs/          # Especificaciones funcionales por módulo
├── plans/          # Hojas de ruta por fases
├── database.sql    # Esquema completo de PostgreSQL (autoritativo)
├── BACKEND_ARCHITECTURE.md
├── FRONTEND_ARCHITECTURE.md
└── DIAGRAMA_ER.md
```

## Fases de desarrollo

1. **FASE 1 — Base de datos y autenticación** (`plans/01-database-and-auth-plan.md`)
2. **FASE 2 — Núcleo de activos** (`plans/02-assets-core-plan.md`)
3. **FASE 3 — SPA frontend** (`plans/03-frontend-spa-plan.md`)

## Decisiones y desviaciones de la plantilla

- **Nombre del ensamblado:** se conserva `TecnmInventario.Api` (y su `UserSecretsId`) para no invalidar los user-secrets que contienen `Jwt:Secret`. El nombre de producto es *TecNM Assets Management*.
- **`backend/Common/`:** capa transversal (autorización, auditoría, middleware, paginación, seguridad, almacenamiento) no contemplada en la plantilla, pero requerida por el código existente.
- **`database/`:** solo infraestructura de BD (`docker-compose.yml`, `.env`). Los SQL migraron a `database.sql` (raíz) y `scripts/`.
- **`backend/tests/` y `backend/tools/PasswordHasher`:** se conservan (utilidades de desarrollo).
- **Tailwind v4:** configuración CSS-first (`@theme` en `frontend/src/shared/styles/index.css`); no se usa `tailwind.config.js`.
