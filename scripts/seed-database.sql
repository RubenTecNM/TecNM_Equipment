SET search_path TO inventario, public;

-- asset_types (códigos alineados con los que ya usa la vista v_location_equipment_summary)
INSERT INTO asset_types (code, name, created_by) VALUES
  ('PC', 'Computadora de Escritorio', 1),
  ('LAPTOP', 'Laptop', 1),
  ('SERVER', 'Servidor', 1),
  ('SWITCH', 'Switch de Red', 1),
  ('ACCESS_POINT', 'Punto de Acceso', 1),
  ('ROUTER', 'Router', 1),
  ('PRINTER', 'Impresora', 1),
  ('MONITOR', 'Monitor', 1),
  ('PROJECTOR', 'Proyector', 1),
  ('RACK', 'Rack', 1),
  ('UPS', 'UPS / No Break', 1),
  ('CAMERA', 'Cámara de Videovigilancia', 1),
  ('OTHER', 'Otro', 1)
ON CONFLICT DO NOTHING;

-- location_types (jerarquía: Edificio -> Planta -> Área/Lab/Oficina -> Rack/Sububicación)
INSERT INTO location_types (code, name, created_by) VALUES
  ('EDIFICIO', 'Edificio', 1),
  ('PLANTA', 'Planta / Piso', 1),
  ('AREA', 'Área', 1),
  ('LABORATORIO', 'Laboratorio', 1),
  ('OFICINA', 'Oficina', 1),
  ('RACK', 'Rack', 1),
  ('SUBUBICACION', 'Sububicación', 1)
ON CONFLICT DO NOTHING;

-- component_types
INSERT INTO component_types (code, name, created_by) VALUES
  ('CPU', 'Procesador', 1),
  ('RAM', 'Memoria RAM', 1),
  ('ALMACENAMIENTO', 'Almacenamiento (HDD/SSD)', 1),
  ('GPU', 'Tarjeta Gráfica', 1),
  ('FUENTE_PODER', 'Fuente de Poder', 1),
  ('TARJETA_MADRE', 'Tarjeta Madre', 1),
  ('OTRO', 'Otro Componente', 1)
ON CONFLICT DO NOTHING;

-- incident_types
INSERT INTO incident_types (code, name, created_by) VALUES
  ('HARDWARE', 'Falla de Hardware', 1),
  ('SOFTWARE', 'Falla de Software', 1),
  ('RED', 'Problema de Red', 1),
  ('ROBO_EXTRAVIO', 'Robo o Extravío', 1),
  ('OTRO', 'Otro', 1)
ON CONFLICT DO NOTHING;

-- incident_priorities
INSERT INTO incident_priorities (code, name, created_by) VALUES
  ('BAJA', 'Baja', 1),
  ('MEDIA', 'Media', 1),
  ('ALTA', 'Alta', 1),
  ('CRITICA', 'Crítica', 1)
ON CONFLICT DO NOTHING;

-- incident_statuses
INSERT INTO incident_statuses (code, name, created_by) VALUES
  ('ABIERTO', 'Abierto', 1),
  ('EN_DIAGNOSTICO', 'En Diagnóstico', 1),
  ('EN_REPARACION', 'En Reparación', 1),
  ('RESUELTO', 'Resuelto', 1),
  ('CERRADO', 'Cerrado', 1)
ON CONFLICT DO NOTHING;

-- maintenance_types
INSERT INTO maintenance_types (code, name, created_by) VALUES
  ('PREVENTIVO', 'Mantenimiento Preventivo', 1),
  ('CORRECTIVO', 'Mantenimiento Correctivo', 1)
ON CONFLICT DO NOTHING;

-- relation_types
INSERT INTO relation_types (code, name, created_by) VALUES
  ('PADRE_HIJO', 'Relación Padre-Hijo', 1),
  ('PERIFERICO_DE', 'Periférico De', 1),
  ('CONECTADO_A', 'Conectado A', 1),
  ('OTRO', 'Otra Relación', 1)
ON CONFLICT DO NOTHING;

-- assignment_types
INSERT INTO assignment_types (code, name, created_by) VALUES
  ('PERSONAL', 'Asignación Personal', 1),
  ('AREA', 'Asignación a Área', 1),
  ('TEMPORAL', 'Asignación Temporal', 1)
ON CONFLICT DO NOTHING;

-- movement_reasons
INSERT INTO movement_reasons (code, name, created_by) VALUES
  ('REUBICACION', 'Reubicación', 1),
  ('PRESTAMO', 'Préstamo', 1),
  ('MANTENIMIENTO', 'Traslado por Mantenimiento', 1),
  ('BAJA', 'Traslado por Baja', 1),
  ('OTRO', 'Otro Motivo', 1)
ON CONFLICT DO NOTHING;

-- software_types
INSERT INTO software_types (code, name, created_by) VALUES
  ('SISTEMA_OPERATIVO', 'Sistema Operativo', 1),
  ('OFIMATICA', 'Ofimática', 1),
  ('ANTIVIRUS', 'Antivirus', 1),
  ('DESARROLLO', 'Herramienta de Desarrollo', 1),
  ('OTRO', 'Otro Software', 1)
ON CONFLICT DO NOTHING;

-- license_types
INSERT INTO license_types (code, name, created_by) VALUES
  ('PERPETUA', 'Licencia Perpetua', 1),
  ('SUSCRIPCION', 'Suscripción', 1),
  ('OEM', 'OEM', 1),
  ('VOLUMEN', 'Licencia por Volumen', 1),
  ('FREEWARE', 'Gratuita / Freeware', 1)
ON CONFLICT DO NOTHING;
