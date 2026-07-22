-- Copyright (C) 2026 Orhan Özşahin — Asdamir.
-- Licensed under the GNU Lesser General Public License v3.0. See LICENSE.
-- SPDX-License-Identifier: LGPL-3.0-or-later
--
-- Asdamir background-run primitive — schema + guarded state-transition procs.
-- SOURCE OF TRUTH: this is a verbatim copy of src/Asdamir.Data/BackgroundRuns/BackgroundRuns.sql
-- (Asdamir.Data.BackgroundRuns.BackgroundRunsSchema), embedded here as a `new app` template asset so
-- the generated app ships it as a journaled migration. Keep the two in sync — if the core DDL changes,
-- re-copy it here (there is no per-app token, so it is a plain copy).
-- PLACEMENT: this is APP-OPERATIONAL state (the lifecycle of the app's OWN long-running jobs, e.g.
-- reconciliation), NOT central management data — per the CENTRAL rule it lives in the generated
-- app's OWN business database (alongside its business tables), NOT in AsdamirVault. Ship this via
-- the app's journaled db/migrations (the template follow-up emits it as a versioned migration).
--
-- Fully idempotent (IF NOT EXISTS / CREATE OR ALTER) so `asdamir db apply` is safe to re-run.
-- No numeric-money columns here.
SET QUOTED_IDENTIFIER ON;
GO

IF OBJECT_ID(N'dbo.BackgroundRuns', N'U') IS NULL
BEGIN
    CREATE TABLE dbo.BackgroundRuns
    (
        RunId             UNIQUEIDENTIFIER NOT NULL CONSTRAINT PK_BackgroundRuns PRIMARY KEY,
        TenantId          NVARCHAR(128)    NOT NULL,
        JobType           NVARCHAR(200)    NOT NULL,
        Payload           NVARCHAR(MAX)    NULL,
        DedupKey          NVARCHAR(256)    NULL,
        State             TINYINT          NOT NULL,   -- 0 Pending, 1 Running, 2 Completed, 3 Failed, 4 Interrupted
        ProgressCompleted INT              NULL,
        ProgressTotal     INT              NULL,
        ResultRef         NVARCHAR(512)    NULL,
        ErrorSummary      NVARCHAR(2000)   NULL,
        OwnerToken        NVARCHAR(200)    NULL,       -- node/process that owns a Running row (single-node today)
        CreatedAtUtc      DATETIME2(3)     NOT NULL CONSTRAINT DF_BackgroundRuns_Created DEFAULT (SYSUTCDATETIME()),
        StartedAtUtc      DATETIME2(3)     NULL,
        CompletedAtUtc    DATETIME2(3)     NULL
    );
END
GO

-- Fast dedup lookup: an active (Pending/Running) run of a (Tenant, JobType, DedupKey).
IF NOT EXISTS (SELECT 1 FROM sys.indexes WHERE name = N'IX_BackgroundRuns_Dedup' AND object_id = OBJECT_ID(N'dbo.BackgroundRuns'))
BEGIN
    CREATE INDEX IX_BackgroundRuns_Dedup
        ON dbo.BackgroundRuns (TenantId, JobType, DedupKey, State)
        WHERE State IN (0, 1);
END
GO

-- Insert a Pending run. NOCOUNT ON so a client's ExecuteAsync() rowcount check isn't confused.
CREATE OR ALTER PROCEDURE dbo.BackgroundRuns_CreatePending
    @RunId    UNIQUEIDENTIFIER,
    @TenantId NVARCHAR(128),
    @JobType  NVARCHAR(200),
    @Payload  NVARCHAR(MAX),
    @DedupKey NVARCHAR(256)
AS
BEGIN
    SET NOCOUNT ON;
    INSERT INTO dbo.BackgroundRuns (RunId, TenantId, JobType, Payload, DedupKey, State)
    VALUES (@RunId, @TenantId, @JobType, @Payload, @DedupKey, 0);
END
GO

-- Dedup lookup: the id of an active run for (Tenant, JobType, DedupKey), else nothing.
CREATE OR ALTER PROCEDURE dbo.BackgroundRuns_FindActiveByDedup
    @TenantId NVARCHAR(128),
    @JobType  NVARCHAR(200),
    @DedupKey NVARCHAR(256)
AS
BEGIN
    SET NOCOUNT ON;
    SELECT TOP (1) RunId
    FROM dbo.BackgroundRuns
    WHERE TenantId = @TenantId
      AND JobType  = @JobType
      AND State IN (0, 1)
      AND ((@DedupKey IS NULL AND DedupKey IS NULL) OR DedupKey = @DedupKey)
    ORDER BY CreatedAtUtc ASC;
