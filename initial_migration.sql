
-- Initial database schema for MoneyTransfer demo

CREATE TABLE Accounts (
    Id SERIAL PRIMARY KEY,
    AccountNumber VARCHAR(64) NOT NULL,
    Owner VARCHAR(128) NOT NULL,
    Balance NUMERIC(18,2) NOT NULL DEFAULT 0,
    CreatedAt TIMESTAMP WITHOUT TIME ZONE NOT NULL DEFAULT NOW()
);

CREATE TABLE IdempotencyKeys_Account (
    Id SERIAL PRIMARY KEY,
    Key VARCHAR(200) NOT NULL UNIQUE,
    RequestHash TEXT,
    CreatedAt TIMESTAMP WITHOUT TIME ZONE NOT NULL DEFAULT NOW()
);

CREATE TABLE transfers (
    id              SERIAL PRIMARY KEY,
    from_account_id INT NOT NULL,
    to_account_id   INT NOT NULL,
    amount          NUMERIC(18,2) NOT NULL,
    status          VARCHAR(50) NOT NULL,
    created_at      TIMESTAMP NOT NULL DEFAULT NOW(),
    idempotency_key VARCHAR(100)
);

CREATE UNIQUE INDEX ux_transfers_idempotency_key
ON transfers(idempotency_key);