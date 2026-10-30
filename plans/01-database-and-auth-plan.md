# Plan — FASE 1: Base de datos y autenticación

**Objetivo:** tener la base de datos operativa, el esquema completo sembrado y el sistema de autenticación/autorización funcionando como cimiento del resto del sistema.

## Alcance

### Base de datos
- [x] PostgreSQL 18 en Docker (`database/docker-compose.yml`, puerto 5433)
- [x] Esquema completo (`database.sql` en raíz, ~850 líneas): activos, ubicaciones, incidentes, mantenimiento, software, adquisiciones, auditoría, RBAC
- [x] Semillas de catálogos (`scripts/seed-database.sql`)
- [x] Semillas de permisos RBAC (`scripts/seed-permissions.sql` + adiciones de procurement e incidents)

### Autenticación y autorización
- [x] `POST /api/auth/login` (JWT simétrico, BCrypt)
- [x] `POST /api/auth/register` (rol ADMIN)
- [x] RBAC dinámico por permisos (`modulo.accion`) con políticas resueltas desde BD
- [x] Política de fallback: todo el API requiere autenticación
- [x] Herramienta `backend/tools/PasswordHasher` para sembrar usuarios

### Auditoría y transversales
- [x] `audit_log` inmutable + `AuditLogService`
- [x] Soft-delete auditado (`ISoftDeleteAuditor`)
- [x] Middleware de excepciones, paginación, almacenamiento local de documentos

## Criterios de salida

- `dotnet build` y `dotnet test` en verde
- Login funcional contra usuario sembrado; endpoints protegidos responden 401/403 según corresponda

## Estado: ✅ Completada (backend)

Pendiente en esta fase: formulario de login en el SPA (FASE 3).