END
GO

-- Tenant-scoped status read.
CREATE OR ALTER PROCEDURE dbo.BackgroundRuns_Get
    @TenantId NVARCHAR(128),
    @RunId    UNIQUEIDENTIFIER
AS
BEGIN
    SET NOCOUNT ON;
    SELECT RunId, JobType, State, ProgressCompleted, ProgressTotal, ResultRef, ErrorSummary,
           CreatedAtUtc, StartedAtUtc, CompletedAtUtc
    FROM dbo.BackgroundRuns
    WHERE TenantId = @TenantId AND RunId = @RunId;
END
GO

-- Tenant-scoped payload read (kept separate from Get: the payload can be large).
CREATE OR ALTER PROCEDURE dbo.BackgroundRuns_GetPayload
    @TenantId NVARCHAR(128),
    @RunId    UNIQUEIDENTIFIER
AS
BEGIN
    SET NOCOUNT ON;
    SELECT Payload
    FROM dbo.BackgroundRuns
    WHERE TenantId = @TenantId AND RunId = @RunId;
END
GO

-- Guarded Pending -> Running. WHERE State=0 makes an illegal transition affect 0 rows;
-- SELECT @@ROWCOUNT lets the caller detect success (the NOCOUNT/rowcount convention).
CREATE OR ALTER PROCEDURE dbo.BackgroundRuns_MarkRunning
    @TenantId   NVARCHAR(128),
    @RunId      UNIQUEIDENTIFIER,
    @OwnerToken NVARCHAR(200)
AS
BEGIN
    SET NOCOUNT ON;
    UPDATE dbo.BackgroundRuns
        SET State = 1, OwnerToken = @OwnerToken, StartedAtUtc = SYSUTCDATETIME()
    WHERE TenantId = @TenantId AND RunId = @RunId AND State = 0;
    SELECT @@ROWCOUNT;
END
GO

-- Guarded Running -> Completed.
CREATE OR ALTER PROCEDURE dbo.BackgroundRuns_MarkCompleted
    @TenantId  NVARCHAR(128),
    @RunId     UNIQUEIDENTIFIER,
    @ResultRef NVARCHAR(512)
AS
BEGIN
    SET NOCOUNT ON;
    UPDATE dbo.BackgroundRuns
        SET State = 2, ResultRef = @ResultRef, CompletedAtUtc = SYSUTCDATETIME()
    WHERE TenantId = @TenantId AND RunId = @RunId AND State = 1;
    SELECT @@ROWCOUNT;
END
GO

-- Guarded Running -> Failed.
CREATE OR ALTER PROCEDURE dbo.BackgroundRuns_MarkFailed
    @TenantId     NVARCHAR(128),
    @RunId        UNIQUEIDENTIFIER,
    @ErrorSummary NVARCHAR(2000)
AS
BEGIN
    SET NOCOUNT ON;
    UPDATE dbo.BackgroundRuns
        SET State = 3, ErrorSummary = @ErrorSummary, CompletedAtUtc = SYSUTCDATETIME()
    WHERE TenantId = @TenantId AND RunId = @RunId AND State = 1;
    SELECT @@ROWCOUNT;
END
GO

-- Progress update (not a state transition; only touches an active run).
CREATE OR ALTER PROCEDURE dbo.BackgroundRuns_UpdateProgress
    @TenantId  NVARCHAR(128),
    @RunId     UNIQUEIDENTIFIER,
    @Completed INT,
    @Total     INT
AS
BEGIN
    SET NOCOUNT ON;
    UPDATE dbo.BackgroundRuns
        SET ProgressCompleted = @Completed, ProgressTotal = @Total
    WHERE TenantId = @TenantId AND RunId = @RunId AND State IN (0, 1);
END
GO

-- Restart-recovery: any Pending/Running left by a dead process -> Interrupted (ALL tenants).
CREATE OR ALTER PROCEDURE dbo.BackgroundRuns_RecoverInterrupted
AS
BEGIN
    SET NOCOUNT ON;
    UPDATE dbo.BackgroundRuns
        SET State = 4,
            ErrorSummary = COALESCE(ErrorSummary, N'Interrupted: owning process ended before completion.'),
            CompletedAtUtc = SYSUTCDATETIME()
    WHERE State IN (0, 1);
    SELECT @@ROWCOUNT;
END
GO
