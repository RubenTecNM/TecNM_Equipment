# Spec — Ubicaciones (Locations)

## Propósito

Árbol jerárquico de espacios físicos del campus: **Edificio → Área → Rack** (autorreferenciado por `parent_location_id`), usado como ubicación actual de los activos.

## Endpoints

| Método | Ruta | Permiso | Descripción |
| --- | --- | --- | --- |
| `GET` | `/api/locations` | `locations.view` | Árbol / listado |
| `GET` | `/api/locations/{locationId}` | `locations.view` | Detalle |
| `POST` | `/api/locations` | `locations.create` | Crea nodo |
| `PUT` | `/api/locations/{locationId}` | `locations.update` | Edita nodo |
| `DELETE` | `/api/locations/{locationId}` | `locations.delete` | Eliminación lógica |

## Reglas de negocio

- Código (`code`) único y nombre obligatorio; tipo vía `location_type`.
- Un nodo solo puede eliminarse si no tiene hijos ni activos asociados.
- La ubicación actual de un activo (`asset.current_location_id`) apunta a un nodo hoja o intermedio; los cambios históricos se registran en `asset_movements`.
- `building_setting` almacena configuración propia de nodos tipo edificio.

## Criterios de aceptación

- [x] CRUD de nodos con validación
- [x] Estructura de árbol padre/hijo
- [x] Protección contra eliminación con hijos/activos

## Implementación

- Feature: `backend/Features/Locations/` (CreateLocation, GetLocations, GetLocationById, UpdateLocation, DeleteLocation)
- Vista: `v_location_equipment_summary` (equipos por ubicación, para dashboard/reportes)
