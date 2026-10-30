-- ============================================================
-- TEC INVENTARIO - ESQUEMA COMPLETO Y CORREGIDO
-- Reemplaza al par (script base perdido + V2 correcciones).
-- PostgreSQL 18+
-- Ejecutar UNA sola vez sobre una base de datos vacía.
-- ============================================================

CREATE SCHEMA IF NOT EXISTS inventario;
SET search_path TO inventario, public;

-- ============================================================
-- 0. FUNCIONES DE AUDITORIA (se declaran antes que las tablas)
-- ============================================================

-- Incrementa version y refresca updated_at en cada UPDATE.
-- Se asigna dinámicamente (sección 18) a TODA tabla con columna "version".
CREATE OR REPLACE FUNCTION touch_version()
RETURNS trigger
LANGUAGE plpgsql
AS $$
BEGIN
  IF TG_OP = 'UPDATE' THEN
    NEW.updated_at := now();
    NEW.version := OLD.version + 1;
  END IF;
  RETURN NEW;
END;
$$;

-- Mantiene consistencia entre is_deleted / deleted_at / deleted_by.
-- Se asigna dinámicamente (sección 19) a TODA tabla con columna "is_deleted".
CREATE OR REPLACE FUNCTION prevent_invalid_soft_delete()
RETURNS trigger
LANGUAGE plpgsql
AS $$
BEGIN
  IF NEW.is_deleted AND NOT OLD.is_deleted THEN
    NEW.deleted_at := COALESCE(NEW.deleted_at, now());
  ELSIF NOT NEW.is_deleted THEN
    NEW.deleted_at := NULL;
    NEW.deleted_by := NULL;
  END IF;
  RETURN NEW;
END;
$$;

-- ============================================================
-- 1. SEGURIDAD: ROLES, PERMISOS, USUARIOS
-- ============================================================

CREATE TABLE IF NOT EXISTS roles (
  role_id       bigint GENERATED ALWAYS AS IDENTITY PRIMARY KEY,
  code          varchar(40) NOT NULL UNIQUE,
  name          varchar(120) NOT NULL,
  description   text,
  created_at    timestamptz NOT NULL DEFAULT now(),
  created_by    bigint,
  updated_at    timestamptz NOT NULL DEFAULT now(),
  updated_by    bigint,
  is_deleted    boolean NOT NULL DEFAULT false,
  deleted_at    timestamptz,
  deleted_by    bigint,
  version       integer NOT NULL DEFAULT 1
);

CREATE TABLE IF NOT EXISTS permissions (
  permission_id bigint GENERATED ALWAYS AS IDENTITY PRIMARY KEY,
  code          varchar(80) NOT NULL UNIQUE,
  name          varchar(150) NOT NULL,
  description   text,
  created_at    timestamptz NOT NULL DEFAULT now(),
  created_by    bigint,
  updated_at    timestamptz NOT NULL DEFAULT now(),
  updated_by    bigint,
  is_deleted    boolean NOT NULL DEFAULT false,
  deleted_at    timestamptz,
  deleted_by    bigint,
  version       integer NOT NULL DEFAULT 1
);

-- Tabla puente RBAC: un rol tiene muchos permisos (sin auditoría propia,
-- vive y muere con el rol/permiso al que pertenece).
CREATE TABLE IF NOT EXISTS role_permissions (
  role_id       bigint NOT NULL REFERENCES roles(role_id),
  permission_id bigint NOT NULL REFERENCES permissions(permission_id),
  PRIMARY KEY (role_id, permission_id)
);

-- FIX 5: un usuario = un solo rol, vía FK simple NOT NULL.
-- El catálogo roles sí admite que el admin cree tantos roles como quiera.
CREATE TABLE IF NOT EXISTS app_users (
  user_id        bigint GENERATED ALWAYS AS IDENTITY PRIMARY KEY,
  username       varchar(60) NOT NULL UNIQUE,
  email          varchar(254) NOT NULL UNIQUE,
  password_hash  text NOT NULL,
  first_name     varchar(100) NOT NULL,
  last_name      varchar(100) NOT NULL,
  role_id        bigint NOT NULL REFERENCES roles(role_id),
  status         varchar(20) NOT NULL DEFAULT 'activo'
                   CHECK (status IN ('activo','inactivo','suspendido')),
  created_at     timestamptz NOT NULL DEFAULT now(),
  created_by     bigint REFERENCES app_users(user_id),
  updated_at     timestamptz NOT NULL DEFAULT now(),
  updated_by     bigint REFERENCES app_users(user_id),
  is_deleted     boolean NOT NULL DEFAULT false,
  deleted_at     timestamptz,
  deleted_by     bigint REFERENCES app_users(user_id),
  version        integer NOT NULL DEFAULT 1
);

-- ============================================================
-- 2. CATALOGOS (mismo patrón: code, name, auditoría completa)
-- El admin agrega valores nuevos aquí sin tocar el esquema.
-- ============================================================

CREATE TABLE IF NOT EXISTS asset_types (
  asset_type_id bigint GENERATED ALWAYS AS IDENTITY PRIMARY KEY,
  code varchar(40) NOT NULL UNIQUE, name varchar(120) NOT NULL, description text,
  created_at timestamptz NOT NULL DEFAULT now(), created_by bigint REFERENCES app_users(user_id),
  updated_at timestamptz NOT NULL DEFAULT now(), updated_by bigint REFERENCES app_users(user_id),
  is_deleted boolean NOT NULL DEFAULT false, deleted_at timestamptz, deleted_by bigint REFERENCES app_users(user_id),
  version integer NOT NULL DEFAULT 1
);

CREATE TABLE IF NOT EXISTS asset_statuses (
  asset_status_id bigint GENERATED ALWAYS AS IDENTITY PRIMARY KEY,
  code varchar(40) NOT NULL UNIQUE, name varchar(120) NOT NULL, description text,
  created_at timestamptz NOT NULL DEFAULT now(), created_by bigint REFERENCES app_users(user_id),
  updated_at timestamptz NOT NULL DEFAULT now(), updated_by bigint REFERENCES app_users(user_id),
  is_deleted boolean NOT NULL DEFAULT false, deleted_at timestamptz, deleted_by bigint REFERENCES app_users(user_id),
  version integer NOT NULL DEFAULT 1
);

