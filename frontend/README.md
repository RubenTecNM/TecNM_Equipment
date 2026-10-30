# Frontend — TecNM Assets Management

SPA para la gestión de activos tecnológicos del TecNM. React 19 + TypeScript + Vite 8 + Tailwind CSS 4.

## Estructura

- `src/app/` — configuración global: layouts (`MainLayout`, `AuthLayout`), enrutador, `App.tsx` y `main.tsx`
- `src/modules/` — módulos por dominio (auth, assets, locations, incidents, maintenance, disposal, bulk-import, reports, dashboard, software, components, documents, history, users, roles, procurement)
- `src/shared/` — transversal: `api/http.ts` (cliente HTTP + JWT), UI kit (`components/`), `hooks/`, `styles/` (Tailwind CSS-first), `utils/`, `assets/`

Ver `FRONTEND_ARCHITECTURE.md` en la raíz del repo para la descripción completa.

## Comandos

```bash
npm install
npm run dev      # servidor de desarrollo
npm run build    # tsc -b && vite build
npm run lint     # eslint
```

## Configuración

Copia `.env.example` a `.env`:

```bash
VITE_API_BASE_URL=http://localhost:5112   # API .NET 10
```

## Fases

- FASE 1 (backend): base de datos y autenticación — ver `plans/01-database-and-auth-plan.md`
- FASE 2 (backend): núcleo de activos — ver `plans/02-assets-core-plan.md`
- FASE 3 (este proyecto): SPA — ver `plans/03-frontend-spa-plan.md`

> **Nota sobre Tailwind:** la configuración es CSS-first (Tailwind v4, `@theme` en
> `src/shared/styles/index.css`); no se utiliza `tailwind.config.js`.
