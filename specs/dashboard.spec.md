# Spec — Dashboard

## Propósito

Métricas e indicadores visuales generales del inventario para el panel principal.

## Indicadores

- Total de activos por estado (`asset_status`) y por tipo (`asset_type`)
- Incidentes abiertos por prioridad
- Mantenimientos preventivos próximos a vencer
- Equipos por edificio/área (`v_location_equipment_summary`)
- Licencias por vencer

## Reglas de negocio

- Datos agregados vía vistas materializadas (`v_asset_summary`); permiso `dashboard.view`.
- Caché en memoria para métricas costosas (`AddMemoryCache`).

## Criterios de aceptación

- [ ] Endpoint de métricas agregadas (pendiente)
- [ ] Panel visual en frontend (FASE 3)

## Implementación

- Feature: `backend/Features/Dashboard/`
- Módulo frontend: `frontend/src/modules/dashboard/`
