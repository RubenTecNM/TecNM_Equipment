# Módulo assets

Lista de activos y ficha detallada con movimientos de ubicación y asignaciones.

## Implementado

- `pages/AssetsPage.tsx` — tabla paginada (código, serie, hostname), enlace a ficha
- `pages/AssetDetailPage.tsx` — ficha: datos generales, badges de tipo/estado, historial de movimientos con paginación y formulario de registro, asignaciones con formulario y devolución
- `api.ts` — cliente del módulo sobre `shared/api/http`
- `types.ts` — tipos de la API
- `shared/utils/format.ts` — formateo de fechas (es-MX)

## Reglas reflejadas

- Una sola asignación activa por activo (el backend bloquea duplicados)
- Movimiento con destino distinto a la ubicación actual
- Selector de usuario con búsqueda (top 50 activos, `users.view`)

Especificación: `specs/assets.spec.md`
