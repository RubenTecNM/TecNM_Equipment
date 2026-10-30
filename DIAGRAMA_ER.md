# Diagrama ER — TecNM Assets Management

Resumen de las entidades principales y sus relaciones (el esquema autoritativo es `database.sql` en la raíz; vistas materializadas en azul).

```mermaid
erDiagram
    APP_USER }o--|| ROLE : "tiene"
    APP_USER ||--o{ AUDIT_LOG : "realiza"
    APP_USER ||--o{ INCIDENT : "reporta"
    APP_USER ||--o{ INCIDENT : "asignado_a"
    APP_USER ||--o{ ASSET : "crea / actualiza / elimina"
    ROLE ||--o{ ROLE_PERMISSION : "tiene"
    PERMISSION ||--o{ ROLE_PERMISSION : "otorga"

    ASSET }o--|| ASSET_TYPE : "tipo"
    ASSET }o--|| ASSET_STATUS : "estado"
    ASSET }o--|| MANUFACTURER : "fabricante"
    ASSET }o--|| MODEL : "modelo"
    ASSET }o--|| LOCATION : "ubicacion_actual"
    ASSET ||--o{ ASSET_ASSIGNMENT : "asignaciones"
    ASSET ||--o{ ASSET_COMPONENT : "componentes"
    ASSET ||--o{ ASSET_MOVEMENT : "movimientos"
    ASSET ||--o{ ASSET_WARRANTY : "garantias"
    ASSET ||--o{ ASSET_SOFTWARE : "software_instalado"
    ASSET ||--o{ ASSET_SERVICE : "servicios"
    ASSET ||--o{ ASSET_SPECIFICATION : "especificaciones"
    ASSET ||--o{ ASSET_RELATION : "relaciones"
    ASSET ||--o{ NETWORK_INTERFACE : "interfaces_red"
    ASSET ||--o{ INVENTORY_RECORD : "conteo_fisico"
    ASSET ||--o{ INCIDENT : "incidentes"
    ASSET ||--o{ MAINTENANCE_RECORD : "mantenimientos"
    ASSET ||--o| ASSET_DISPOSAL : "baja"
    ASSET ||--o| ASSET_OS_HISTORY : "historico_SO"

    LOCATION }o--|| LOCATION_TYPE : "tipo"
    LOCATION ||--o{ LOCATION : "arbol_padre_hijo"
    LOCATION ||--o{ BUILDING_SETTING : "configuracion_edificio"

    ACQUISITION ||--o{ ACQUISITION_ASSET : "incluye"
    ACQUISITION_ASSET }o--|| ASSET : "adquirido"
    ACQUISITION }o--|| SUPPLIER : "proveedor"

    MAINTENANCE_RECORD }o--|| MAINTENANCE_TYPE : "tipo"
    ASSET_DISPOSAL }o--|| DISPOSAL_REASON : "motivo"
    ASSET_ASSIGNMENT }o--|| ASSIGNMENT_TYPE : "tipo"
    ASSET_MOVEMENT }o--|| MOVEMENT_REASON : "motivo"
    ASSET_COMPONENT }o--|| COMPONENT_TYPE : "tipo"
    ASSET_RELATION }o--|| RELATION_TYPE : "tipo"

    SOFTWARE ||--o{ SOFTWARE_LICENSE : "licencias"
    SOFTWARE }o--|| SOFTWARE_TYPE : "tipo"
    SOFTWARE_LICENSE }o--|| LICENSE_TYPE : "tipo"
    SOFTWARE_LICENSE ||--o{ SOFTWARE_LICENSE_ASSIGNMENT : "asignaciones"
    SOFTWARE_LICENSE_ASSIGNMENT }o--|| ASSET : "maquina"
    SOFTWARE_LICENSE_ASSIGNMENT }o--|| APP_USER : "usuario"

    INVENTORY_SESSION ||--o{ INVENTORY_RECORD : "registros"

    INCIDENT }o--|| INCIDENT_TYPE : "tipo"
    INCIDENT }o--|| INCIDENT_PRIORITY : "prioridad"
    INCIDENT }o--|| INCIDENT_STATUS : "estado"

    DOCUMENT }o--|| APP_USER : "propietario"

    MODEL }o--|| MANUFACTURER : "fabricante"

    V_ASSET_SUMMARY ..> ASSET : "vista_materializada"
    V_LOCATION_EQUIPMENT_SUMMARY ..> LOCATION : "vista_materializada"
```

## Notas de diseño

- **`asset`** es la entidad central: código de inventario estable (`inventory_code`, ej. `TEC-PC-0001`), histórico de ubicaciones en `asset_movements` y ubicación desnormalizada en `current_location_id` para consulta rápida.
- **Soft-delete con auditoría:** tablas transaccionales tienen `is_deleted`, `deleted_at`, `deleted_by` (+ `created_*`, `updated_*`, `version` para concurrencia). `audit_log` es inmutable.
- **`asset_disposal`** es 1:1 opcional con `asset` (un activo se da de baja una sola vez, mediante workflow formal).
- **Árbol de ubicaciones:** `location.parent_location_id` autorreferenciado (Edificio → Área → Rack).
- **Catálogos:** `asset_type`, `asset_status`, `manufacturer`, `model`, `location_type`, `maintenance_type`, `incident_type`, `incident_priority`, `incident_status`, `disposal_reason`, `movement_reason`, `assignment_type`, `component_type`, `relation_type`, `service`, `software_type`, `license_type`, `supplier` son tablas paramétricas sembradas desde `scripts/seed-database.sql`.
- **Vistas:** `v_asset_summary` y `v_location_equipment_summary` para consultas de dashboard/reportes.
