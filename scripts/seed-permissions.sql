-- ============================================================
-- SEED: Catálogo de permisos + asignación inicial por rol
-- Idempotente: seguro de re-ejecutar (ON CONFLICT DO NOTHING)
-- ============================================================

SET search_path TO inventario, public;

INSERT INTO permissions (code, name, description) VALUES
  ('assets.view',        'Ver activos',                    'Consultar el inventario de activos'),
  ('assets.create',      'Registrar activos',               'Dar de alta nuevos activos'),
  ('assets.update',      'Editar activos',                  'Modificar datos de activos existentes'),
  ('assets.delete',      'Dar de baja activos',              'Ejecutar el flujo formal de baja de activos'),
  ('assets.move',        'Reubicar activos',                'Registrar movimientos de ubicación'),
  ('locations.manage',   'Administrar ubicaciones',          'Alta, edición y baja del árbol de ubicaciones'),
  ('components.manage',  'Administrar componentes internos', 'Gestionar componentes y su historial de reemplazo'),
  ('software.manage',    'Administrar software/licencias',   'Gestionar software instalado y licencias'),
  ('incidents.create',   'Reportar incidentes',              'Registrar nuevos incidentes'),
  ('incidents.resolve',  'Diagnosticar/resolver incidentes', 'Atender y cerrar incidentes'),
  ('maintenance.manage', 'Administrar mantenimientos',       'Registrar y programar mantenimientos'),
  ('users.register',     'Registrar usuarios',               'Dar de alta nuevos usuarios del sistema'),
  ('roles.manage',       'Administrar roles y permisos',     'Crear roles y asignar permisos'),
  ('reports.view',       'Ver/exportar reportes',            'Acceso a reportes exportables'),
  ('dashboard.view',     'Ver dashboard',                    'Acceso al dashboard de métricas')
ON CONFLICT (code) DO NOTHING;

-- ADMIN: todos los permisos
INSERT INTO role_permissions (role_id, permission_id)
SELECT r.role_id, p.permission_id
FROM roles r
CROSS JOIN permissions p
WHERE r.code = 'ADMIN'
ON CONFLICT DO NOTHING;

-- TECNICO
INSERT INTO role_permissions (role_id, permission_id)
SELECT r.role_id, p.permission_id
FROM roles r
CROSS JOIN permissions p
WHERE r.code = 'TECNICO'
  AND p.code IN (
    'assets.view','assets.update','assets.move','assets.create',
    'components.manage','software.manage',
    'incidents.create','incidents.resolve',
    'maintenance.manage','dashboard.view'
  )
ON CONFLICT DO NOTHING;

-- RESPONSABLE_AREA
INSERT INTO role_permissions (role_id, permission_id)
SELECT r.role_id, p.permission_id
FROM roles r
CROSS JOIN permissions p
WHERE r.code = 'RESPONSABLE_AREA'
  AND p.code IN ('assets.view','incidents.create','reports.view','dashboard.view')
ON CONFLICT DO NOTHING;

-- USUARIO_GENERAL
INSERT INTO role_permissions (role_id, permission_id)
SELECT r.role_id, p.permission_id
FROM roles r
CROSS JOIN permissions p
WHERE r.code = 'USUARIO_GENERAL'
  AND p.code IN ('assets.view','incidents.create')
ON CONFLICT DO NOTHING;