CREATE TABLE IF NOT EXISTS location_types (
  location_type_id bigint GENERATED ALWAYS AS IDENTITY PRIMARY KEY,
  code varchar(40) NOT NULL UNIQUE, name varchar(120) NOT NULL, description text,
  created_at timestamptz NOT NULL DEFAULT now(), created_by bigint REFERENCES app_users(user_id),
  updated_at timestamptz NOT NULL DEFAULT now(), updated_by bigint REFERENCES app_users(user_id),
  is_deleted boolean NOT NULL DEFAULT false, deleted_at timestamptz, deleted_by bigint REFERENCES app_users(user_id),
  version integer NOT NULL DEFAULT 1
);

CREATE TABLE IF NOT EXISTS component_types (
  component_type_id bigint GENERATED ALWAYS AS IDENTITY PRIMARY KEY,
  code varchar(40) NOT NULL UNIQUE, name varchar(120) NOT NULL, description text,
  created_at timestamptz NOT NULL DEFAULT now(), created_by bigint REFERENCES app_users(user_id),
  updated_at timestamptz NOT NULL DEFAULT now(), updated_by bigint REFERENCES app_users(user_id),
  is_deleted boolean NOT NULL DEFAULT false, deleted_at timestamptz, deleted_by bigint REFERENCES app_users(user_id),
  version integer NOT NULL DEFAULT 1
);

CREATE TABLE IF NOT EXISTS incident_types (
  incident_type_id bigint GENERATED ALWAYS AS IDENTITY PRIMARY KEY,
  code varchar(40) NOT NULL UNIQUE, name varchar(120) NOT NULL, description text,
  created_at timestamptz NOT NULL DEFAULT now(), created_by bigint REFERENCES app_users(user_id),
  updated_at timestamptz NOT NULL DEFAULT now(), updated_by bigint REFERENCES app_users(user_id),
  is_deleted boolean NOT NULL DEFAULT false, deleted_at timestamptz, deleted_by bigint REFERENCES app_users(user_id),
  version integer NOT NULL DEFAULT 1
);

CREATE TABLE IF NOT EXISTS incident_priorities (
  incident_priority_id bigint GENERATED ALWAYS AS IDENTITY PRIMARY KEY,
  code varchar(40) NOT NULL UNIQUE, name varchar(120) NOT NULL, description text,
  created_at timestamptz NOT NULL DEFAULT now(), created_by bigint REFERENCES app_users(user_id),
  updated_at timestamptz NOT NULL DEFAULT now(), updated_by bigint REFERENCES app_users(user_id),
  is_deleted boolean NOT NULL DEFAULT false, deleted_at timestamptz, deleted_by bigint REFERENCES app_users(user_id),
  version integer NOT NULL DEFAULT 1
);

CREATE TABLE IF NOT EXISTS incident_statuses (
  incident_status_id bigint GENERATED ALWAYS AS IDENTITY PRIMARY KEY,
  code varchar(40) NOT NULL UNIQUE, name varchar(120) NOT NULL, description text,
  created_at timestamptz NOT NULL DEFAULT now(), created_by bigint REFERENCES app_users(user_id),
  updated_at timestamptz NOT NULL DEFAULT now(), updated_by bigint REFERENCES app_users(user_id),
  is_deleted boolean NOT NULL DEFAULT false, deleted_at timestamptz, deleted_by bigint REFERENCES app_users(user_id),
  version integer NOT NULL DEFAULT 1
);

CREATE TABLE IF NOT EXISTS maintenance_types (
  maintenance_type_id bigint GENERATED ALWAYS AS IDENTITY PRIMARY KEY,
  code varchar(40) NOT NULL UNIQUE, name varchar(120) NOT NULL, description text,
  created_at timestamptz NOT NULL DEFAULT now(), created_by bigint REFERENCES app_users(user_id),
  updated_at timestamptz NOT NULL DEFAULT now(), updated_by bigint REFERENCES app_users(user_id),
  is_deleted boolean NOT NULL DEFAULT false, deleted_at timestamptz, deleted_by bigint REFERENCES app_users(user_id),
  version integer NOT NULL DEFAULT 1
);

CREATE TABLE IF NOT EXISTS relation_types (
  relation_type_id bigint GENERATED ALWAYS AS IDENTITY PRIMARY KEY,
  code varchar(40) NOT NULL UNIQUE, name varchar(120) NOT NULL, description text,
  created_at timestamptz NOT NULL DEFAULT now(), created_by bigint REFERENCES app_users(user_id),
  updated_at timestamptz NOT NULL DEFAULT now(), updated_by bigint REFERENCES app_users(user_id),
  is_deleted boolean NOT NULL DEFAULT false, deleted_at timestamptz, deleted_by bigint REFERENCES app_users(user_id),
  version integer NOT NULL DEFAULT 1
);

CREATE TABLE IF NOT EXISTS assignment_types (
  assignment_type_id bigint GENERATED ALWAYS AS IDENTITY PRIMARY KEY,
  code varchar(40) NOT NULL UNIQUE, name varchar(120) NOT NULL, description text,
  created_at timestamptz NOT NULL DEFAULT now(), created_by bigint REFERENCES app_users(user_id),
  updated_at timestamptz NOT NULL DEFAULT now(), updated_by bigint REFERENCES app_users(user_id),
  is_deleted boolean NOT NULL DEFAULT false, deleted_at timestamptz, deleted_by bigint REFERENCES app_users(user_id),
  version integer NOT NULL DEFAULT 1
);

CREATE TABLE IF NOT EXISTS movement_reasons (
  movement_reason_id bigint GENERATED ALWAYS AS IDENTITY PRIMARY KEY,
  code varchar(40) NOT NULL UNIQUE, name varchar(120) NOT NULL, description text,
  created_at timestamptz NOT NULL DEFAULT now(), created_by bigint REFERENCES app_users(user_id),
  updated_at timestamptz NOT NULL DEFAULT now(), updated_by bigint REFERENCES app_users(user_id),
  is_deleted boolean NOT NULL DEFAULT false, deleted_at timestamptz, deleted_by bigint REFERENCES app_users(user_id),
  version integer NOT NULL DEFAULT 1
);

