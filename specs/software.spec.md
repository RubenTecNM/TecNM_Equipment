# Spec — Software y licencias (Software)

## Propósito

Inventario de programas instalados en activos y control de licencias (asignaciones por máquina o por usuario).

## Modelo

- `software` (software_id, nombre, software_type_id, fabricante, versión)
- `software_license` (license_id, software_id, license_type_id, clave, cantidad, fechas)
- `software_license_assignment` (asignación a `asset` o `app_user`)
- `asset_software` (software instalado por activo)

## Reglas de negocio

- No asignar más licencias de las disponibles (`license_type` define métrica: por máquina / por usuario / sitio).
- `asset_os_history` registra cambios de sistema operativo del activo.
- Alertas de licencias por vencer (backlog).

## Criterios de aceptación

- [ ] CRUD de software y licencias (pendiente)
- [ ] Control de conteo de licencias (pendiente)

## Implementación

- Feature: `backend/Features/Software/`
- Módulo frontend: `frontend/src/modules/software/`
