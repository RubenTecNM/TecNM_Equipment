# Spec — Reportes (Reports)

## Propósito

Generación y exportación de reportes del inventario: equipos por ubicación, resumen de activos, incidentes abiertos, mantenimientos, licencias.

## Fuentes de datos

- Vistas `v_asset_summary` y `v_location_equipment_summary`
- Entidades transaccionales (incident, maintenance_record, software_license)

## Reglas de negocio

- Exportación a PDF/Excel (según permiso `reports.view`).
- Filtros por fecha, ubicación, tipo de activo y estado.

## Criterios de aceptación

- [ ] Reporte de equipos por ubicación (FASE 2+)
- [ ] Exportación PDF/Excel (FASE 2+)

## Implementación

- Feature: `backend/Features/Reports/`
- Módulo frontend: `frontend/src/modules/reports/`