CREATE TABLE IF NOT EXISTS disposal_reasons (
  disposal_reason_id bigint GENERATED ALWAYS AS IDENTITY PRIMARY KEY,
  code varchar(40) NOT NULL UNIQUE, name varchar(120) NOT NULL, description text,
  created_at timestamptz NOT NULL DEFAULT now(), created_by bigint REFERENCES app_users(user_id),
  updated_at timestamptz NOT NULL DEFAULT now(), updated_by bigint REFERENCES app_users(user_id),
  is_deleted boolean NOT NULL DEFAULT false, deleted_at timestamptz, deleted_by bigint REFERENCES app_users(user_id),
  version integer NOT NULL DEFAULT 1
);

CREATE TABLE IF NOT EXISTS software_types (
  software_type_id bigint GENERATED ALWAYS AS IDENTITY PRIMARY KEY,
  code varchar(40) NOT NULL UNIQUE, name varchar(120) NOT NULL, description text,
  created_at timestamptz NOT NULL DEFAULT now(), created_by bigint REFERENCES app_users(user_id),
  updated_at timestamptz NOT NULL DEFAULT now(), updated_by bigint REFERENCES app_users(user_id),
  is_deleted boolean NOT NULL DEFAULT false, deleted_at timestamptz, deleted_by bigint REFERENCES app_users(user_id),
  version integer NOT NULL DEFAULT 1
);

-- FIX 4: license_type deja de ser varchar libre; ahora es catálogo real.
CREATE TABLE IF NOT EXISTS license_types (
  license_type_id bigint GENERATED ALWAYS AS IDENTITY PRIMARY KEY,
  code varchar(40) NOT NULL UNIQUE, name varchar(120) NOT NULL, description text,
  created_at timestamptz NOT NULL DEFAULT now(), created_by bigint REFERENCES app_users(user_id),
  updated_at timestamptz NOT NULL DEFAULT now(), updated_by bigint REFERENCES app_users(user_id),
  is_deleted boolean NOT NULL DEFAULT false, deleted_at timestamptz, deleted_by bigint REFERENCES app_users(user_id),
  version integer NOT NULL DEFAULT 1
);

-- ============================================================
-- 3. DATOS MAESTROS: FABRICANTES, MODELOS, UBICACIONES
-- ============================================================

CREATE TABLE IF NOT EXISTS manufacturers (
  manufacturer_id bigint GENERATED ALWAYS AS IDENTITY PRIMARY KEY,
  name           varchar(150) NOT NULL UNIQUE,
  created_at     timestamptz NOT NULL DEFAULT now(), created_by bigint REFERENCES app_users(user_id),
  updated_at     timestamptz NOT NULL DEFAULT now(), updated_by bigint REFERENCES app_users(user_id),
  is_deleted     boolean NOT NULL DEFAULT false, deleted_at timestamptz, deleted_by bigint REFERENCES app_users(user_id),
  version        integer NOT NULL DEFAULT 1
);

CREATE TABLE IF NOT EXISTS models (
  model_id        bigint GENERATED ALWAYS AS IDENTITY PRIMARY KEY,
  manufacturer_id bigint REFERENCES manufacturers(manufacturer_id),
  asset_type_id   bigint REFERENCES asset_types(asset_type_id),
  model_name      varchar(150) NOT NULL,
  created_at      timestamptz NOT NULL DEFAULT now(), created_by bigint REFERENCES app_users(user_id),
  updated_at      timestamptz NOT NULL DEFAULT now(), updated_by bigint REFERENCES app_users(user_id),
  is_deleted      boolean NOT NULL DEFAULT false, deleted_at timestamptz, deleted_by bigint REFERENCES app_users(user_id),
  version         integer NOT NULL DEFAULT 1,
  UNIQUE (manufacturer_id, model_name)
);

-- Jerarquía: Edificio -> Planta -> Área/Lab/Oficina -> Rack/Sububicación
CREATE TABLE IF NOT EXISTS locations (
  location_id       bigint GENERATED ALWAYS AS IDENTITY PRIMARY KEY,
  code              varchar(40) NOT NULL UNIQUE,
  name              varchar(150) NOT NULL,
  location_type_id  bigint NOT NULL REFERENCES location_types(location_type_id),
  parent_location_id bigint REFERENCES locations(location_id),
  created_at        timestamptz NOT NULL DEFAULT now(), created_by bigint REFERENCES app_users(user_id),
  updated_at        timestamptz NOT NULL DEFAULT now(), updated_by bigint REFERENCES app_users(user_id),
  is_deleted        boolean NOT NULL DEFAULT false, deleted_at timestamptz, deleted_by bigint REFERENCES app_users(user_id),
  version           integer NOT NULL DEFAULT 1,
  CHECK (parent_location_id IS NULL OR parent_location_id <> location_id)
);

-- Configuración clave/valor específica de un edificio (pisos, capacidad, etc.)
CREATE TABLE IF NOT EXISTS building_settings (
  building_setting_id bigint GENERATED ALWAYS AS IDENTITY PRIMARY KEY,
  location_id          bigint NOT NULL REFERENCES locations(location_id),
  setting_key           varchar(100) NOT NULL,
  setting_value          text,
  created_at            timestamptz NOT NULL DEFAULT now(), created_by bigint REFERENCES app_users(user_id),
  updated_at            timestamptz NOT NULL DEFAULT now(), updated_by bigint REFERENCES app_users(user_id),
  is_deleted            boolean NOT NULL DEFAULT false, deleted_at timestamptz, deleted_by bigint REFERENCES app_users(user_id),
  version               integer NOT NULL DEFAULT 1,
  UNIQUE (location_id, setting_key)
);

-- ============================================================
-- 4. ACTIVOS (identidad administrativa central)
-- ============================================================

CREATE TABLE IF NOT EXISTS assets (
  asset_id            bigint GENERATED ALWAYS AS IDENTITY PRIMARY KEY,
  inventory_code      varchar(40) NOT NULL UNIQUE,
  asset_type_id       bigint NOT NULL REFERENCES asset_types(asset_type_id),
  asset_status_id     bigint NOT NULL REFERENCES asset_statuses(asset_status_id),
  manufacturer_id     bigint REFERENCES manufacturers(manufacturer_id),
  model_id            bigint REFERENCES models(model_id),
  current_location_id bigint REFERENCES locations(location_id),
  serial_number       varchar(150),
  hostname            varchar(150),
  notes               text,
  created_at          timestamptz NOT NULL DEFAULT now(),
  created_by          bigint REFERENCES app_users(user_id),
  updated_at          timestamptz NOT NULL DEFAULT now(),
  updated_by          bigint REFERENCES app_users(user_id),
  is_deleted          boolean NOT NULL DEFAULT false,
  deleted_at          timestamptz,
  deleted_by          bigint REFERENCES app_users(user_id),
  version             integer NOT NULL DEFAULT 1
);

