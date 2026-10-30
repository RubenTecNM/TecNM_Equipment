# Spec — Incidentes (Incidents)

> Actualizada para coincidir con el controlador real
> (`backend/Features/Incidents/IncidentsController.cs`).

## Propósito

Levantamiento, asignación y seguimiento de fallas reportadas sobre activos,
con resolución y eliminación lógica de incidentes cerrados. Cada operación
que cambia el estado del incidente actualiza el estado del activo en la
misma transacción explícita.

## Endpoints

| Método | Ruta | Permiso | Descripción |
| --- | --- | --- | --- |
| `GET` | `/api/incidents` | `incidents.view` | Listado paginado con filtros |
| `GET` | `/api/incidents/{incidentId}` | `incidents.view` | Detalle |
| `POST` | `/api/incidents` | `incidents.create` | Levanta incidente (estado inicial `ABIERTO`) |
| `PUT` | `/api/incidents/{incidentId}/assignment?expectedVersion=N` | `incidents.resolve` | Asigna a técnico (`assigned_to`) |
| `PATCH` | `/api/incidents/{incidentId}/status?expectedVersion=N` | `incidents.resolve` | Cambia estado (transiciones manuales) |
| `POST` | `/api/incidents/{incidentId}/resolve?expectedVersion=N` | `incidents.resolve` | Resuelve (diagnóstico + solución + `resolved_at`) |
| `DELETE` | `/api/incidents/{incidentId}?expectedVersion=N` | `incidents.delete` | Eliminación lógica (resueltos y cerrados, auditada) |

- Todas las mutaciones llevan `expectedVersion` (concurrencia optimista;
  409 si no coincide con la versión del servidor).
- Listado paginado: `pageNumber` (def. 1) y `pageSize` (def. 20, máx. 100).

## Filtros de listado

`assetId` (404 si el activo no existe), `status` (código), `priority`
(código), `incidentTypeId`, `assignedTo`, `onlyOpen` (abierto/diagnóstico/
reparación), `search` (texto libre sobre el **título**).

## Flujo de estados del incidente

`ABIERTO → EN_DIAGNOSTICO → EN_REPARACION ⇄ EN_DIAGNOSTICO`; `RESUELTO`
(solo vía `/resolve`, porque exige la solución) `→ CERRADO` (terminal).

Destinos manuales válidos en `PATCH /status`: `EN_DIAGNOSTICO`,
`EN_REPARACION`, `CERRADO`.

## Efecto sobre el estado del activo (misma transacción)

- **Reportar**: activo `DISPONIBLE`/`ASIGNADO` → `CON_FALLA`; si ya estaba
  `CON_FALLA`/`EN_MANTENIMIENTO`, no cambia. Prohibido sobre `DADO_DE_BAJA`.
- **Iniciar reparación** (`PATCH` a `EN_REPARACION`): activo →
  `EN_MANTENIMIENTO`. Prohibido sobre `DADO_DE_BAJA`.
- **Resolver**: si no quedan otros incidentes abiertos del activo y está
  `CON_FALLA`/`EN_MANTENIMIENTO` → `ASIGNADO` (si hay asignación vigente)
  o `DISPONIBLE`. La respuesta indica `released` y `otherOpenIncidents`.
- **Eliminar**: no restaura el estado del activo.

## Reglas de negocio

- Todo incidente se reporta **contra un activo existente** (`asset_id`
  obligatorio) y lo reporta un usuario (`reported_by`).
- Asignar: no sobre resueltos/cerrados; el destino debe ser un usuario
  **activo** cuyo rol tenga `incidents.resolve` (ADMIN/TECNICO).
- Resolver exige **solución** (obligatoria, ≤ 4000) y diagnóstico
  (opcional, ≤ 4000); sella `resolved_at`.
- Título obligatorio (≤ 200); descripción opcional (≤ 4000); motivo de
  cambio de estado opcional (≤ 500).
- Eliminar solo `RESUELTO`/`CERRADO`; la eliminación queda registrada en
  `audit_log` (`SoftDeleteAuditor`).
- Estado, prioridad y tipo son catálogos (`incident_status`,
  `incident_priority`, `incident_type`), servidos por `GET /api/catalogs`.

## Permisos por rol

De `scripts/seed-permissions.sql` y `scripts/add-incidents-permissions.sql`:

| Rol | view | create | resolve | delete |
| --- | --- | --- | --- | --- |
| ADMIN | ✅ | ✅ | ✅ | ✅ |
| TECNICO | ✅ | ✅ | ✅ | ❌ |
| RESPONSABLE_AREA | ✅ | ✅ | ❌ | ❌ |
| USUARIO_GENERAL | ❌ | ✅ | ❌ | ❌ |

`USUARIO_GENERAL` puede reportar desde la ficha del activo, pero no ve el
módulo (deuda registrada en AGENTS.md: "mis incidentes").

## Evidencias (`Features/Documents`, `ownerType = INCIDENT`)

| Método | Ruta | Descripción |
| --- | --- | --- |
| `POST` | `/api/documents` (multipart) | Sube evidencia (≤ 5 MB; `image/jpeg`, `image/png`, `application/pdf`) |
| `GET` | `/api/documents?ownerType=INCIDENT&ownerId={incidentId}` | Lista |
| `GET` | `/api/documents/{documentId}/download` | Descarga binaria |
| `DELETE` | `/api/documents/{documentId}?expectedVersion=N` | Eliminación lógica |

Permiso: basta `incidents.create` **o** `incidents.resolve`
(`DocumentAuthorizationMap`).

## Datos

`incident`, `incident_type`, `incident_priority`, `incident_status`
(+ auditoría y soft-delete estándar).

## Criterios de aceptación

- [x] CRUD con validación, filtros y paginación
- [x] Asignación y resolución con trazabilidad de usuario
- [x] Eliminación lógica solo de resueltos/cerrados, con auditoría
- [x] Efecto sobre el estado del activo en la misma transacción
- [ ] Seguimiento en tiempo real / notificaciones (backlog)

## Implementación

- Backend: `backend/Features/Incidents/` (CreateIncident, GetIncidents,
  GetIncidentById, AssignIncident, UpdateIncidentStatus, ResolveIncident,
  DeleteIncident).
- Frontend: `frontend/src/modules/incidents/` (lista con filtros, detalle
  con evidencias, reportar, acciones de técnico).
