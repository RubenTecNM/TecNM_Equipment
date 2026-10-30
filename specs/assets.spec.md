# Spec — Activos (Assets)

## Propósito

Ficha técnica de cada equipo físico inventariable, máquina de estados de ciclo de vida, filtros y consultas (incluye QR para escaneo en campo, pendiente en frontend).

## Endpoints

| Método | Ruta | Permiso | Descripción |
| --- | --- | --- | --- |
| `GET` | `/api/assets` | `assets.view` | Listado paginado |
| `GET` | `/api/assets/{assetId}` | `assets.view` | Detalle por id |
| `POST` | `/api/assets` | `assets.create` | Crea activo |
| `PUT` | `/api/assets/{assetId}` | `assets.update` | Edita ficha (fabricante, modelo, ubicación, serie, hostname, notas) |
| `PUT` | `/api/assets/{assetId}/status` | `assets.update` | Transición de estado (máquina de estados) |
| `GET` | `/api/assets/{assetId}/warranties` | `assets.view` | Garantías del activo |
| `POST` | `/api/assets/{assetId}/movements` | `assets.move` | Registra movimiento de ubicación (actualiza `current_location_id`). Requiere `expectedVersion` (query) con la versión del activo que el cliente vio; `409` si no coincide. Devuelve `assetVersion` (versión nueva del activo) |
| `GET` | `/api/assets/{assetId}/movements` | `assets.view` | Historial de movimientos (paginado) |
| `POST` | `/api/assets/{assetId}/assignments` | `assets.update` | Asigna activo a usuario. Requiere `expectedVersion` (query); `409` si no coincide. Devuelve `assetVersion` |
| `GET` | `/api/assets/{assetId}/assignments` | `assets.view` | Historial de asignaciones |
| `POST` | `/api/assets/{assetId}/assignments/{assignmentId}/return` | `assets.update` | Devuelve asignación activa. Requiere `expectedVersion` (query); `409` si no coincide. Devuelve `assetVersion` |

## Reglas de negocio

- **Código de inventario** (`inventory_code`) estable e independiente de la ubicación (ej. `TEC-PC-0001`).
- **Máquina de estados** (`AssetStatusTransitions`): solo transiciones válidas entre `asset_status`.
- **No se puede liberar un activo con incidentes abiertos** (regla aplicada en `UpdateAssetStatus`).
- **No se elimina físicamente** (soft-delete auditado vía `ISoftDeleteAuditor`).
- Ubicación actual desnormalizada en `current_location_id`; el histórico vive en `asset_movements`.
- **Concurrencia optimista con `version`**: `PUT /{assetId}`, `PATCH /{assetId}/status` y los comandos de movimiento, asignación y devolución reciben `expectedVersion` (query) con la versión del activo que el cliente vio. Si no coincide con la versión actual: `409 Conflicto de concurrencia` y no se escribe nada (la comparación ocurre antes de abrir la transacción). Un `expectedVersion` ausente o ≤ 0 da `400` de validación. Las respuestas de escritura devuelven la versión nueva del activo (`version` en `PUT`/`PATCH`, `assetVersion` en los otros tres).
- **Movimientos:** el destino no puede coincidir con la ubicación actual; cada movimiento registra origen/destino y es inmutable (no se borra para corregir).
- **Asignaciones:** un activo solo tiene una asignación activa (`returned_at IS NULL`); debe devolverse antes de reasignar. El usuario asignado debe existir y estar activo.

## Datos

`asset`, `asset_type`, `asset_status`, `asset_assignment`, `asset_component`, `asset_movement`, `asset_os_history`, `asset_relation`, `asset_service`, `asset_software`, `asset_specification`, `asset_warranty`, `network_interface`, `inventory_record` (ver `DIAGRAMA_ER.md`).

## Criterios de aceptación

- [x] CRUD con validación (FluentValidation) y paginación
- [x] Transiciones de estado válidas únicamente
- [x] Bloqueo de liberación con incidentes abiertos
- [x] Auditoría de cambios (audit_log) y soft-delete
- [x] Movimientos de ubicación con historial inmutable
- [x] Asignaciones con una activa por activo, devolución auditada
- [x] Concurrencia optimista en movimientos y asignaciones (`expectedVersion`, 409, versión nueva en la respuesta)
- [ ] Escáner QR en frontend (FASE 3)

## Implementación

- Feature: `backend/Features/Assets/` (GetAssets, GetAssetById, CreateAsset, UpdateAsset, UpdateAssetStatus, `Shared/AssetStatusChanger`)
- Garantías: `backend/Features/Procurement/Warranties/`