COMMENT ON TABLE assets IS
'Identidad administrativa de cada equipo físico inventariable. No se elimina físicamente.';
COMMENT ON COLUMN assets.inventory_code IS
'Código de inventario estable e independiente de la ubicación (ej. TEC-PC-0001).';
COMMENT ON COLUMN assets.current_location_id IS
'Ubicación actual para consulta rápida; los cambios históricos están en asset_movements.';

CREATE TABLE IF NOT EXISTS asset_specifications (
  asset_specification_id bigint GENERATED ALWAYS AS IDENTITY PRIMARY KEY,
  asset_id              bigint NOT NULL REFERENCES assets(asset_id),
  specification_key     varchar(100) NOT NULL,
  specification_value   text,
  unit                  varchar(30),
  created_at             timestamptz NOT NULL DEFAULT now(), created_by bigint REFERENCES app_users(user_id),
  updated_at             timestamptz NOT NULL DEFAULT now(), updated_by bigint REFERENCES app_users(user_id),
  is_deleted             boolean NOT NULL DEFAULT false, deleted_at timestamptz, deleted_by bigint REFERENCES app_users(user_id),
  version                integer NOT NULL DEFAULT 1,
  UNIQUE(asset_id, specification_key)
);

-- ============================================================
-- 5. COMPONENTES, SISTEMA OPERATIVO, RED
-- ============================================================

CREATE TABLE IF NOT EXISTS asset_components (
  asset_component_id bigint GENERATED ALWAYS AS IDENTITY PRIMARY KEY,
  asset_id            bigint NOT NULL REFERENCES assets(asset_id),
  component_type_id   bigint NOT NULL REFERENCES component_types(component_type_id),
  description          varchar(250),
  serial_number        varchar(150),
  installed_at         timestamptz NOT NULL DEFAULT now(),
  removed_at           timestamptz,
  created_at           timestamptz NOT NULL DEFAULT now(), created_by bigint REFERENCES app_users(user_id),
  updated_at           timestamptz NOT NULL DEFAULT now(), updated_by bigint REFERENCES app_users(user_id),
  is_deleted           boolean NOT NULL DEFAULT false, deleted_at timestamptz, deleted_by bigint REFERENCES app_users(user_id),
  version              integer NOT NULL DEFAULT 1
);
COMMENT ON TABLE asset_components IS
'Componentes internos de un activo; conserva instalación y retiro para historial.';

CREATE TABLE IF NOT EXISTS asset_os_history (
  asset_os_history_id bigint GENERATED ALWAYS AS IDENTITY PRIMARY KEY,
  asset_id             bigint NOT NULL REFERENCES assets(asset_id),
  os_name              varchar(120) NOT NULL,
  os_version           varchar(60),
  installed_at         timestamptz NOT NULL DEFAULT now(),
  is_current           boolean NOT NULL DEFAULT true,
  created_at           timestamptz NOT NULL DEFAULT now(), created_by bigint REFERENCES app_users(user_id),
  updated_at           timestamptz NOT NULL DEFAULT now(), updated_by bigint REFERENCES app_users(user_id),
  is_deleted           boolean NOT NULL DEFAULT false, deleted_at timestamptz, deleted_by bigint REFERENCES app_users(user_id),
  version              integer NOT NULL DEFAULT 1
);
COMMENT ON TABLE asset_os_history IS
'Historial de sistemas operativos instalados por activo.';

CREATE TABLE IF NOT EXISTS software (
  software_id      bigint GENERATED ALWAYS AS IDENTITY PRIMARY KEY,
  software_type_id bigint REFERENCES software_types(software_type_id),
  name             varchar(150) NOT NULL,
  vendor           varchar(150),
  created_at       timestamptz NOT NULL DEFAULT now(), created_by bigint REFERENCES app_users(user_id),
  updated_at       timestamptz NOT NULL DEFAULT now(), updated_by bigint REFERENCES app_users(user_id),
  is_deleted       boolean NOT NULL DEFAULT false, deleted_at timestamptz, deleted_by bigint REFERENCES app_users(user_id),
  version          integer NOT NULL DEFAULT 1,
  UNIQUE (name, vendor)
);

CREATE TABLE IF NOT EXISTS asset_software (
  asset_software_id bigint GENERATED ALWAYS AS IDENTITY PRIMARY KEY,
  asset_id           bigint NOT NULL REFERENCES assets(asset_id),
  software_id        bigint NOT NULL REFERENCES software(software_id),
  version_installed  varchar(60),
  installed_at       timestamptz NOT NULL DEFAULT now(),
  is_current         boolean NOT NULL DEFAULT true,
  created_at         timestamptz NOT NULL DEFAULT now(), created_by bigint REFERENCES app_users(user_id),
  updated_at         timestamptz NOT NULL DEFAULT now(), updated_by bigint REFERENCES app_users(user_id),
  is_deleted         boolean NOT NULL DEFAULT false, deleted_at timestamptz, deleted_by bigint REFERENCES app_users(user_id),
  version            integer NOT NULL DEFAULT 1
);
COMMENT ON TABLE asset_software IS
'Historial de software instalado por activo.';

-- FIX 2: network_interfaces queda protegida por el trigger dinámico
-- de soft delete (sección 19) porque ya tiene is_deleted desde su creación.
CREATE TABLE IF NOT EXISTS network_interfaces (
  network_interface_id bigint GENERATED ALWAYS AS IDENTITY PRIMARY KEY,
  asset_id              bigint NOT NULL REFERENCES assets(asset_id),
  interface_name         varchar(60),
  mac_address            macaddr,
  ip_address             inet,
  created_at             timestamptz NOT NULL DEFAULT now(), created_by bigint REFERENCES app_users(user_id),
  updated_at             timestamptz NOT NULL DEFAULT now(), updated_by bigint REFERENCES app_users(user_id),
  is_deleted             boolean NOT NULL DEFAULT false, deleted_at timestamptz, deleted_by bigint REFERENCES app_users(user_id),
  version                integer NOT NULL DEFAULT 1
);

