import { http } from '../../shared/api/http'
import type {
  CreateLocationInput,
  CreateLocationResponse,
  DeleteLocationResponse,
  LocationDetail,
  LocationItem,
  LocationTypeItem,
  UpdateLocationInput,
  UpdateLocationResponse,
} from './types'

// Catálogo base para el desplegable de tipos de ubicación.
// GET /api/catalogs solo exige estar autenticado (sin permiso
// específico), así que cualquier formulario puede llenarlo.
interface LocationCatalogsResponse {
  locationTypes: LocationTypeItem[]
}

export const locationsApi = {
  // Lista plana ordenada por código; el árbol se arma del lado
  // del cliente a partir de parentLocationId (decisión de diseño
  // del backend: el catálogo de un plantel es acotado).
  list() {
    return http.get<LocationItem[]>('/api/locations')
  },
  getById(locationId: number) {
    return http.get<LocationDetail>(`/api/locations/${locationId}`)
  },
  create(input: CreateLocationInput) {
    return http.post<CreateLocationResponse>('/api/locations', input)
  },
  update(locationId: number, expectedVersion: number, input: UpdateLocationInput) {
    return http.put<UpdateLocationResponse>(
      `/api/locations/${locationId}?expectedVersion=${expectedVersion}`,
      input,
    )
  },
  remove(locationId: number, expectedVersion: number) {
    return http.delete<DeleteLocationResponse>(
      `/api/locations/${locationId}?expectedVersion=${expectedVersion}`,
    )
  },
}

export function fetchLocationTypes() {
  return http.get<LocationCatalogsResponse>('/api/catalogs').then(
    (catalogs) => catalogs.locationTypes,
  )
}
