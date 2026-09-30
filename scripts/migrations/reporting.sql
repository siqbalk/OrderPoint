DO $EF$
BEGIN
    IF NOT EXISTS(SELECT 1 FROM pg_namespace WHERE nspname = 'reporting') THEN
        CREATE SCHEMA reporting;
    END IF;
END $EF$;
CREATE TABLE IF NOT EXISTS reporting.__ef_migrations_history (
    "MigrationId" character varying(150) NOT NULL,
    "ProductVersion" character varying(32) NOT NULL,
    CONSTRAINT "PK___ef_migrations_history" PRIMARY KEY ("MigrationId")
);

START TRANSACTION;

DO $EF$
BEGIN
    IF NOT EXISTS(SELECT 1 FROM reporting.__ef_migrations_history WHERE "MigrationId" = '20260929124847_InitialCreate') THEN
        IF NOT EXISTS(SELECT 1 FROM pg_namespace WHERE nspname = 'reporting') THEN
            CREATE SCHEMA reporting;
        END IF;
    END IF;
END $EF$;

DO $EF$
BEGIN
    IF NOT EXISTS(SELECT 1 FROM reporting.__ef_migrations_history WHERE "MigrationId" = '20260929124847_InitialCreate') THEN
    CREATE TABLE reporting.outbox_messages (
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
    IF NOT EXISTS(SELECT 1 FROM reporting.__ef_migrations_history WHERE "MigrationId" = '20260929124847_InitialCreate') THEN
    CREATE TABLE reporting.sales_records (
        "OrderId" uuid NOT NULL,
        "ConfirmedOnUtc" timestamp with time zone NOT NULL,
        "ConfirmedOnDate" date NOT NULL,
        "Total" numeric(14,2) NOT NULL,
        "ItemCount" integer NOT NULL,
        "IsCancelled" boolean NOT NULL,
        "CancelledOnUtc" timestamp with time zone,
        "TenantId" uuid NOT NULL,
        CONSTRAINT "PK_sales_records" PRIMARY KEY ("OrderId")
    );
    END IF;
END $EF$;

DO $EF$
BEGIN
    IF NOT EXISTS(SELECT 1 FROM reporting.__ef_migrations_history WHERE "MigrationId" = '20260929124847_InitialCreate') THEN
    CREATE INDEX ix_outbox_messages_unprocessed ON reporting.outbox_messages ("OccurredOnUtc") WHERE "ProcessedOnUtc" IS NULL;
    END IF;
END $EF$;

DO $EF$
BEGIN
    IF NOT EXISTS(SELECT 1 FROM reporting.__ef_migrations_history WHERE "MigrationId" = '20260929124847_InitialCreate') THEN
    CREATE INDEX "IX_sales_records_TenantId_ConfirmedOnDate" ON reporting.sales_records ("TenantId", "ConfirmedOnDate");
    END IF;
END $EF$;

DO $EF$
BEGIN
    IF NOT EXISTS(SELECT 1 FROM reporting.__ef_migrations_history WHERE "MigrationId" = '20260929124847_InitialCreate') THEN
    INSERT INTO reporting.__ef_migrations_history ("MigrationId", "ProductVersion")
    VALUES ('20260929124847_InitialCreate', '10.0.4');
    END IF;
END $EF$;
COMMIT;

