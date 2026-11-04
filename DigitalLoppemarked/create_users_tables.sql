-- =============================================================
-- setup_users.sql
-- Kør i konsollen på "digital_loppemarked" som postgres
-- =============================================================

BEGIN;

-- Bruger tabel
CREATE TABLE users (
    id              INT GENERATED ALWAYS AS IDENTITY PRIMARY KEY,
    email           VARCHAR(320)   NOT NULL UNIQUE,
    password_hash   TEXT           NOT NULL,
    full_name       VARCHAR(200)   NOT NULL,
    is_active       BOOLEAN        NOT NULL DEFAULT FALSE,
    created_at      TIMESTAMPTZ    NOT NULL DEFAULT NOW(),
    gdpr_consent    BOOLEAN        NOT NULL DEFAULT FALSE
);

-- Verifikationstokens til e-mail aktivering
CREATE TABLE user_verification_tokens (
    user_id         INT            NOT NULL REFERENCES users(id) ON DELETE CASCADE,
    token           VARCHAR(100)   NOT NULL UNIQUE,
    expires_at      TIMESTAMPTZ    NOT NULL,
    PRIMARY KEY (user_id)
);

-- Rettigheder til loppe_app
GRANT USAGE ON SCHEMA public TO loppe_app;
GRANT SELECT, INSERT, UPDATE ON users TO loppe_app;
GRANT SELECT, INSERT, DELETE ON user_verification_tokens TO loppe_app;
GRANT USAGE, SELECT ON SEQUENCE users_id_seq TO loppe_app;

COMMIT;