-- ============================================================
-- 6. SERVICIOS
-- ============================================================

CREATE TABLE IF NOT EXISTS services (
  service_id   bigint GENERATED ALWAYS AS IDENTITY PRIMARY KEY,
  name         varchar(150) NOT NULL UNIQUE,
  description  text,
  created_at   timestamptz NOT NULL DEFAULT now(), created_by bigint REFERENCES app_users(user_id),
  updated_at   timestamptz NOT NULL DEFAULT now(), updated_by bigint REFERENCES app_users(user_id),
  is_deleted   boolean NOT NULL DEFAULT false, deleted_at timestamptz, deleted_by bigint REFERENCES app_users(user_id),
  version      integer NOT NULL DEFAULT 1
);

CREATE TABLE IF NOT EXISTS asset_services (
  asset_service_id bigint GENERATED ALWAYS AS IDENTITY PRIMARY KEY,
  asset_id          bigint NOT NULL REFERENCES assets(asset_id),
  service_id        bigint NOT NULL REFERENCES services(service_id),
  created_at        timestamptz NOT NULL DEFAULT now(), created_by bigint REFERENCES app_users(user_id),
  updated_at        timestamptz NOT NULL DEFAULT now(), updated_by bigint REFERENCES app_users(user_id),
  is_deleted        boolean NOT NULL DEFAULT false, deleted_at timestamptz, deleted_by bigint REFERENCES app_users(user_id),
  version           integer NOT NULL DEFAULT 1,
  UNIQUE (asset_id, service_id)
);

-- ============================================================
-- 7. PROVEEDORES, ADQUISICIONES, GARANTÍAS, LICENCIAS
-- ============================================================

CREATE TABLE IF NOT EXISTS suppliers (
  supplier_id    bigint GENERATED ALWAYS AS IDENTITY PRIMARY KEY,
  name           varchar(180) NOT NULL UNIQUE,
  tax_id         varchar(30),
  contact_name   varchar(150),
  email          varchar(254),
  phone          varchar(50),
  address        text,
  notes          text,
  created_at     timestamptz NOT NULL DEFAULT now(), created_by bigint REFERENCES app_users(user_id),
  updated_at     timestamptz NOT NULL DEFAULT now(), updated_by bigint REFERENCES app_users(user_id),
  is_deleted     boolean NOT NULL DEFAULT false, deleted_at timestamptz, deleted_by bigint REFERENCES app_users(user_id),
  version        integer NOT NULL DEFAULT 1
);

CREATE TABLE IF NOT EXISTS acquisitions (
  acquisition_id  bigint GENERATED ALWAYS AS IDENTITY PRIMARY KEY,
  supplier_id     bigint REFERENCES suppliers(supplier_id),
  purchase_order  varchar(100),
  invoice_number  varchar(100),
  acquired_at     date,
  subtotal        numeric(14,2) CHECK(subtotal IS NULL OR subtotal >= 0),
  tax_amount      numeric(14,2) CHECK(tax_amount IS NULL OR tax_amount >= 0),
  total_amount    numeric(14,2) CHECK(total_amount IS NULL OR total_amount >= 0),
  currency_code   char(3) NOT NULL DEFAULT 'MXN',
  warranty_start  date,
  warranty_end    date,
  notes           text,
  created_at      timestamptz NOT NULL DEFAULT now(), created_by bigint REFERENCES app_users(user_id),
  updated_at      timestamptz NOT NULL DEFAULT now(), updated_by bigint REFERENCES app_users(user_id),
  is_deleted      boolean NOT NULL DEFAULT false, deleted_at timestamptz, deleted_by bigint REFERENCES app_users(user_id),
  version         integer NOT NULL DEFAULT 1,
  CHECK (warranty_end IS NULL OR warranty_start IS NULL OR warranty_end >= warranty_start)
);

CREATE TABLE IF NOT EXISTS acquisition_assets (
  acquisition_id bigint NOT NULL REFERENCES acquisitions(acquisition_id),
  asset_id       bigint NOT NULL REFERENCES assets(asset_id),
  unit_cost      numeric(14,2) CHECK(unit_cost IS NULL OR unit_cost >= 0),
  PRIMARY KEY(acquisition_id, asset_id)
);

CREATE TABLE IF NOT EXISTS asset_warranties (
  warranty_id      bigint GENERATED ALWAYS AS IDENTITY PRIMARY KEY,
  asset_id         bigint NOT NULL REFERENCES assets(asset_id),
  supplier_id      bigint REFERENCES suppliers(supplier_id),
  warranty_number  varchar(100),
  starts_on        date,
  ends_on          date,
  coverage         text,
  terms            text,
  created_at       timestamptz NOT NULL DEFAULT now(), created_by bigint REFERENCES app_users(user_id),
  updated_at       timestamptz NOT NULL DEFAULT now(), updated_by bigint REFERENCES app_users(user_id),
  is_deleted       boolean NOT NULL DEFAULT false, deleted_at timestamptz, deleted_by bigint REFERENCES app_users(user_id),
  version          integer NOT NULL DEFAULT 1,
  CHECK(ends_on IS NULL OR starts_on IS NULL OR ends_on >= starts_on)
);

-- FIX 4: license_type_id reemplaza al varchar libre original.
CREATE TABLE IF NOT EXISTS software_licenses (
  license_id          bigint GENERATED ALWAYS AS IDENTITY PRIMARY KEY,
  software_id         bigint NOT NULL REFERENCES software(software_id),
  license_type_id     bigint NOT NULL REFERENCES license_types(license_type_id),
  license_key         text,
  quantity_purchased  integer CHECK(quantity_purchased IS NULL OR quantity_purchased >= 0),
  valid_from          date,
  valid_until         date,
  supplier_id         bigint REFERENCES suppliers(supplier_id),
  notes               text,
  created_at          timestamptz NOT NULL DEFAULT now(), created_by bigint REFERENCES app_users(user_id),
  updated_at          timestamptz NOT NULL DEFAULT now(), updated_by bigint REFERENCES app_users(user_id),
  is_deleted          boolean NOT NULL DEFAULT false, deleted_at timestamptz, deleted_by bigint REFERENCES app_users(user_id),
  version             integer NOT NULL DEFAULT 1
);

