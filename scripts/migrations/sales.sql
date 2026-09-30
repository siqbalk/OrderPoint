DO $EF$
BEGIN
    IF NOT EXISTS(SELECT 1 FROM pg_namespace WHERE nspname = 'sales') THEN
        CREATE SCHEMA sales;
    END IF;
END $EF$;
CREATE TABLE IF NOT EXISTS sales.__ef_migrations_history (
    "MigrationId" character varying(150) NOT NULL,
    "ProductVersion" character varying(32) NOT NULL,
    CONSTRAINT "PK___ef_migrations_history" PRIMARY KEY ("MigrationId")
);

START TRANSACTION;

DO $EF$
BEGIN
    IF NOT EXISTS(SELECT 1 FROM sales.__ef_migrations_history WHERE "MigrationId" = '20260929124831_InitialCreate') THEN
        IF NOT EXISTS(SELECT 1 FROM pg_namespace WHERE nspname = 'sales') THEN
            CREATE SCHEMA sales;
        END IF;
    END IF;
END $EF$;

DO $EF$
BEGIN
    IF NOT EXISTS(SELECT 1 FROM sales.__ef_migrations_history WHERE "MigrationId" = '20260929124831_InitialCreate') THEN
    CREATE TABLE sales.orders (
        "Id" uuid NOT NULL,
        "CustomerName" character varying(200) NOT NULL,
        "CustomerEmail" character varying(256) NOT NULL,
        "Status" character varying(30) NOT NULL,
        "Total" numeric(14,2) NOT NULL,
        "PlacedOnUtc" timestamp with time zone NOT NULL,
        "ConfirmedOnUtc" timestamp with time zone,
        "CancelledOnUtc" timestamp with time zone,
        "RejectionReason" character varying(500),
        "TenantId" uuid NOT NULL,
        CONSTRAINT "PK_orders" PRIMARY KEY ("Id")
    );
    END IF;
END $EF$;

DO $EF$
BEGIN
    IF NOT EXISTS(SELECT 1 FROM sales.__ef_migrations_history WHERE "MigrationId" = '20260929124831_InitialCreate') THEN
    CREATE TABLE sales.outbox_messages (
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
    IF NOT EXISTS(SELECT 1 FROM sales.__ef_migrations_history WHERE "MigrationId" = '20260929124831_InitialCreate') THEN
    CREATE TABLE sales.order_lines (
        "Id" uuid NOT NULL,
        "Sku" character varying(50) NOT NULL,
        "ProductName" character varying(200) NOT NULL,
        "Quantity" integer NOT NULL,
        "UnitPrice" numeric(12,2) NOT NULL,
        "OrderId" uuid NOT NULL,
        CONSTRAINT "PK_order_lines" PRIMARY KEY ("Id"),
        CONSTRAINT "FK_order_lines_orders_OrderId" FOREIGN KEY ("OrderId") REFERENCES sales.orders ("Id") ON DELETE CASCADE
    );
    END IF;
END $EF$;

DO $EF$
BEGIN
    IF NOT EXISTS(SELECT 1 FROM sales.__ef_migrations_history WHERE "MigrationId" = '20260929124831_InitialCreate') THEN
    CREATE INDEX "IX_order_lines_OrderId" ON sales.order_lines ("OrderId");
    END IF;
END $EF$;

DO $EF$
BEGIN
    IF NOT EXISTS(SELECT 1 FROM sales.__ef_migrations_history WHERE "MigrationId" = '20260929124831_InitialCreate') THEN
    CREATE INDEX "IX_orders_TenantId_PlacedOnUtc" ON sales.orders ("TenantId", "PlacedOnUtc");
    END IF;
END $EF$;

DO $EF$
BEGIN
    IF NOT EXISTS(SELECT 1 FROM sales.__ef_migrations_history WHERE "MigrationId" = '20260929124831_InitialCreate') THEN
    CREATE INDEX "IX_orders_TenantId_Status" ON sales.orders ("TenantId", "Status");
    END IF;
END $EF$;

DO $EF$
BEGIN
    IF NOT EXISTS(SELECT 1 FROM sales.__ef_migrations_history WHERE "MigrationId" = '20260929124831_InitialCreate') THEN
    CREATE INDEX ix_outbox_messages_unprocessed ON sales.outbox_messages ("OccurredOnUtc") WHERE "ProcessedOnUtc" IS NULL;
    END IF;
END $EF$;

DO $EF$
BEGIN
    IF NOT EXISTS(SELECT 1 FROM sales.__ef_migrations_history WHERE "MigrationId" = '20260929124831_InitialCreate') THEN
    INSERT INTO sales.__ef_migrations_history ("MigrationId", "ProductVersion")
    VALUES ('20260929124831_InitialCreate', '10.0.4');
    END IF;
END $EF$;
COMMIT;

