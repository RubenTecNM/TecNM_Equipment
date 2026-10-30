# Plan — FASE 3: SPA Frontend

**Objetivo:** construir la aplicación React sobre la estructura modular (`app` / `modules` / `shared`) consumiendo la API de las fases 1–2.

## Alcance

### App global
- [x] `MainLayout` (navegación por módulos, chip de usuario, cierre de sesión) y `AuthLayout` (login)
- [x] Router con ruta protegida (`RequireAuth`) y redirect a /login
- [x] `shared/api/http.ts`: cliente HTTP con sesión JWT y manejo de 401
- [x] `useAuth` + sesión en localStorage con validación de expiración

### Módulos
- [x] `auth` — login (ProblemDetails → error inline)
- [x] `dashboard` — página base (métricas pendientes de endpoint FASE 2)
- [ ] `assets` — listado, filtros, detalle, ficha, máquina de estados, escáner QR
- [ ] `locations` — árbol de navegación Edificio → Área → Rack
- [ ] `incidents` — levantamiento y seguimiento de fallas
- [ ] `maintenance` — calendario e intervenciones
- [ ] `disposal` — generación y firma de actas de baja
- [ ] `bulk-import` — asistente de carga masiva
- [ ] `reports` — generador de reportes
- [ ] `software`, `components`, `documents`, `history`, `users`, `roles`, `procurement`

### UI kit (`shared/components`)
- [ ] `UiButton`, `UiInput`, `UiSelect`, `UiTable`, `UiModal`, `UiCard`, `UiBadge`

## Criterios de salida

- `npm run build` en verde; cobertura de flujos principales de `specs/`
- Prototipo navegable end-to-end contra la API

## Estado: 🟡 Estructura base creada — módulos pendientes
