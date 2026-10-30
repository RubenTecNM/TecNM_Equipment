-- ============================================================
-- SEED DE EJEMPLO: árbol de ubicaciones para desarrollo
-- (OPCIONAL, SOLO DESARROLLO — no forma parte de la instalación)
--
-- Jerarquía: Edificio B -> Planta 1 / Planta 2 -> Laboratorio y
-- Oficina en cada planta -> Rack dentro del laboratorio de la
-- Planta 1.
--
-- Idempotente: cada inserción revisa WHERE NOT EXISTS por code
-- (no depende de ON CONFLICT). Resuelve tipos y padres por su
-- código mediante subconsultas. created_by/updated_by = system
-- (app_user_id 1). NO toca ED-A ni PRUEBA-LAB (datos de prueba
-- protegidos).
-- ============================================================

SET search_path TO inventario, public;

-- Raíz: Edificio B
INSERT INTO locations (code, name, location_type_id, parent_location_id, created_by, updated_by)
SELECT 'ED-B', 'Edificio B', lt.location_type_id, NULL, 1, 1
FROM location_types lt
WHERE lt.code = 'EDIFICIO'
  AND lt.is_deleted = false
  AND NOT EXISTS (SELECT 1 FROM locations WHERE code = 'ED-B');

-- Planta 1 (hija de ED-B)
INSERT INTO locations (code, name, location_type_id, parent_location_id, created_by, updated_by)
SELECT 'ED-B-P1', 'Planta 1', lt.location_type_id,
       (SELECT location_id FROM locations WHERE code = 'ED-B'), 1, 1
FROM location_types lt
WHERE lt.code = 'PLANTA'
  AND lt.is_deleted = false
  AND NOT EXISTS (SELECT 1 FROM locations WHERE code = 'ED-B-P1')
  AND EXISTS (SELECT 1 FROM locations WHERE code = 'ED-B');

-- Planta 2 (hija de ED-B)
INSERT INTO locations (code, name, location_type_id, parent_location_id, created_by, updated_by)
SELECT 'ED-B-P2', 'Planta 2', lt.location_type_id,
       (SELECT location_id FROM locations WHERE code = 'ED-B'), 1, 1
FROM location_types lt
WHERE lt.code = 'PLANTA'
  AND lt.is_deleted = false
  AND NOT EXISTS (SELECT 1 FROM locations WHERE code = 'ED-B-P2')
  AND EXISTS (SELECT 1 FROM locations WHERE code = 'ED-B');

-- Laboratorio en Planta 1 (hija de ED-B-P1)
INSERT INTO locations (code, name, location_type_id, parent_location_id, created_by, updated_by)
SELECT 'ED-B-P1-LAB', 'Laboratorio de redes', lt.location_type_id,
       (SELECT location_id FROM locations WHERE code = 'ED-B-P1'), 1, 1
FROM location_types lt
WHERE lt.code = 'LABORATORIO'
  AND lt.is_deleted = false
  AND NOT EXISTS (SELECT 1 FROM locations WHERE code = 'ED-B-P1-LAB')
  AND EXISTS (SELECT 1 FROM locations WHERE code = 'ED-B-P1');

-- Oficina en Planta 1 (hija de ED-B-P1)
INSERT INTO locations (code, name, location_type_id, parent_location_id, created_by, updated_by)
SELECT 'ED-B-P1-OF', 'Oficina de cómputo', lt.location_type_id,
       (SELECT location_id FROM locations WHERE code = 'ED-B-P1'), 1, 1
FROM location_types lt
WHERE lt.code = 'OFICINA'
  AND lt.is_deleted = false
  AND NOT EXISTS (SELECT 1 FROM locations WHERE code = 'ED-B-P1-OF')
  AND EXISTS (SELECT 1 FROM locations WHERE code = 'ED-B-P1');

-- Rack dentro del laboratorio de la Planta 1 (hija de ED-B-P1-LAB)
INSERT INTO locations (code, name, location_type_id, parent_location_id, created_by, updated_by)
SELECT 'ED-B-P1-LAB-R1', 'Rack de comunicaciones', lt.location_type_id,
       (SELECT location_id FROM locations WHERE code = 'ED-B-P1-LAB'), 1, 1
FROM location_types lt
WHERE lt.code = 'RACK'
  AND lt.is_deleted = false
  AND NOT EXISTS (SELECT 1 FROM locations WHERE code = 'ED-B-P1-LAB-R1')
  AND EXISTS (SELECT 1 FROM locations WHERE code = 'ED-B-P1-LAB');

-- Laboratorio en Planta 2 (hija de ED-B-P2)
INSERT INTO locations (code, name, location_type_id, parent_location_id, created_by, updated_by)
SELECT 'ED-B-P2-LAB', 'Laboratorio de física', lt.location_type_id,
       (SELECT location_id FROM locations WHERE code = 'ED-B-P2'), 1, 1
FROM location_types lt
WHERE lt.code = 'LABORATORIO'
  AND lt.is_deleted = false
  AND NOT EXISTS (SELECT 1 FROM locations WHERE code = 'ED-B-P2-LAB')
  AND EXISTS (SELECT 1 FROM locations WHERE code = 'ED-B-P2');

-- Oficina en Planta 2 (hija de ED-B-P2)
INSERT INTO locations (code, name, location_type_id, parent_location_id, created_by, updated_by)
SELECT 'ED-B-P2-OF', 'Oficina de docencia', lt.location_type_id,
       (SELECT location_id FROM locations WHERE code = 'ED-B-P2'), 1, 1
FROM location_types lt
WHERE lt.code = 'OFICINA'
  AND lt.is_deleted = false
  AND NOT EXISTS (SELECT 1 FROM locations WHERE code = 'ED-B-P2-OF')
  AND EXISTS (SELECT 1 FROM locations WHERE code = 'ED-B-P2');
