DO $EF$
BEGIN
    IF NOT EXISTS(SELECT 1 FROM pg_namespace WHERE nspname = 'notifications') THEN
        CREATE SCHEMA notifications;
    END IF;
END $EF$;
CREATE TABLE IF NOT EXISTS notifications.__ef_migrations_history (
    "MigrationId" character varying(150) NOT NULL,
    "ProductVersion" character varying(32) NOT NULL,
    CONSTRAINT "PK___ef_migrations_history" PRIMARY KEY ("MigrationId")
);

START TRANSACTION;

DO $EF$
BEGIN
    IF NOT EXISTS(SELECT 1 FROM notifications.__ef_migrations_history WHERE "MigrationId" = '20260929124839_InitialCreate') THEN
        IF NOT EXISTS(SELECT 1 FROM pg_namespace WHERE nspname = 'notifications') THEN
            CREATE SCHEMA notifications;
        END IF;
    END IF;
END $EF$;

DO $EF$
BEGIN
    IF NOT EXISTS(SELECT 1 FROM notifications.__ef_migrations_history WHERE "MigrationId" = '20260929124839_InitialCreate') THEN
    CREATE TABLE notifications.notifications (
        "Id" uuid NOT NULL,
        "SourceEventId" uuid NOT NULL,
        "Channel" character varying(20) NOT NULL,
        "Recipient" character varying(256) NOT NULL,
        "Subject" character varying(300) NOT NULL,
        "Body" text NOT NULL,
        "SentOnUtc" timestamp with time zone NOT NULL,
        "TenantId" uuid NOT NULL,
        CONSTRAINT "PK_notifications" PRIMARY KEY ("Id")
    );
    END IF;
END $EF$;

DO $EF$
BEGIN
    IF NOT EXISTS(SELECT 1 FROM notifications.__ef_migrations_history WHERE "MigrationId" = '20260929124839_InitialCreate') THEN
    CREATE TABLE notifications.outbox_messages (
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
    IF NOT EXISTS(SELECT 1 FROM notifications.__ef_migrations_history WHERE "MigrationId" = '20260929124839_InitialCreate') THEN
    CREATE UNIQUE INDEX "IX_notifications_SourceEventId_Recipient" ON notifications.notifications ("SourceEventId", "Recipient");
    END IF;
END $EF$;

DO $EF$
BEGIN
    IF NOT EXISTS(SELECT 1 FROM notifications.__ef_migrations_history WHERE "MigrationId" = '20260929124839_InitialCreate') THEN
    CREATE INDEX "IX_notifications_TenantId_SentOnUtc" ON notifications.notifications ("TenantId", "SentOnUtc");
    END IF;
END $EF$;

DO $EF$
BEGIN
    IF NOT EXISTS(SELECT 1 FROM notifications.__ef_migrations_history WHERE "MigrationId" = '20260929124839_InitialCreate') THEN
    CREATE INDEX ix_outbox_messages_unprocessed ON notifications.outbox_messages ("OccurredOnUtc") WHERE "ProcessedOnUtc" IS NULL;
    END IF;
END $EF$;

DO $EF$
BEGIN
    IF NOT EXISTS(SELECT 1 FROM notifications.__ef_migrations_history WHERE "MigrationId" = '20260929124839_InitialCreate') THEN
    INSERT INTO notifications.__ef_migrations_history ("MigrationId", "ProductVersion")
    VALUES ('20260929124839_InitialCreate', '10.0.4');
    END IF;
END $EF$;
COMMIT;

