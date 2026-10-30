-- Sesiones con cookies HttpOnly (refresh tokens rotativos) y vínculos de login
-- federado (Microsoft Entra / Google). Idempotente.
-- Uso: psql ... -v ON_ERROR_STOP=1 -f scripts/add-auth-sessions.sql
SET search_path TO inventario, public;

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