-- FIX 3: se agregan updated_at / updated_by / version para saber
-- quién y cuándo cerró o modificó una asignación de licencia.
CREATE TABLE IF NOT EXISTS software_license_assignments (
  license_assignment_id bigint GENERATED ALWAYS AS IDENTITY PRIMARY KEY,
  license_id             bigint NOT NULL REFERENCES software_licenses(license_id),
  asset_id               bigint NOT NULL REFERENCES assets(asset_id),
  assigned_at             timestamptz NOT NULL DEFAULT now(),
  ended_at                timestamptz,
  created_by              bigint REFERENCES app_users(user_id),
  updated_at              timestamptz NOT NULL DEFAULT now(),
  updated_by              bigint REFERENCES app_users(user_id),
  version                 integer NOT NULL DEFAULT 1,
  CHECK(ended_at IS NULL OR ended_at >= assigned_at)
);

-- ============================================================
-- 8. INCIDENTES Y MANTENIMIENTO
-- ============================================================

CREATE TABLE IF NOT EXISTS incidents (
  incident_id           bigint GENERATED ALWAYS AS IDENTITY PRIMARY KEY,
  asset_id               bigint NOT NULL REFERENCES assets(asset_id),
  incident_type_id       bigint NOT NULL REFERENCES incident_types(incident_type_id),
  incident_priority_id   bigint NOT NULL REFERENCES incident_priorities(incident_priority_id),
  incident_status_id     bigint NOT NULL REFERENCES incident_statuses(incident_status_id),
  reported_by             bigint NOT NULL REFERENCES app_users(user_id),
  assigned_to             bigint REFERENCES app_users(user_id),
  title                   varchar(200) NOT NULL,
  description             text,
  diagnosis               text,
  solution                text,
  reported_at             timestamptz NOT NULL DEFAULT now(),
  resolved_at             timestamptz,
  created_at              timestamptz NOT NULL DEFAULT now(),
  created_by              bigint REFERENCES app_users(user_id),
  updated_by              bigint REFERENCES app_users(user_id),
  is_deleted              boolean NOT NULL DEFAULT false,
  deleted_at              timestamptz,
  deleted_by              bigint REFERENCES app_users(user_id),
  version                 integer NOT NULL DEFAULT 1,
  updated_at              timestamptz NOT NULL DEFAULT now()
);

CREATE TABLE IF NOT EXISTS maintenance_records (
  maintenance_record_id  bigint GENERATED ALWAYS AS IDENTITY PRIMARY KEY,
  asset_id                bigint NOT NULL REFERENCES assets(asset_id),
  maintenance_type_id     bigint NOT NULL REFERENCES maintenance_types(maintenance_type_id),
  performed_by             bigint REFERENCES app_users(user_id),
  scheduled_at             timestamptz,
  performed_at             timestamptz,
  next_maintenance_at      timestamptz,
  description              text,
  parts_used               text,
  created_at               timestamptz NOT NULL DEFAULT now(),
  created_by               bigint REFERENCES app_users(user_id),
  updated_at               timestamptz NOT NULL DEFAULT now(),
  updated_by               bigint REFERENCES app_users(user_id),
  is_deleted               boolean NOT NULL DEFAULT false,
  deleted_at               timestamptz,
  deleted_by               bigint REFERENCES app_users(user_id),
  version                  integer NOT NULL DEFAULT 1
);

-- ============================================================
-- 9. RELACIONES / ASIGNACIONES / MOVIMIENTOS (históricas)
-- No llevan soft delete: no se "corrige" borrando el pasado.
-- ============================================================

CREATE TABLE IF NOT EXISTS asset_relations (
  asset_relation_id  bigint GENERATED ALWAYS AS IDENTITY PRIMARY KEY,
  primary_asset_id    bigint NOT NULL REFERENCES assets(asset_id),
  related_asset_id     bigint NOT NULL REFERENCES assets(asset_id),
  relation_type_id      bigint NOT NULL REFERENCES relation_types(relation_type_id),
  created_at             timestamptz NOT NULL DEFAULT now(),
  created_by             bigint REFERENCES app_users(user_id),
  updated_at             timestamptz NOT NULL DEFAULT now(),
  updated_by             bigint REFERENCES app_users(user_id),
  version                integer NOT NULL DEFAULT 1,
  CHECK (primary_asset_id <> related_asset_id)
);

CREATE TABLE IF NOT EXISTS asset_assignments (
  asset_assignment_id  bigint GENERATED ALWAYS AS IDENTITY PRIMARY KEY,
  asset_id              bigint NOT NULL REFERENCES assets(asset_id),
  assigned_to            bigint NOT NULL REFERENCES app_users(user_id),
  assignment_type_id     bigint NOT NULL REFERENCES assignment_types(assignment_type_id),
  location_id            bigint REFERENCES locations(location_id),
  assigned_at             timestamptz NOT NULL DEFAULT now(),
  returned_at             timestamptz,
  notes                   text,
  created_at              timestamptz NOT NULL DEFAULT now(),
  created_by              bigint REFERENCES app_users(user_id),
  updated_at              timestamptz NOT NULL DEFAULT now(),
  updated_by              bigint REFERENCES app_users(user_id),
  version                 integer NOT NULL DEFAULT 1
);

CREATE TABLE IF NOT EXISTS asset_movements (
  asset_movement_id  bigint GENERATED ALWAYS AS IDENTITY PRIMARY KEY,
  asset_id            bigint NOT NULL REFERENCES assets(asset_id),
  from_location_id     bigint REFERENCES locations(location_id),
  to_location_id        bigint NOT NULL REFERENCES locations(location_id),
  movement_reason_id     bigint NOT NULL REFERENCES movement_reasons(movement_reason_id),
  moved_by                bigint NOT NULL REFERENCES app_users(user_id),
  moved_at                 timestamptz NOT NULL DEFAULT now(),
  notes                    text,
  created_at               timestamptz NOT NULL DEFAULT now(),
  created_by               bigint REFERENCES app_users(user_id),
  updated_at               timestamptz NOT NULL DEFAULT now(),
  updated_by               bigint REFERENCES app_users(user_id),
  version                  integer NOT NULL DEFAULT 1
);
COMMENT ON TABLE asset_movements IS
'Historial de movimientos. No debe borrarse para corregir ubicación.';

