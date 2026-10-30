# Spec — Usuarios (Users)

## Propósito

Administración de usuarios del sistema (alta, edición, habilitación/inhabilitación) con asignación de rol.

## Modelo

`app_user`: user_id, username (único), email, password_hash, first_name, last_name, role_id, status (`activo`/`inactivo`), auditoría + soft-delete.

## Reglas de negocio

- Creación y restablecimiento de contraseña con hash BCrypt.
- Deshabilitar (`status`) revoca accesos sin borrar el historial de auditoría del usuario.
- El usuario es quien firma `created_by` / `updated_by` / `performed_by` en todo el sistema.

## Criterios de aceptación

- [ ] CRUD de usuarios con asignación de rol (parcial — registro vía `auth.register`)
- [ ] Perfil y cambio de contraseña propio (backlog)

## Implementación

- Feature: `backend/Features/Users/`
- Módulo frontend: `frontend/src/modules/users/`
