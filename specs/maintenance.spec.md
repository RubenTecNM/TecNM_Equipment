# Spec — Mantenimiento (Maintenance)

## Propósito

Registro de intervenciones **preventivas y correctivas** sobre activos, con calendario de próximos servicios.

## Modelo

`maintenance_record`: maintenance_id, asset_id, maintenance_type_id, fecha, descripción, técnico (`performed_by` = app_user), costo, próxima fecha, auditoría + soft-delete.

## Reglas de negocio

- Todo mantenimiento se asocia a un activo existente.
- `maintenance_type` distingue preventivo / correctivo / predictivo (catálogo sembrado).
- El mantenimiento correctivo puede vincularse al incidente que lo originó (campo de trazabilidad).

## Criterios de aceptación

- [ ] CRUD de registros de mantenimiento (pendiente implementar feature completa)
- [ ] Calendario de mantenimientos preventivos próximos a vencer (FASE 2+)

## Implementación

- Feature: `backend/Features/Maintenance/`
- Módulo frontend: `frontend/src/modules/maintenance/`
