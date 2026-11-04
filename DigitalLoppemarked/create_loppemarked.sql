-- =============================================================
-- create_loppemarked.sql
--
-- DEL 1: Kør i konsollen på databasen "postgres" (som postgres).
--        Kør kun én gang. Hvis CREATE DATABASE giver
--        "already exists", er databasen oprettet, og linjen kan springes over.
--
-- DEL 2: Kør i konsollen på "digital_loppemarked" (som postgres).
--        Kan køres igen: tabellerne slettes og genopbygges.
-- =============================================================


-- #############################################################
-- DEL 1: roller og database (kør i konsollen på postgres)
-- #############################################################

DROP TABLE IF EXISTS order_lines, orders, products CASCADE;
DROP TYPE  IF EXISTS order_status;

DO $$
BEGIN
    IF NOT EXISTS (SELECT 1 FROM pg_roles WHERE rolname = 'loppe_admin') THEN
        CREATE ROLE loppe_admin LOGIN NOSUPERUSER NOCREATEDB NOCREATEROLE;
    END IF;
    IF NOT EXISTS (SELECT 1 FROM pg_roles WHERE rolname = 'loppe_app') THEN
        CREATE ROLE loppe_app LOGIN NOSUPERUSER NOCREATEDB NOCREATEROLE NOINHERIT;
    END IF;
END
$$;

ALTER ROLE loppe_admin PASSWORD 'DIN_ADMIN_PW';
ALTER ROLE loppe_app   PASSWORD 'DIN_APP_PW';

-- Kør denne linje separat, og kun hvis databasen ikke findes endnu
CREATE DATABASE digital_loppemarked OWNER loppe_admin;

REVOKE ALL ON DATABASE digital_loppemarked FROM PUBLIC;
GRANT CONNECT ON DATABASE digital_loppemarked TO loppe_app;


-- #############################################################
-- DEL 2: schema, seed-data og rettigheder
--        (kør i konsollen på digital_loppemarked)
-- #############################################################

BEGIN;

SET ROLE loppe_admin;

REVOKE CREATE ON SCHEMA public FROM PUBLIC;

-- Ryd op (børn først)
DROP TABLE IF EXISTS order_lines CASCADE;
DROP TABLE IF EXISTS orders CASCADE;
DROP TABLE IF EXISTS products CASCADE;
DROP TABLE IF EXISTS user_verification_tokens CASCADE;
DROP TABLE IF EXISTS password_history CASCADE;
DROP TABLE IF EXISTS users CASCADE;
DROP TYPE  IF EXISTS order_status;

-- Typer og tabeller
CREATE TYPE order_status AS ENUM ('Pending', 'Paid', 'Shipped', 'Cancelled');

CREATE TABLE users (
    id               INT GENERATED ALWAYS AS IDENTITY PRIMARY KEY,
    email            VARCHAR(320)   NOT NULL UNIQUE,
    password_hash    TEXT           NOT NULL,
    full_name        VARCHAR(200)   NOT NULL DEFAULT '',
    is_active       BOOLEAN        NOT NULL DEFAULT TRUE,
    created_at       TIMESTAMPTZ    NOT NULL DEFAULT NOW(),
    gdpr_consent     BOOLEAN        NOT NULL DEFAULT FALSE,
    security_stamp    UUID           NOT NULL DEFAULT gen_random_uuid()
);

CREATE TABLE user_verification_tokens (
    user_id       INT           NOT NULL REFERENCES users(id) ON DELETE CASCADE,
    token         TEXT           NOT NULL,
    expires_at    TIMESTAMPTZ   NOT NULL,
    PRIMARY KEY (user_id)
);

CREATE TABLE password_history (
    user_id       INT           NOT NULL REFERENCES users(id) ON DELETE CASCADE,
    password_hash TEXT           NOT NULL,
    changed_at    TIMESTAMPTZ   NOT NULL DEFAULT NOW(),
    PRIMARY KEY (user_id, password_hash, changed_at)
);

