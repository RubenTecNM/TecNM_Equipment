import { http } from '../../shared/api/http'
import type {
  AssignTechnicianResult,
  ChangeStatusInput,
  ChangeStatusResult,
  CreateIncidentInput,
  CreateIncidentResult,
  DeleteIncidentResult,
  IncidentCatalogs,
  IncidentDetail,
  IncidentDocument,
  IncidentFilters,
  IncidentSummary,
  PagedResult,
  ResolveIncidentResult,
  UserItem,
} from './types'

function buildQuery(params: Record<string, string | number | boolean | undefined>): string {
  const search = new URLSearchParams()
  for (const [key, value] of Object.entries(params)) {
    if (value !== undefined) search.set(key, String(value))
  }
  const query = search.toString()
  return query ? `?${query}` : ''
}

export const incidentsApi = {
  list(filters: IncidentFilters, pageNumber = 1, pageSize = 20) {
    return http.get<PagedResult<IncidentSummary>>(
      `/api/incidents${buildQuery({ pageNumber, pageSize, ...filters })}`,
    )
  },
  getById(incidentId: number) {
    return http.get<IncidentDetail>(`/api/incidents/${incidentId}`)
  },
  create(input: CreateIncidentInput) {
    return http.post<CreateIncidentResult>('/api/incidents', input)
  },
  assign(incidentId: number, expectedVersion: number, input: { assignedToUserId: number }) {
    return http.put<AssignTechnicianResult>(
      `/api/incidents/${incidentId}/assignment?expectedVersion=${expectedVersion}`,
      input,
    )
  },
  updateStatus(
    incidentId: number,
    expectedVersion: number,
    input: ChangeStatusInput,
  ) {
    return http.patch<ChangeStatusResult>(
      `/api/incidents/${incidentId}/status?expectedVersion=${expectedVersion}`,
      input,
    )
  },
  resolve(
    incidentId: number,
    expectedVersion: number,
    input: { solution: string; diagnosis?: string },
  ) {
    return http.post<ResolveIncidentResult>(
      `/api/incidents/${incidentId}/resolve?expectedVersion=${expectedVersion}`,
      input,
    )
  },
  remove(incidentId: number, expectedVersion: number) {
    return http.delete<DeleteIncidentResult>(
      `/api/incidents/${incidentId}?expectedVersion=${expectedVersion}`,
    )
  },
  // GET /api/catalogs solo exige estar autenticado: cualquier
  // formulario del módulo llena sus desplegables con esto.
  catalogs() {
    return http.get<IncidentCatalogs>('/api/catalogs')
  },
  // Técnicos asignables: el backend exige que el destino tenga
  // incidents.resolve (solo ADMIN y TECNICO), así que el
  // selector filtra el resultado de GET /api/users.
  technicians(search?: string) {
    return http
      .get<UserItem[]>(`/api/users${buildQuery({ search })}`)
      .then((users) =>
        users.filter((user) => user.roleCode === 'ADMIN' || user.roleCode === 'TECNICO'),
      )
  },
}

export const incidentDocumentsApi = {
  list(incidentId: number) {
    return http.get<IncidentDocument[]>(
      `/api/documents${buildQuery({ ownerType: 'INCIDENT', ownerId: incidentId })}`,
    )
  },
  download(documentId: number) {
    return http.download(`/api/documents/${documentId}/download`)
  },
}
