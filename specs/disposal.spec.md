# Spec — Baja de activos (AssetDisposal)

## Propósito

Workflow formal de baja de activos: generación de acta, causa (`disposal_reason`), aprobaciones y firma, con trazabilidad completa.

## Modelo

`asset_disposal`: disposal_id, asset_id (1:1), disposal_reason_id, fecha de baja, observaciones, acta/documento asociado, aprobado por, auditoría.

## Reglas de negocio

- Un activo solo puede darse de baja **una vez** (relación 1:1 con `asset`).
- No se puede dar de baja un activo con incidentes abiertos ni asignaciones activas pendientes de devolución.
- La baja cambia el estado del activo a `dado_de_baja` y genera registro de auditoría.
- El acta de baja se genera como documento firmable (módulo `disposal` del frontend).

## Criterios de aceptación

- [ ] Workflow de baja con validaciones de pre-condición (FASE 2+)
- [ ] Generación y firma de acta (FASE 3)

## Implementación

- Feature: `backend/Features/AssetDisposal/`
- Módulo frontend: `frontend/src/modules/disposal/`
