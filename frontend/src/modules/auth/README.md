# Módulo auth

Login JWT y acceso según permisos RBAC.

## Implementado (FASE 3, slice inicial)

- `pages/Login.tsx` — formulario con etiquetas visibles, error inline (`role="alert"`), estado de envío, foco automático
- `shared/hooks/AuthProvider.tsx` — `AuthProvider` (sesión en localStorage, valida `expiresAt`)
- `shared/hooks/useAuth.ts` — hook `useAuth()` (contexto de sesión; archivo aparte del provider por react-refresh)
- Ruta `/login` en `AuthLayout`; rutas protegidas por `RequireAuth` en `app/router`

## Pendiente

- Menú filtrado por permisos reales (hoy filtra por `roleCode`; el token no incluye lista de permisos)
- Recuperación de acceso / cambio de contraseña

Especificación: `specs/auth.spec.md`
