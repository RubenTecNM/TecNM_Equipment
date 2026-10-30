export interface LocationItem {
  locationId: number
  code: string
  name: string
  locationTypeCode: string
  parentLocationId: number | null
  version: number
}

export interface LocationDetail {
  locationId: number
  code: string
  name: string
  locationTypeCode: string
  parentLocationId: number | null
  breadcrumb: string[]
  version: number
}

export interface LocationTypeItem {
  id: number
  code: string
  name: string
}

export interface CreateLocationInput {
  code: string
  name: string
  locationTypeId: number
  parentLocationId?: number | null
}

export interface UpdateLocationInput {
  name: string
  locationTypeId: number
  parentLocationId?: number | null
}

export interface CreateLocationResponse {
  locationId: number
  code: string
  version: number
}

export interface UpdateLocationResponse {
  locationId: number
  version: number
}

export interface DeleteLocationResponse {
  locationId: number
  deleted: boolean
}
