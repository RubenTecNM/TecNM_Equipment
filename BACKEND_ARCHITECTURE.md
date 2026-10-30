# Arquitectura Backend — TecNM Assets Management

API REST construida con **C# / .NET 10**, **ASP.NET Core Web API**, **EF Core** sobre **PostgreSQL**, organizada en **Vertical Slice Architecture** (una carpeta por caso de uso) dentro de un único proyecto web.

## Estructura de carpetas

```text
backend/
├── Core/
│   ├── Entities/            # Entidades EF Core (database-first) + vistas V*
│   │   └── Extensions/      # Extensiones de entidades (soft-deletable)
│   └── Contracts/           # Interfaces de dominio (ISoftDeletable)
├── Data/
│   ├── TecnmInventarioDbContext.cs        # DbContext + mapeo
│   └── TecnmInventarioDbContext.Custom.cs # Configuración adicional
├── Features/                # Vertical Slice: un módulo por característica
│   ├── Auth/                # Login / Register (JWT)
│   ├── Assets/              # CRUD + máquina de estados + filtros
│   ├── Locations/           # Árbol Edificio → Área → Rack
│   ├── Incidents/           # Levantamiento y seguimiento de fallas
│   ├── Maintenance/         # Mantenimiento preventivo/correctivo
│   ├── AssetDisposal/       # Workflow de baja formal
│   ├── BulkImport/          # Carga masiva Excel/CSV
│   ├── Software/            # Licencias y programas
│   ├── Components/          # Hardware interno y actualizaciones
│   ├── Documents/           # Evidencias / archivos
│   ├── History/             # Auditoría y timeline
│   ├── Reports/             # Reportes y exportación
│   ├── Dashboard/           # Métricas generales
│   ├── Catalogs/            # Catálogos, fabricantes, modelos
│   ├── Procurement/         # Suppliers / Warranties / Acquisitions
│   ├── Roles/               # Roles y asignación de permisos
│   └── Users/               # Usuarios
├── Common/                  # Cross-cutting (ver abajo)
├── Properties/launchSettings.json
├── tests/TecnmInventario.Api.Tests/
├── tools/PasswordHasher/    # Utilidad BCrypt para sembrar usuarios
├── .env.example
├── appsettings.json / appsettings.Development.json
├── Program.cs
├── TecnmInventario.Api.csproj
└── Directory.Packages.props # Central Package Management
```

## Patrón por feature (Vertical Slice)

Cada caso de uso es una carpeta con sus propios archivos, sin capas compartidas entre features:

```text
Features/Assets/CreateAsset/
├── CreateAssetCommand.cs     # Input (MediatR IRequest)
├── CreateAssetHandler.cs     # Reglas de negocio + acceso a datos
├── CreateAssetValidator.cs   # FluentValidation
└── CreateAssetResponse.cs    # Output (DTO)
```

- **MediatR** (`Mediator.Abstractions`) desacopla controladores de handlers.
- **FluentValidation** se aplica vía `ValidationBehavior` (pipeline behavior global).
- Los **controladores** viven dentro de su feature (`Features/Auth/AuthController.cs`) y solo traducen HTTP → MediatR.

## Common/ (cross-cutting)

| Carpeta | Responsabilidad |
| --- | --- |
| `Authorization` | `PermissionPolicyProvider`, `RolePermissionService`, handler de autorización |
| `Security` | Generación de JWT (`IJwtTokenGenerator`) |
| `Audit` | `AuditLogService` (tabla `audit_logs`, inmutable) |
| `Deletion` | `ISoftDeleteAuditor` — borrado lógico auditado |
| `Middleware` | Manejo global de excepciones |
| `Pagination` | `PagedRequest` / `PagedResult<T>` |
| `Storage` | `IFileStorageService` (disco local para documentos) |
| `Behaviors` / `Domain` / `Errors` | Pipeline, reglas y errores compartidos |

## Seguridad y autorización

- **JWT Bearer** (clave simétrica; `Jwt:Secret`, `Jwt:Issuer`, `Jwt:Audience` en **user-secrets**, nunca en appsettings).
- **RBAC dinámico:** `Role` ↔ `Permission` (n:m). Las políticas se resuelven por código de permiso (`assets.view`, `incidents.create`, …) mediante `PermissionPolicyProvider` + `PermissionAuthorizationHandler`.
- Política de fallback: todo endpoint requiere autenticación (`FallbackPolicy`).
- El usuario actual se inyecta vía `ICurrentUserService` (de `HttpContext`).

## Persistencia (Database-First)

- Esquema autoritativo: `database.sql` (raíz) + seeds en `scripts/`.
- EF Core con `Npgsql.EntityFrameworkCore.PostgreSQL`; el modelo refleja el esquema existente (incluidas vistas `v_asset_summary`, `v_location_equipment_summary`).
- Auditoría: campos `CreatedAt/By`, `UpdatedAt/By`, `Version` (concurrencia) y `IsDeleted/DeletedAt/DeletedBy` (soft-delete) en tablas transaccionales.

## Convenciones

- Namespaces: `TecnmInventario.Api.{Core,Data,Features,Common}.…`
- Nombres de permisos: `<modulo>.<accion>` (`view`, `create`, `update`, `delete`).
- Errores: `ExceptionHandlingMiddleware` devuelve respuestas de problema consistentes.
- Documentos subidos: `backend/storage/documents/` (ignorado por git, solo `.gitkeep`).

## Comandos

```bash
cd backend
dotnet build
dotnet run --project TecnmInventario.Api.csproj
dotnet test tests/TecnmInventario.Api.Tests
```
