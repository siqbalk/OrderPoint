DO $EF$
BEGIN
    IF NOT EXISTS(SELECT 1 FROM pg_namespace WHERE nspname = 'catalog') THEN
        CREATE SCHEMA catalog;
    END IF;
END $EF$;
CREATE TABLE IF NOT EXISTS catalog.__ef_migrations_history (
    "MigrationId" character varying(150) NOT NULL,
    "ProductVersion" character varying(32) NOT NULL,
    CONSTRAINT "PK___ef_migrations_history" PRIMARY KEY ("MigrationId")
);

START TRANSACTION;

DO $EF$
BEGIN
    IF NOT EXISTS(SELECT 1 FROM catalog.__ef_migrations_history WHERE "MigrationId" = '20260929124812_InitialCreate') THEN
        IF NOT EXISTS(SELECT 1 FROM pg_namespace WHERE nspname = 'catalog') THEN
            CREATE SCHEMA catalog;
        END IF;
    END IF;
END $EF$;

DO $EF$
BEGIN
    IF NOT EXISTS(SELECT 1 FROM catalog.__ef_migrations_history WHERE "MigrationId" = '20260929124812_InitialCreate') THEN
    CREATE TABLE catalog.outbox_messages (
        "Id" uuid NOT NULL,
        "Type" character varying(300) NOT NULL,
        "Content" jsonb NOT NULL,
        "OccurredOnUtc" timestamp with time zone NOT NULL,
        "ProcessedOnUtc" timestamp with time zone,
        "Attempts" integer NOT NULL DEFAULT 0,
        "Error" character varying(2000),
        CONSTRAINT "PK_outbox_messages" PRIMARY KEY ("Id")
    );
    END IF;
END $EF$;

DO $EF$
BEGIN
    IF NOT EXISTS(SELECT 1 FROM catalog.__ef_migrations_history WHERE "MigrationId" = '20260929124812_InitialCreate') THEN
    CREATE TABLE catalog.products (
        "Id" uuid NOT NULL,
        "Sku" character varying(50) NOT NULL,
        "Name" character varying(200) NOT NULL,
        "Description" character varying(2000),
        "Price" numeric(12,2) NOT NULL,
        "IsActive" boolean NOT NULL,
        "CreatedOnUtc" timestamp with time zone NOT NULL,
        "UpdatedOnUtc" timestamp with time zone,
        "TenantId" uuid NOT NULL,
        CONSTRAINT "PK_products" PRIMARY KEY ("Id")
    );
    END IF;
END $EF$;

DO $EF$
BEGIN
    IF NOT EXISTS(SELECT 1 FROM catalog.__ef_migrations_history WHERE "MigrationId" = '20260929124812_InitialCreate') THEN
    CREATE INDEX ix_outbox_messages_unprocessed ON catalog.outbox_messages ("OccurredOnUtc") WHERE "ProcessedOnUtc" IS NULL;
    END IF;
END $EF$;

DO $EF$
BEGIN
    IF NOT EXISTS(SELECT 1 FROM catalog.__ef_migrations_history WHERE "MigrationId" = '20260929124812_InitialCreate') THEN
    CREATE UNIQUE INDEX "IX_products_TenantId_Sku" ON catalog.products ("TenantId", "Sku");
    END IF;
END $EF$;

DO $EF$
BEGIN
    IF NOT EXISTS(SELECT 1 FROM catalog.__ef_migrations_history WHERE "MigrationId" = '20260929124812_InitialCreate') THEN
    INSERT INTO catalog.__ef_migrations_history ("MigrationId", "ProductVersion")
    VALUES ('20260929124812_InitialCreate', '10.0.4');
    END IF;
END $EF$;
COMMIT;

