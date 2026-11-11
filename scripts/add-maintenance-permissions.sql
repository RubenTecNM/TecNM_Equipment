-- ============================================================
-- PERMISOS: módulo Mantenimiento (M.1)
-- Idempotente: seguro de re-ejecutar (ON CONFLICT DO NOTHING)
--
--   maintenance.view    ver listado, detalle y calendario
--                       ADMIN, TECNICO, RESPONSABLE_AREA
--   maintenance.manage  programar, iniciar, cerrar, cancelar y editar
--                       ADMIN, TECNICO (ya existía en seed-permissions.sql)
--   maintenance.delete  baja lógica de registros creados por error
--                       solo ADMIN
-- ============================================================

-- maintenance.manage ya existe (seed-permissions.sql): la fila idéntica no se
-- duplica (code es UNIQUE) y evita que un entorno sin ella pierda las asignaciones.
INSERT INTO inventario.permissions (code, name, description) VALUES
  ('maintenance.manage', 'Administrar mantenimientos', 'Registrar y programar mantenimientos'),
  ('maintenance.view',   'Ver mantenimientos',         'Consultar el listado, el detalle y el calendario de mantenimientos'),
  ('maintenance.delete', 'Eliminar mantenimientos',    'Baja lógica de registros de mantenimiento creados por error')
ON CONFLICT (code) DO NOTHING;

INSERT INTO inventario.role_permissions (role_id, permission_id)
SELECT r.role_id, p.permission_id
FROM inventario.roles r
CROSS JOIN inventario.permissions p
WHERE (r.code = 'ADMIN'
         AND p.code IN ('maintenance.view', 'maintenance.manage', 'maintenance.delete'))
   OR (r.code = 'TECNICO'
         AND p.code IN ('maintenance.view', 'maintenance.manage'))
   OR (r.code = 'RESPONSABLE_AREA'
         AND p.code = 'maintenance.view')
ON CONFLICT DO NOTHING;