CREATE TABLE products (
    id              INT GENERATED ALWAYS AS IDENTITY PRIMARY KEY,
    name            VARCHAR(200)   NOT NULL,
    description     TEXT           NOT NULL DEFAULT '',
    price           NUMERIC(10,2)  NOT NULL CHECK (price >= 0),
    image_url       VARCHAR(500)   NOT NULL DEFAULT '',
    stock_quantity  INT            NOT NULL DEFAULT 0 CHECK (stock_quantity >= 0),
    is_active       BOOLEAN        NOT NULL DEFAULT TRUE
);

CREATE TABLE orders (
    id               INT GENERATED ALWAYS AS IDENTITY PRIMARY KEY,
    customer_name    VARCHAR(200)  NOT NULL DEFAULT '',
    email            VARCHAR(320)  NOT NULL DEFAULT '',
    shipping_address VARCHAR(300)  NOT NULL DEFAULT '',
    city             VARCHAR(100)  NOT NULL DEFAULT '',
    postal_code      VARCHAR(20)   NOT NULL DEFAULT '',
    country          VARCHAR(100)  NOT NULL DEFAULT '',
    order_date       TIMESTAMPTZ   NOT NULL DEFAULT NOW(),
    status           order_status  NOT NULL DEFAULT 'Pending'
);

CREATE TABLE order_lines (
    id            INT GENERATED ALWAYS AS IDENTITY PRIMARY KEY,
    order_id      INT           NOT NULL REFERENCES orders(id) ON DELETE CASCADE,
    product_id    INT           NOT NULL REFERENCES products(id) ON DELETE RESTRICT,
    product_name  VARCHAR(200)  NOT NULL,
    unit_price    NUMERIC(10,2) NOT NULL CHECK (unit_price >= 0),
    quantity      INT           NOT NULL CHECK (quantity > 0)
);

CREATE INDEX ix_order_lines_order_id   ON order_lines(order_id);
CREATE INDEX ix_order_lines_product_id ON order_lines(product_id);

-- Seed-data
INSERT INTO products (name, description, price, image_url, stock_quantity, is_active) VALUES
    ('Wireless Mouse',           'Ergonomic wireless mouse',          24.99, 'images/mouse.svg',       50, TRUE),
    ('Mechanical Keyboard',      'RGB mechanical keyboard',           79.99, 'images/keyboard.svg',    30, TRUE),
    ('USB-C Hub',                '7-in-1 USB-C hub',                  34.99, 'images/hub.svg',        100, TRUE),
    ('Webcam 1080p',             'Full HD webcam with microphone',    44.99, 'images/webcam.svg',      20, TRUE),
    ('Noise-Cancelling Headset', 'Over-ear headset with ANC',        119.99, 'images/headset.svg',     15, TRUE),
    ('Laptop Stand',             'Adjustable aluminium laptop stand', 29.99, 'images/stand.svg',        0, TRUE),
    ('27" Monitor',              'QHD 144Hz IPS monitor',           249.99, 'images/monitor.svg',       8, TRUE),
    ('Desk Mat',                 'Large felt desk mat',              19.99, 'images/deskmat.svg',      60, TRUE),
    ('Discontinued Gadget',      'No longer for sale',                9.99, 'images/placeholder.svg',   5, FALSE);

-- Rettigheder til loppe_app
GRANT USAGE ON SCHEMA public TO loppe_app;

-- Læsning
GRANT SELECT ON products, orders, order_lines, users, user_verification_tokens, password_history TO loppe_app;

-- Ordrer oprettes, men ændres eller slettes ikke
GRANT INSERT ON orders, order_lines TO loppe_app;

-- Lager opdateres KUN i stock_quantity
GRANT UPDATE (stock_quantity) ON products TO loppe_app;

-- User management rettigheder
GRANT INSERT, UPDATE, DELETE ON users, user_verification_tokens, password_history TO loppe_app;

-- Identity-kolonner kræver USAGE på sekvenserne
GRANT USAGE, SELECT ON SEQUENCE products_id_seq, orders_id_seq, order_lines_id_seq, users_id_seq TO loppe_app;

RESET ROLE;

COMMIT;

-- Tjek
SELECT current_database() AS database, current_user AS kørt_som;
SELECT tablename, tableowner FROM pg_tables WHERE schemaname = 'public';
SELECT sequencename, sequenceowner FROM pg_sequences WHERE schemaname = 'public';
