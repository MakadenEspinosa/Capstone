CREATE TABLE IF NOT EXISTS "__EFMigrationsHistory" (
    "MigrationId" character varying(150) NOT NULL,
    "ProductVersion" character varying(32) NOT NULL,
    CONSTRAINT "PK___EFMigrationsHistory" PRIMARY KEY ("MigrationId")
);

START TRANSACTION;

DO $EF$
BEGIN
    IF NOT EXISTS(SELECT 1 FROM "__EFMigrationsHistory" WHERE "MigrationId" = '20261023001525_InitialCreate') THEN
    CREATE TABLE events (
        "Id" uuid NOT NULL,
        "Title" character varying(200) NOT NULL,
        "CreatedAtUtc" timestamp with time zone NOT NULL,
        "UpdatedAtUtc" timestamp with time zone,
        CONSTRAINT "PK_events" PRIMARY KEY ("Id")
    );
    END IF;
END $EF$;

DO $EF$
BEGIN
    IF NOT EXISTS(SELECT 1 FROM "__EFMigrationsHistory" WHERE "MigrationId" = '20261023001525_InitialCreate') THEN
    CREATE TABLE users (
        "Id" uuid NOT NULL,
        "Email" character varying(320) NOT NULL,
        "PasswordHash" text NOT NULL,
        "SystemRole" character varying(32) NOT NULL,
        "Status" character varying(32) NOT NULL,
        bio character varying(1000),
        first_name character varying(100) NOT NULL,
        last_name character varying(100) NOT NULL,
        phone_number character varying(32),
        "CreatedAtUtc" timestamp with time zone NOT NULL,
        "UpdatedAtUtc" timestamp with time zone,
        CONSTRAINT "PK_users" PRIMARY KEY ("Id")
    );
    END IF;
END $EF$;

DO $EF$
BEGIN
    IF NOT EXISTS(SELECT 1 FROM "__EFMigrationsHistory" WHERE "MigrationId" = '20261023001525_InitialCreate') THEN
    CREATE TABLE event_participants (
        "EventId" uuid NOT NULL,
        "UserId" uuid NOT NULL,
        "Role" character varying(32) NOT NULL,
        CONSTRAINT "PK_event_participants" PRIMARY KEY ("EventId", "UserId"),
        CONSTRAINT "FK_event_participants_events_EventId" FOREIGN KEY ("EventId") REFERENCES events ("Id") ON DELETE CASCADE,
        CONSTRAINT "FK_event_participants_users_UserId" FOREIGN KEY ("UserId") REFERENCES users ("Id") ON DELETE CASCADE
    );
    END IF;
END $EF$;

DO $EF$
BEGIN
    IF NOT EXISTS(SELECT 1 FROM "__EFMigrationsHistory" WHERE "MigrationId" = '20261023001525_InitialCreate') THEN
    CREATE INDEX "IX_event_participants_UserId" ON event_participants ("UserId");
    END IF;
END $EF$;

DO $EF$
BEGIN
    IF NOT EXISTS(SELECT 1 FROM "__EFMigrationsHistory" WHERE "MigrationId" = '20261023001525_InitialCreate') THEN
    CREATE UNIQUE INDEX "IX_users_Email" ON users ("Email");
    END IF;
END $EF$;

DO $EF$
BEGIN
    IF NOT EXISTS(SELECT 1 FROM "__EFMigrationsHistory" WHERE "MigrationId" = '20261023001525_InitialCreate') THEN
    INSERT INTO "__EFMigrationsHistory" ("MigrationId", "ProductVersion")
    VALUES ('20261023001525_InitialCreate', '10.0.13');
    END IF;
END $EF$;
COMMIT;