-- ============================================================
-- 10. BAJA FORMAL DE ACTIVOS
-- ============================================================

CREATE TABLE IF NOT EXISTS asset_disposals (
  asset_disposal_id   bigint GENERATED ALWAYS AS IDENTITY PRIMARY KEY,
  asset_id              bigint NOT NULL UNIQUE REFERENCES assets(asset_id),
  disposal_reason_id     bigint NOT NULL REFERENCES disposal_reasons(disposal_reason_id),
  folio                   varchar(100) NOT NULL UNIQUE,
  dictamen                 text,
  act_document_path         text,
  disposed_at                date NOT NULL DEFAULT current_date,
  created_at                 timestamptz NOT NULL DEFAULT now(),
  created_by                 bigint REFERENCES app_users(user_id),
  updated_at                 timestamptz NOT NULL DEFAULT now(),
  updated_by                 bigint REFERENCES app_users(user_id),
  version                    integer NOT NULL DEFAULT 1
);
COMMENT ON TABLE asset_disposals IS
'Baja administrativa; el asset original permanece en assets.';

-- ============================================================
-- 11. INVENTARIO FÍSICO
-- ============================================================

CREATE TABLE IF NOT EXISTS inventory_sessions (
  inventory_session_id  bigint GENERATED ALWAYS AS IDENTITY PRIMARY KEY,
  name                    varchar(150) NOT NULL,
  started_at               timestamptz NOT NULL DEFAULT now(),
  ended_at                 timestamptz,
  status                   varchar(20) NOT NULL DEFAULT 'abierta'
                             CHECK (status IN ('abierta','cerrada','cancelada')),
  created_at               timestamptz NOT NULL DEFAULT now(),
  created_by               bigint REFERENCES app_users(user_id),
  updated_at               timestamptz NOT NULL DEFAULT now(),
  updated_by               bigint REFERENCES app_users(user_id),
  version                  integer NOT NULL DEFAULT 1
);

CREATE TABLE IF NOT EXISTS inventory_records (
  inventory_record_id  bigint GENERATED ALWAYS AS IDENTITY PRIMARY KEY,
  inventory_session_id   bigint NOT NULL REFERENCES inventory_sessions(inventory_session_id),
  asset_id                 bigint NOT NULL REFERENCES assets(asset_id),
  found                     boolean NOT NULL DEFAULT false,
  location_found_id          bigint REFERENCES locations(location_id),
  notes                      text,
  created_at                 timestamptz NOT NULL DEFAULT now(),
  created_by                 bigint REFERENCES app_users(user_id),
  updated_at                 timestamptz NOT NULL DEFAULT now(),
  updated_by                 bigint REFERENCES app_users(user_id),
  version                    integer NOT NULL DEFAULT 1,
  UNIQUE (inventory_session_id, asset_id)
);
COMMENT ON TABLE inventory_records IS
'Resultado de una verificación física contra la información de la BD.';

-- ============================================================
-- 12. AUDITORÍA CENTRAL (inmutable)
-- ============================================================

CREATE TABLE IF NOT EXISTS audit_log (
  audit_log_id  bigint GENERATED ALWAYS AS IDENTITY PRIMARY KEY,
  table_name     varchar(120) NOT NULL,
  record_id       bigint NOT NULL,
  action           varchar(20) NOT NULL CHECK (action IN ('INSERT','UPDATE','DELETE')),
  old_data          jsonb,
  new_data          jsonb,
  performed_by       bigint REFERENCES app_users(user_id),
  performed_at        timestamptz NOT NULL DEFAULT now(),
  client_ip            inet
);
COMMENT ON TABLE audit_log IS
'Registro inmutable de auditoría. No debe modificarse ni eliminarse desde la aplicación.';

-- ============================================================
-- 13. UNICIDAD E ÍNDICES
-- ============================================================

CREATE UNIQUE INDEX IF NOT EXISTS ux_assets_serial_nonempty
ON assets (serial_number)
WHERE serial_number IS NOT NULL AND btrim(serial_number) <> '';

CREATE UNIQUE INDEX IF NOT EXISTS ux_current_asset_os_v2
ON asset_os_history(asset_id)
WHERE is_current AND NOT is_deleted;

CREATE UNIQUE INDEX IF NOT EXISTS ux_current_asset_software_v2
ON asset_software(asset_id, software_id)
WHERE is_current AND NOT is_deleted;

CREATE INDEX IF NOT EXISTS ix_assets_active_location ON assets(current_location_id) WHERE is_deleted = false;
CREATE INDEX IF NOT EXISTS ix_assets_active_type     ON assets(asset_type_id)       WHERE is_deleted = false;
CREATE INDEX IF NOT EXISTS ix_locations_active_parent ON locations(parent_location_id) WHERE is_deleted = false;
CREATE INDEX IF NOT EXISTS ix_incidents_active_asset  ON incidents(asset_id)         WHERE is_deleted = false;
CREATE INDEX IF NOT EXISTS ix_maintenance_asset        ON maintenance_records(asset_id);
CREATE INDEX IF NOT EXISTS ix_movements_asset           ON asset_movements(asset_id);
CREATE INDEX IF NOT EXISTS ix_assignments_asset          ON asset_assignments(asset_id);
CREATE INDEX IF NOT EXISTS ix_components_asset            ON asset_components(asset_id);
CREATE INDEX IF NOT EXISTS ix_network_interfaces_asset      ON network_interfaces(asset_id);
CREATE INDEX IF NOT EXISTS ix_audit_log_table_record          ON audit_log(table_name, record_id);

-- ============================================================
-- 14. FIX 1: version -> trigger dinámico
-- Detecta TODAS las tablas con columna "version" y les aplica
-- touch_version. Ya no depende de una lista escrita a mano.
-- ============================================================
DO $$
DECLARE
  t text;
