/*
 * Generated app — OWN database schema (business data only).
 *
 * Deliberately minimal: this database holds ONLY what this app's own business needs.
 * It does NOT carry the management schema (users / roles / permissions / menus / app
 * config / localization) — that lives in AsdamirVault and is administered from the
 * AppManagement control plane. The app consumes identity / authz / error-handling /
 * localization from the framework packages (DI), not from its own tables.
 *
 * Start with a single demo table; add real tables with `asdamir new entity`.
 * Idempotent (IF OBJECT_ID ... IS NULL / CREATE OR ALTER), so re-runs are no-ops.
 *
 * ALL data access goes through stored procedures — no inline SQL in repositories/Gateway. This is
 * single-tenant (the app IS the tenant; cross-app scoping by AppId lives in AsdamirVault, not here),
 * so these procs take no @appId. `asdamir new entity` emits the same proc-backed shape per table.
 */
SET ANSI_NULLS ON;
GO
SET QUOTED_IDENTIFIER ON;
GO

IF OBJECT_ID(N'[dbo].[DemoItems]', N'U') IS NULL
BEGIN
    CREATE TABLE [dbo].[DemoItems](
        [Id]           INT IDENTITY(1,1) NOT NULL CONSTRAINT [PK_DemoItems] PRIMARY KEY,
        [Name]         NVARCHAR(200) NOT NULL,
        [IsActive]     BIT NOT NULL CONSTRAINT [DF_DemoItems_IsActive] DEFAULT (1),
        [CreatedAtUtc] DATETIME2(7) NOT NULL CONSTRAINT [DF_DemoItems_CreatedAtUtc] DEFAULT (SYSUTCDATETIME())
    );
END
GO

-- ── DemoItems CRUD stored procedures (the only way the app touches this table) ──────────────────
CREATE OR ALTER PROCEDURE dbo.DemoItems_List
AS
BEGIN
    SET NOCOUNT ON;
    SELECT Id, Name, IsActive, CreatedAtUtc FROM dbo.DemoItems ORDER BY Id DESC;
END
GO

CREATE OR ALTER PROCEDURE dbo.DemoItems_Count
AS
BEGIN
    SET NOCOUNT ON;
    SELECT COUNT(*) FROM dbo.DemoItems WHERE IsActive = 1;
END
GO

CREATE OR ALTER PROCEDURE dbo.DemoItems_GetById
    @Id INT
AS
BEGIN
    SET NOCOUNT ON;
    SELECT Id, Name, IsActive, CreatedAtUtc FROM dbo.DemoItems WHERE Id = @Id;
END
GO

CREATE OR ALTER PROCEDURE dbo.DemoItems_Insert
    @Name NVARCHAR(200),
    @IsActive BIT
AS
BEGIN
    SET NOCOUNT ON;
    INSERT INTO dbo.DemoItems (Name, IsActive)
    OUTPUT INSERTED.Id
    VALUES (@Name, @IsActive);
END
GO

CREATE OR ALTER PROCEDURE dbo.DemoItems_Update
    @Id INT,
    @Name NVARCHAR(200),
    @IsActive BIT
AS
BEGIN
    SET NOCOUNT ON;
    UPDATE dbo.DemoItems SET Name = @Name, IsActive = @IsActive WHERE Id = @Id;
    SELECT @@ROWCOUNT;
END
GO

CREATE OR ALTER PROCEDURE dbo.DemoItems_Delete
    @Id INT
AS
BEGIN
    SET NOCOUNT ON;
    DELETE FROM dbo.DemoItems WHERE Id = @Id;
    SELECT @@ROWCOUNT;
END
GO

