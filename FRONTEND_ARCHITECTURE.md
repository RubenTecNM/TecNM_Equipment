# Arquitectura Frontend — TecNM Assets Management

SPA construida con **React 19 + TypeScript + Vite 8 + Tailwind CSS 4**, organizada en tres capas: `app` (global), `modules` (dominio) y `shared` (reutilizable).

## Estructura de carpetas

```text
frontend/
├── scripts/                     # Validaciones / scripts de build
├── src/
│   ├── app/                     # Configuración global
│   │   ├── layouts/
│   │   │   ├── MainLayout.tsx   # Shell con navegación (autenticado)
│   │   │   └── AuthLayout.tsx   # Shell minimalista (login)
│   │   ├── router/index.tsx     # Enrutador de la SPA
│   │   ├── App.tsx
│   │   └── main.tsx             # Entry point
│   ├── modules/                 # Módulos por dominio (aislados)
│   │   ├── auth/                # Login y permisos RBAC
│   │   ├── assets/              # Vistas de activos, escáner QR, detalles
│   │   ├── locations/           # Árbol de navegación de ubicaciones
│   │   ├── incidents/           # Levantamiento y seguimiento de fallas
│   │   ├── maintenance/         # Calendario e intervenciones
│   │   ├── disposal/            # Generación y firma de actas de baja
│   │   ├── bulk-import/         # Asistente de carga masiva de Excel
│   │   ├── reports/             # Generador de reportes
│   │   ├── dashboard/           # Métricas e indicadores visuales
│   │   ├── software/            # Licencias y programas
│   │   ├── components/          # Hardware interno
│   │   ├── documents/           # Evidencias y archivos
│   │   ├── history/             # Auditoría y timeline
│   │   ├── users/               # Usuarios
│   │   ├── roles/               # Roles y permisos
│   │   └── procurement/         # Proveedores, garantías, adquisiciones
│   └── shared/                  # Transversal
│       ├── api/http.ts          # Cliente HTTP (fetch wrapper + interceptor de JWT)
│       ├── assets/              # Logos, SVG, imágenes
│       ├── components/          # UI kit reutilizable (UiButton, UiTable, UiModal…)
│       ├── hooks/               # Custom hooks (useAuth, useTheme…)
│       ├── styles/              # Tailwind (CSS-first) y global.css
│       └── utils/               # Formateadores y helpers
├── .env.example
├── .gitignore
├── index.html
├── package.json
├── tsconfig.json / tsconfig.app.json / tsconfig.node.json
└── vite.config.ts
```

## Principios

1. **`modules/` es soberano:** cada módulo contiene sus páginas, componentes específicos, tipos y llamadas a API. No importa de otro módulo; lo compartido vive en `shared/`.
2. **`shared/` no conoce el dominio:** solo UI kit, cliente HTTP, hooks genéricos y utilidades.
3. **`app/` orquesta:** layouts, router y bootstrap. No contiene lógica de negocio.

## Estilo y configuración

- **Tailwind CSS v4** con configuración **CSS-first** (`@import "tailwindcss"` y `@theme` en `src/shared/styles/index.css`). No se utiliza `tailwind.config.js` (obsoleto en v4).
- ESLint + typescript-eslint (config estricto: `noUnusedLocals`, `noUnusedParameters`, `verbatimModuleSyntax`).

## API

- `shared/api/http.ts`: cliente base sobre `fetch` con:
  - `baseURL` desde `import.meta.env.VITE_API_BASE_URL`
  - inyección automática del header `Authorization: Bearer <token>`
  - manejo centralizado de `401` (redirección a login)

## Convenciones

- Componentes de UI kit: prefijo `Ui` (`UiButton`, `UiTable`, `UiModal`, `UiInput`, `UiSelect`).
- Hooks: prefijo `use` (`useAuth`, `useAssets`).
- Tipos de API en `modules/<modulo>/types.ts`; nada de `any` en llamadas.
- Variables de entorno: solo las expuestas con prefijo `VITE_`.

## Comandos

```bash
cd frontend
npm install
npm run dev       # servidor de desarrollo (Vite)
npm run build     # tsc -b && vite build
npm run lint      # eslint
```
