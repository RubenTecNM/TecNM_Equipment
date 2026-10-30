# Spec — Carga masiva (BulkImport)

## Propósito

Asistente de importación masiva de activos desde **Excel/CSV** con validación previa y previsualización.

## Flujo

1. Selección de archivo (`.xlsx`, `.csv`)
2. Parseo y validación por fila (catálogos existentes: tipo, fabricante, modelo, ubicación)
3. Previsualización con errores por fila
4. Confirmación e inserción transaccional (todo o nada)

## Reglas de negocio

- Permiso `bulkimport.create` (o equivalente `assets.create`).
- Duplicados por `inventory_code` o `serial_number` se reportan, no se insertan.
- Transacción única; falla de una fila rechaza el lote con reporte detallado.

## Criterios de aceptación

- [ ] Plantilla de importación descargable
- [ ] Import con validación y reporte de errores (pendiente)

## Implementación

- Feature: `backend/Features/BulkImport/`
- Módulo frontend: `frontend/src/modules/bulk-import/`
