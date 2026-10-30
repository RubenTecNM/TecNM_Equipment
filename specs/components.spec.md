# Spec — Componentes (Components)

## Propósito

Hardware interno de cada activo (RAM, disco, tarjeta, etc.) y su historial de actualizaciones.

## Modelo

`asset_component`: asset_component_id, asset_id, component_type_id, descripción, marca/modelo, serie, fecha de instalación, `is_current` (vigente), auditoría + soft-delete (una actualización cierra el anterior y abre uno nuevo).

## Reglas de negocio

- Cada componente se tipifica con `component_type` (catálogo).
- Un componente reemplazado se cierra (soft-delete o `is_current = false`) y se crea el nuevo: historial completo de actualizaciones del activo.

## Criterios de aceptación

- [ ] CRUD de componentes con historial de cambios (pendiente)

## Implementación

- Feature: `backend/Features/Components/`
- Módulo frontend: `frontend/src/modules/components/`