BEGIN
  FOR t IN
    SELECT c.table_name
    FROM information_schema.columns c
    WHERE c.table_schema = 'inventario' AND c.column_name = 'version'
  LOOP
    EXECUTE format('DROP TRIGGER IF EXISTS trg_%I_version ON %I', t, t);
    EXECUTE format(
      'CREATE TRIGGER trg_%I_version BEFORE UPDATE ON %I
       FOR EACH ROW EXECUTE FUNCTION touch_version()', t, t);
  END LOOP;
END $$;

-- ============================================================
-- 15. FIX 2: soft delete -> trigger dinámico
-- Detecta TODAS las tablas con columna "is_deleted" (incluye
-- network_interfaces automáticamente) y les aplica el trigger.
-- ============================================================
DO $$
DECLARE
  t text;
BEGIN
  FOR t IN
    SELECT c.table_name
    FROM information_schema.columns c
    WHERE c.table_schema = 'inventario' AND c.column_name = 'is_deleted'
  LOOP
    EXECUTE format('DROP TRIGGER IF EXISTS trg_%I_soft_delete ON %I', t, t);
    EXECUTE format(
      'CREATE TRIGGER trg_%I_soft_delete BEFORE UPDATE ON %I
       FOR EACH ROW EXECUTE FUNCTION prevent_invalid_soft_delete()', t, t);
  END LOOP;
END $$;

-- ============================================================
-- 16. VISTAS OPERATIVAS
-- ============================================================

CREATE OR REPLACE VIEW v_asset_summary AS
SELECT
  a.asset_id, a.inventory_code,
  at.code AS asset_type, at.name AS asset_type_name,
  a.serial_number,
  mfr.name AS manufacturer, mdl.model_name,
  ast.code AS status, ast.name AS status_name,
  l.code AS location_code, l.name AS location_name,
  a.hostname, a.created_at, a.updated_at, a.version
FROM assets a
JOIN asset_types at ON at.asset_type_id = a.asset_type_id
JOIN asset_statuses ast ON ast.asset_status_id = a.asset_status_id
LEFT JOIN manufacturers mfr ON mfr.manufacturer_id = a.manufacturer_id
LEFT JOIN models mdl ON mdl.model_id = a.model_id
LEFT JOIN locations l ON l.location_id = a.current_location_id
WHERE a.is_deleted = false;

CREATE OR REPLACE VIEW v_location_equipment_summary AS
SELECT
  l.location_id, l.code AS location_code, l.name AS location_name,
  COUNT(a.asset_id) AS total_assets
FROM locations l
LEFT JOIN assets a ON a.current_location_id = l.location_id AND a.is_deleted = false
WHERE l.is_deleted = false
GROUP BY l.location_id, l.code, l.name;

-- ============================================================
-- 17. SEMILLA MÍNIMA PARA ARRANCAR EL SISTEMA
-- ============================================================

INSERT INTO roles (code, name, description) VALUES
  ('ADMIN', 'Administrador', 'Control total del sistema'),
  ('TECNICO', 'Técnico', 'Atiende incidentes y mantenimientos'),
  ('RESPONSABLE_AREA', 'Responsable de Área', 'Resguardante de equipo asignado'),
  ('USUARIO_GENERAL', 'Usuario General', 'Consulta y reporte de fallas')
ON CONFLICT (code) DO NOTHING;

-- Máquina de estados de activos (sección 4 de la especificación)
INSERT INTO asset_statuses (code, name) VALUES
  ('DISPONIBLE', 'Disponible'),
  ('ASIGNADO', 'Asignado'),
  ('EN_MANTENIMIENTO', 'En Mantenimiento'),
  ('CON_FALLA', 'Con Falla / En Diagnóstico'),
  ('DADO_DE_BAJA', 'Dado de Baja')
ON CONFLICT (code) DO NOTHING;

INSERT INTO disposal_reasons (code, name) VALUES
  ('OBSOLESCENCIA', 'Obsolescencia'),
  ('DANIO_IRREPARABLE', 'Daño irreparable'),
  ('ROBO', 'Robo'),
  ('EXTRAVIO', 'Extravío')
ON CONFLICT (code) DO NOTHING;

-- Usuario técnico del sistema, para las FK created_by/updated_by iniciales.
INSERT INTO app_users (username, email, password_hash, first_name, last_name, role_id, status)
SELECT 'system', 'system@localhost', 'SYSTEM_ACCOUNT_NO_LOGIN', 'Sistema', 'Automático',
       r.role_id, 'activo'
FROM roles r WHERE r.code = 'ADMIN'
ON CONFLICT (username) DO NOTHING;

-- ============================================================
-- 18. SESIONES Y LOGIN FEDERADO (scripts/add-auth-sessions.sql)
-- ============================================================

-- Refresh tokens: solo se guarda el SHA-256 del token (nunca el valor).
-- family_id agrupa una cadena de rotaciones; si se reutiliza un token ya
-- rotado se revoca toda la familia (detección de robo).
CREATE TABLE IF NOT EXISTS refresh_tokens (
  refresh_token_id  bigint GENERATED ALWAYS AS IDENTITY PRIMARY KEY,
  user_id           bigint NOT NULL REFERENCES app_users(user_id),
  token_hash        char(64) NOT NULL UNIQUE,
  family_id         uuid NOT NULL,
  expires_at        timestamptz NOT NULL,
  created_at        timestamptz NOT NULL DEFAULT now(),
  revoked_at        timestamptz,
  replaced_by_id    bigint REFERENCES refresh_tokens(refresh_token_id),
  created_ip        inet,
  user_agent        varchar(512)
);
CREATE INDEX IF NOT EXISTS ix_refresh_tokens_family ON refresh_tokens(family_id);
CREATE INDEX IF NOT EXISTS ix_refresh_tokens_user ON refresh_tokens(user_id);

-- Identidad externa vinculada a un usuario existente (sin auto-alta).
-- subject = claim "oid" (Entra) o "sub" (Google).
CREATE TABLE IF NOT EXISTS user_external_logins (
  user_external_login_id  bigint GENERATED ALWAYS AS IDENTITY PRIMARY KEY,
  user_id                 bigint NOT NULL REFERENCES app_users(user_id),
  provider                varchar(20) NOT NULL CHECK (provider IN ('microsoft','google')),
  subject                 varchar(255) NOT NULL,
  email                   varchar(254) NOT NULL,
  linked_at               timestamptz NOT NULL DEFAULT now(),
  CONSTRAINT uq_user_external_logins_subject UNIQUE (provider, subject),
  CONSTRAINT uq_user_external_logins_user UNIQUE (user_id, provider)
);

-- ============================================================
-- FIN DEL ESQUEMA
-- ============================================================