-- ── Audit trail (this app's own; served to AppManagement via gateway/admin/audittrail) ────────────
-- Written by AuditActionFilter on every state-changing request. Guid Id matches the AuditEntryView
-- orchestration contract. Idempotent (IF OBJECT_ID ... IS NULL / CREATE OR ALTER).
IF OBJECT_ID(N'[dbo].[AuditLog]', N'U') IS NULL
BEGIN
    CREATE TABLE [dbo].[AuditLog](
        [Id]           UNIQUEIDENTIFIER NOT NULL CONSTRAINT [PK_AuditLog] PRIMARY KEY DEFAULT (NEWID()),
        [CreatedAtUtc] DATETIME2(3)     NOT NULL CONSTRAINT [DF_AuditLog_CreatedAtUtc] DEFAULT (SYSUTCDATETIME()),
        [UserId]       INT              NULL,
        [UserName]     NVARCHAR(256)    NULL,
        [Action]       NVARCHAR(128)    NOT NULL,
        [TargetType]   NVARCHAR(128)    NULL,
        [TargetId]     NVARCHAR(128)    NULL,
        [Result]       NVARCHAR(32)     NOT NULL CONSTRAINT [DF_AuditLog_Result] DEFAULT (N'success'),
        [IpAddress]    NVARCHAR(45)     NULL,
        [UserAgent]    NVARCHAR(500)    NULL,
        [PayloadJson]  NVARCHAR(MAX)    NULL,
        [ErrorMessage] NVARCHAR(MAX)    NULL,
        [CorrelationId] NVARCHAR(64)    NULL
    );
    CREATE INDEX [IX_AuditLog_CreatedAt] ON [dbo].[AuditLog] ([CreatedAtUtc] DESC);
END
GO

CREATE OR ALTER PROCEDURE dbo.AuditLog_Insert
    @Action NVARCHAR(128), @UserId INT = NULL, @UserName NVARCHAR(256) = NULL,
    @TargetType NVARCHAR(128) = NULL, @TargetId NVARCHAR(128) = NULL, @Result NVARCHAR(32) = N'success',
    @IpAddress NVARCHAR(45) = NULL, @UserAgent NVARCHAR(500) = NULL, @PayloadJson NVARCHAR(MAX) = NULL,
    @ErrorMessage NVARCHAR(MAX) = NULL, @CorrelationId NVARCHAR(64) = NULL
AS
BEGIN
    SET NOCOUNT ON;
    INSERT INTO dbo.AuditLog (Action, UserId, UserName, TargetType, TargetId, Result, IpAddress, UserAgent, PayloadJson, ErrorMessage, CorrelationId)
    VALUES (@Action, @UserId, @UserName, @TargetType, @TargetId, @Result, @IpAddress, @UserAgent, @PayloadJson, @ErrorMessage, @CorrelationId);
END
GO

-- Read side for gateway/admin/audittrail → AuditEntryView (Id, TimestampUtc, Actor, Action, TargetType,
-- TargetId, Result, IpAddress, DetailsJson). Actor is never null (falls back to '-').
CREATE OR ALTER PROCEDURE dbo.AuditLog_List
    @FromUtc DATETIME2(3) = NULL, @ToUtc DATETIME2(3) = NULL,
    @Actor NVARCHAR(256) = NULL, @Action NVARCHAR(128) = NULL, @Limit INT = 200
AS
BEGIN
    SET NOCOUNT ON;
    SELECT TOP (@Limit)
        Id,
        CreatedAtUtc         AS TimestampUtc,
        ISNULL(UserName, N'-') AS Actor,
        Action,
        TargetType,
        TargetId,
        Result,
        IpAddress,
        PayloadJson          AS DetailsJson
    FROM dbo.AuditLog
    WHERE (@FromUtc IS NULL OR CreatedAtUtc >= @FromUtc)
      AND (@ToUtc   IS NULL OR CreatedAtUtc <= @ToUtc)
      AND (@Actor   IS NULL OR UserName LIKE N'%' + @Actor + N'%')
      AND (@Action  IS NULL OR Action = @Action)
    ORDER BY CreatedAtUtc DESC;
END
GO
