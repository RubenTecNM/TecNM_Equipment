# Spec — Roles y permisos (Roles)

## Propósito

Administración de roles y su matriz de permisos (RBAC dinámico, sin redeploy).

## Endpoints

| Método | Ruta | Permiso | Descripción |
| --- | --- | --- | --- |
| `GET` | `/api/roles` | `roles.view` | Listado de roles |
| `POST` | `/api/roles` | `roles.create` | Crea rol |
| `PUT` | `/api/roles/{roleId}/permissions` | `roles.update` | Asigna permisos al rol |

## Reglas de negocio

- `role` (code, name) ↔ `permission` (code) en relación n:m.
- Los permisos se siembran desde `scripts/seed-permissions.sql` con códigos `<modulo>.<accion>`.
- Las políticas de autorización se resuelven en caliente desde la BD (`PermissionPolicyProvider`).

## Criterios de aceptación

- [x] Listado, creación y asignación de permisos a roles
- [x] Efecto inmediato en la autorización de endpoints

## Implementación

- Feature: `backend/Features/Roles/` (GetRoles, CreateRole, AssignPermissions)
- Permisos sembrados: `scripts/seed-permissions.sql`, `scripts/add-procurement-permission.sql`, `scripts/add-incidents-permissions.sql`
