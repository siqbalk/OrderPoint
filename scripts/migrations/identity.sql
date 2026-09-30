DO $EF$
BEGIN
    IF NOT EXISTS(SELECT 1 FROM pg_namespace WHERE nspname = 'identity') THEN
        CREATE SCHEMA identity;
    END IF;
END $EF$;
CREATE TABLE IF NOT EXISTS identity.__ef_migrations_history (
    "MigrationId" character varying(150) NOT NULL,
    "ProductVersion" character varying(32) NOT NULL,
    CONSTRAINT "PK___ef_migrations_history" PRIMARY KEY ("MigrationId")
);

START TRANSACTION;

DO $EF$
BEGIN
    IF NOT EXISTS(SELECT 1 FROM identity.__ef_migrations_history WHERE "MigrationId" = '20260929124804_InitialCreate') THEN
        IF NOT EXISTS(SELECT 1 FROM pg_namespace WHERE nspname = 'identity') THEN
            CREATE SCHEMA identity;
        END IF;
    END IF;
END $EF$;

DO $EF$
BEGIN
    IF NOT EXISTS(SELECT 1 FROM identity.__ef_migrations_history WHERE "MigrationId" = '20260929124804_InitialCreate') THEN
    CREATE TABLE identity.outbox_messages (
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
    IF NOT EXISTS(SELECT 1 FROM identity.__ef_migrations_history WHERE "MigrationId" = '20260929124804_InitialCreate') THEN
    CREATE TABLE identity.tenants (
        "Id" uuid NOT NULL,
        "Name" character varying(100) NOT NULL,
        "Plan" character varying(20) NOT NULL,
        "Status" character varying(20) NOT NULL,
        "CreatedOnUtc" timestamp with time zone NOT NULL,
        "PlanChangedOnUtc" timestamp with time zone,
        CONSTRAINT "PK_tenants" PRIMARY KEY ("Id")
    );
    END IF;
END $EF$;

DO $EF$
BEGIN
    IF NOT EXISTS(SELECT 1 FROM identity.__ef_migrations_history WHERE "MigrationId" = '20260929124804_InitialCreate') THEN
    CREATE TABLE identity.users (
        "Id" uuid NOT NULL,
        "TenantId" uuid NOT NULL,
        "Email" character varying(256) NOT NULL,
        "DisplayName" character varying(100) NOT NULL,
        "PasswordHash" character varying(500),
        "Role" character varying(20) NOT NULL,
        "Status" character varying(20) NOT NULL,
        "InvitationTokenHash" character varying(128),
        "InvitationExpiresOnUtc" timestamp with time zone,
        "CreatedOnUtc" timestamp with time zone NOT NULL,
        "LastLoginOnUtc" timestamp with time zone,
        CONSTRAINT "PK_users" PRIMARY KEY ("Id"),
        CONSTRAINT "FK_users_tenants_TenantId" FOREIGN KEY ("TenantId") REFERENCES identity.tenants ("Id") ON DELETE CASCADE
    );
    END IF;
END $EF$;

DO $EF$
BEGIN
    IF NOT EXISTS(SELECT 1 FROM identity.__ef_migrations_history WHERE "MigrationId" = '20260929124804_InitialCreate') THEN
    CREATE INDEX ix_outbox_messages_unprocessed ON identity.outbox_messages ("OccurredOnUtc") WHERE "ProcessedOnUtc" IS NULL;
    END IF;
END $EF$;

DO $EF$
BEGIN
    IF NOT EXISTS(SELECT 1 FROM identity.__ef_migrations_history WHERE "MigrationId" = '20260929124804_InitialCreate') THEN
    CREATE UNIQUE INDEX "IX_users_Email" ON identity.users ("Email");
    END IF;
END $EF$;

DO $EF$
BEGIN
    IF NOT EXISTS(SELECT 1 FROM identity.__ef_migrations_history WHERE "MigrationId" = '20260929124804_InitialCreate') THEN
    CREATE UNIQUE INDEX "IX_users_InvitationTokenHash" ON identity.users ("InvitationTokenHash") WHERE "InvitationTokenHash" IS NOT NULL;
    END IF;
END $EF$;

DO $EF$
BEGIN
    IF NOT EXISTS(SELECT 1 FROM identity.__ef_migrations_history WHERE "MigrationId" = '20260929124804_InitialCreate') THEN
    CREATE INDEX "IX_users_TenantId" ON identity.users ("TenantId");
    END IF;
END $EF$;

DO $EF$
BEGIN
    IF NOT EXISTS(SELECT 1 FROM identity.__ef_migrations_history WHERE "MigrationId" = '20260929124804_InitialCreate') THEN
    INSERT INTO identity.__ef_migrations_history ("MigrationId", "ProductVersion")
    VALUES ('20260929124804_InitialCreate', '10.0.4');
    END IF;
END $EF$;
COMMIT;

