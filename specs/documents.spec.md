# Spec — Documentos (Documents)

## Propósito

Gestión de evidencias y archivos asociados a registros del sistema (fotografías, facturas, actas), con control de acceso por propietario.

## Endpoints

| Método | Ruta | Permiso | Descripción |
| --- | --- | --- | --- |
| `POST` | `/api/documents/upload` | `documents.create` | Sube archivo |
| `GET` | `/api/documents/owner/{ownerId}` | `documents.view` | Documentos de un propietario |
| `GET` | `/api/documents/{documentId}/download` | `documents.view` | Descarga |
| `DELETE` | `/api/documents/{documentId}` | `documents.delete` | Eliminación lógica |

## Reglas de negocio

- Almacenamiento en disco local (`backend/storage/documents/`, configurable por `FileStorage:LocalPath`).
- Acceso autorizado por `IDocumentAccessAuthorizer` (propietario o permiso de administrador).
- Metadatos en `document` (nombre, tipo MIME, tamaño, hash, propietario, entidad relacionada).

## Criterios de aceptación

- [x] Upload / descarga / listado por propietario / eliminación lógica
- [ ] Vinculación polimórfica a activos/incidentes (FASE 2+)

## Implementación

- Feature: `backend/Features/Documents/`
- Cross-cutting: `Common/Storage/LocalDiskFileStorageService`, `Common/Authorization/DocumentAccessAuthorizer`
