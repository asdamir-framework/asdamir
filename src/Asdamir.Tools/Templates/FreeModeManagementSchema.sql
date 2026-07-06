-- Free-mode management schema — the app's OWN identity / RBAC / menu / localization / config tables.
--
-- FREE MODE ONLY. A free-tier app is SELF-CONTAINED: it does NOT reach a central AsdamirVault via
-- AppManagement. These management tables therefore live in the app's own database, single-tenant
-- ("the app IS the tenant"), so there is NO AppId column and NO TenantId column anywhere here — the
-- whole database belongs to one app. (Commercial mode keeps using AsdamirVault, AppId-scoped, and this
-- file is NOT emitted there.)
--
-- Derived from the canonical AsdamirVault schema with the multi-app dimension removed: AppId dropped
-- from AppConfigurations + LocalizationResource, TenantId dropped from LocalizationResource, and the
-- unique keys re-expressed without AppId (Email / Name / Key are unique within the single app).
--
-- DELIBERATELY ABSENT: AppLog / AuditLog (free mode logs to file + console via ILogger, not the DB) and
-- the central-only tables (Apps, UserAppRoles, Outbox, RateLimitCounters, TenantConnections).
--
-- Stored procedures (login / menu / RBAC / localization / config) are in the companion proc migration.
-- Idempotent: every object is guarded, so re-applying via the journaled `db apply` runner is a no-op.

------------------------------------------------------------------------------------------------------
-- Identity
------------------------------------------------------------------------------------------------------
IF OBJECT_ID(N'[dbo].[Users]', N'U') IS NULL
BEGIN
    CREATE TABLE [dbo].[Users](
        [Id]                     [int] IDENTITY(1,1) NOT NULL CONSTRAINT [PK_Users] PRIMARY KEY CLUSTERED,
        [Email]                  [nvarchar](256) NOT NULL,
        [Name]                   [nvarchar](100) NOT NULL,
        [PasswordHash]           [nvarchar](500) NOT NULL,
        [IsActive]               [bit] NOT NULL CONSTRAINT [DF_Users_IsActive] DEFAULT (1),
        [CreatedAt]              [datetime2](7) NOT NULL CONSTRAINT [DF_Users_CreatedAt] DEFAULT (SYSUTCDATETIME()),
        [LastLoginAt]            [datetime2](0) NULL,
        [UpdatedAt]              [datetime2](0) NULL,
        [Notes]                  [nvarchar](500) NULL,
        [PasswordResetToken]     [nvarchar](256) NULL,
        [PasswordResetTokenExpiry] [datetime2](7) NULL,
        [IsTwoFactorEnabled]     [bit] NOT NULL CONSTRAINT [DF_Users_IsTwoFactorEnabled] DEFAULT (0),
        [PhoneNumber]            [nvarchar](20) NULL,
        [PhoneNumberVerified]    [bit] NOT NULL CONSTRAINT [DF_Users_PhoneNumberVerified] DEFAULT (0),
        [FailedLoginCount]       [int] NOT NULL CONSTRAINT [DF_Users_FailedLoginCount] DEFAULT (0),
        [LockedUntilUtc]         [datetime2](3) NULL,
        -- Single app → email is globally unique in this DB (was UNIQUE(AppId, Email) in AsdamirVault).
        CONSTRAINT [UQ_Users_Email] UNIQUE ([Email])
    );
END
GO

------------------------------------------------------------------------------------------------------
-- RBAC catalog (Roles / Permissions) + junctions
------------------------------------------------------------------------------------------------------
IF OBJECT_ID(N'[dbo].[Roles]', N'U') IS NULL
BEGIN
    CREATE TABLE [dbo].[Roles](
        [Id]          [int] IDENTITY(1,1) NOT NULL CONSTRAINT [PK_Roles] PRIMARY KEY CLUSTERED,
        [Name]        [nvarchar](100) NOT NULL,
        [Description] [nvarchar](256) NULL,
        [CreatedAt]   [datetime2](0) NOT NULL CONSTRAINT [DF_Roles_CreatedAt] DEFAULT (SYSUTCDATETIME()),
        CONSTRAINT [UQ_Roles_Name] UNIQUE ([Name])
    );
END
GO

IF OBJECT_ID(N'[dbo].[Permissions]', N'U') IS NULL
BEGIN
    CREATE TABLE [dbo].[Permissions](
        [Id]          [int] IDENTITY(1,1) NOT NULL CONSTRAINT [PK_Permissions] PRIMARY KEY CLUSTERED,
        [Name]        [nvarchar](100) NOT NULL,
        [Description] [nvarchar](256) NULL,
        [CreatedAt]   [datetime2](0) NOT NULL CONSTRAINT [DF_Permissions_CreatedAt] DEFAULT (SYSUTCDATETIME()),
        CONSTRAINT [UQ_Permissions_Name] UNIQUE ([Name])
    );
END
GO

IF OBJECT_ID(N'[dbo].[RolePermissions]', N'U') IS NULL
BEGIN
    CREATE TABLE [dbo].[RolePermissions](
        [RoleId]       [int] NOT NULL,
        [PermissionId] [int] NOT NULL,
        [AssignedAt]   [datetime2](0) NOT NULL CONSTRAINT [DF_RolePermissions_AssignedAt] DEFAULT (SYSUTCDATETIME()),
        CONSTRAINT [PK_RolePermissions] PRIMARY KEY CLUSTERED ([RoleId], [PermissionId]),
        CONSTRAINT [FK_RolePermissions_Roles]       FOREIGN KEY ([RoleId])       REFERENCES [dbo].[Roles]([Id])       ON DELETE CASCADE,
        CONSTRAINT [FK_RolePermissions_Permissions] FOREIGN KEY ([PermissionId]) REFERENCES [dbo].[Permissions]([Id]) ON DELETE CASCADE
    );
END
GO

IF OBJECT_ID(N'[dbo].[UserRoles]', N'U') IS NULL
BEGIN
    CREATE TABLE [dbo].[UserRoles](
        [UserId]     [int] NOT NULL,
        [RoleId]     [int] NOT NULL,
        [AssignedAt] [datetime2](0) NOT NULL CONSTRAINT [DF_UserRoles_AssignedAt] DEFAULT (SYSUTCDATETIME()),
        CONSTRAINT [PK_UserRoles] PRIMARY KEY CLUSTERED ([UserId], [RoleId]),
        CONSTRAINT [FK_UserRoles_Users] FOREIGN KEY ([UserId]) REFERENCES [dbo].[Users]([Id]) ON DELETE CASCADE,
        CONSTRAINT [FK_UserRoles_Roles] FOREIGN KEY ([RoleId]) REFERENCES [dbo].[Roles]([Id]) ON DELETE CASCADE
    );
END
GO

------------------------------------------------------------------------------------------------------
-- Sessions (refresh tokens) + 2FA
------------------------------------------------------------------------------------------------------
IF OBJECT_ID(N'[dbo].[RefreshTokens]', N'U') IS NULL
BEGIN
    -- No Context column (AsdamirVault's console-vs-app distinction) — a single-app free DB only ever
    -- has "app" tokens, so the audience split is unnecessary here.
    CREATE TABLE [dbo].[RefreshTokens](
        [Id]           [int] IDENTITY(1,1) NOT NULL CONSTRAINT [PK_RefreshTokens] PRIMARY KEY CLUSTERED,
        [UserId]       [int] NOT NULL,
        [TokenHash]    [nvarchar](128) NOT NULL,
        [ExpiresAtUtc] [datetime2](7) NOT NULL,
        [CreatedAtUtc] [datetime2](7) NOT NULL CONSTRAINT [DF_RefreshTokens_CreatedAtUtc] DEFAULT (SYSUTCDATETIME()),
        [RevokedAtUtc] [datetime2](7) NULL,
        [CreatedIp]    [nvarchar](64) NULL,
        [UserAgent]    [nvarchar](256) NULL,
        CONSTRAINT [FK_RefreshTokens_Users] FOREIGN KEY ([UserId]) REFERENCES [dbo].[Users]([Id]) ON DELETE CASCADE
    );
    CREATE NONCLUSTERED INDEX [IX_RefreshTokens_TokenHash] ON [dbo].[RefreshTokens]([TokenHash]);
END
GO

IF OBJECT_ID(N'[dbo].[TwoFactorCodes]', N'U') IS NULL
BEGIN
    CREATE TABLE [dbo].[TwoFactorCodes](
        [Id]                [int] IDENTITY(1,1) NOT NULL CONSTRAINT [PK_TwoFactorCodes] PRIMARY KEY CLUSTERED,
        [UserId]            [int] NOT NULL,
        [Code]              [nvarchar](10) NOT NULL,
        [PhoneNumber]       [nvarchar](20) NULL,
        [CreatedAtUtc]      [datetime2](7) NOT NULL CONSTRAINT [DF_TwoFactorCodes_CreatedAtUtc] DEFAULT (SYSUTCDATETIME()),
        [ExpiresAtUtc]      [datetime2](7) NOT NULL,
        [IsUsed]            [bit] NOT NULL CONSTRAINT [DF_TwoFactorCodes_IsUsed] DEFAULT (0),
        [UsedAtUtc]         [datetime2](7) NULL,
        [AttemptCount]      [int] NOT NULL CONSTRAINT [DF_TwoFactorCodes_AttemptCount] DEFAULT (0),
        [AttemptsRemaining] [int] NOT NULL CONSTRAINT [DF_TwoFactorCodes_AttemptsRemaining] DEFAULT (5),
        [TwoFactorToken]    [nvarchar](64) NULL,
        [IpAddress]         [nvarchar](50) NULL,
        [UserAgent]         [nvarchar](500) NULL,
        CONSTRAINT [FK_TwoFactorCodes_Users] FOREIGN KEY ([UserId]) REFERENCES [dbo].[Users]([Id]) ON DELETE CASCADE
    );
END
GO

------------------------------------------------------------------------------------------------------
-- Navigation menu
------------------------------------------------------------------------------------------------------
IF OBJECT_ID(N'[dbo].[Menus]', N'U') IS NULL
BEGIN
    CREATE TABLE [dbo].[Menus](
        [Id]           [int] IDENTITY(1,1) NOT NULL CONSTRAINT [PK_Menus] PRIMARY KEY CLUSTERED,
        [Name]         [nvarchar](255) NOT NULL,
        [Description]  [nvarchar](500) NULL,
        [Icon]         [nvarchar](100) NULL,
        [Url]          [nvarchar](500) NULL,
        [ParentId]     [int] NULL,
        [Order]        [int] NOT NULL CONSTRAINT [DF_Menus_Order] DEFAULT (0),
        [IsActive]     [bit] NOT NULL CONSTRAINT [DF_Menus_IsActive] DEFAULT (1),
        [IsVisible]    [bit] NOT NULL CONSTRAINT [DF_Menus_IsVisible] DEFAULT (1),
        [CreatedAtUtc] [datetime2](7) NOT NULL CONSTRAINT [DF_Menus_CreatedAtUtc] DEFAULT (SYSUTCDATETIME()),
        [UpdatedAtUtc] [datetime2](7) NULL,
        [CreatedBy]    [nvarchar](255) NOT NULL CONSTRAINT [DF_Menus_CreatedBy] DEFAULT (N'system'),
        [UpdatedBy]    [nvarchar](255) NULL,
        [PermissionId] [int] NULL,
        CONSTRAINT [FK_Menus_Parent]      FOREIGN KEY ([ParentId])     REFERENCES [dbo].[Menus]([Id]),
        CONSTRAINT [FK_Menus_Permissions] FOREIGN KEY ([PermissionId]) REFERENCES [dbo].[Permissions]([Id])
    );
END
GO

IF OBJECT_ID(N'[dbo].[UserMenuPermissions]', N'U') IS NULL
BEGIN
    CREATE TABLE [dbo].[UserMenuPermissions](
        [Id]           [int] IDENTITY(1,1) NOT NULL CONSTRAINT [PK_UserMenuPermissions] PRIMARY KEY CLUSTERED,
        [UserId]       [int] NOT NULL,
        [MenuId]       [int] NOT NULL,
        [CanView]      [bit] NOT NULL CONSTRAINT [DF_UserMenuPermissions_CanView]   DEFAULT (0),
        [CanCreate]    [bit] NOT NULL CONSTRAINT [DF_UserMenuPermissions_CanCreate] DEFAULT (0),
        [CanEdit]      [bit] NOT NULL CONSTRAINT [DF_UserMenuPermissions_CanEdit]   DEFAULT (0),
        [CanDelete]    [bit] NOT NULL CONSTRAINT [DF_UserMenuPermissions_CanDelete] DEFAULT (0),
        [PermissionId] [int] NULL,
        [CreatedAtUtc] [datetime2](0) NOT NULL CONSTRAINT [DF_UserMenuPermissions_CreatedAtUtc] DEFAULT (SYSUTCDATETIME()),
        [CreatedBy]    [nvarchar](128) NULL,
        [ExpiresAt]    [datetime2](0) NULL,
        [UpdatedAt]    [datetime2](0) NULL,
        [UpdatedBy]    [nvarchar](128) NULL,
        CONSTRAINT [FK_UserMenuPermissions_Users] FOREIGN KEY ([UserId]) REFERENCES [dbo].[Users]([Id]) ON DELETE CASCADE,
        CONSTRAINT [FK_UserMenuPermissions_Menus] FOREIGN KEY ([MenuId]) REFERENCES [dbo].[Menus]([Id]) ON DELETE CASCADE
    );
END
GO

------------------------------------------------------------------------------------------------------
-- Localization (single-tenant: no AppId, no TenantId)
------------------------------------------------------------------------------------------------------
IF OBJECT_ID(N'[dbo].[LocalizationResource]', N'U') IS NULL
BEGIN
    CREATE TABLE [dbo].[LocalizationResource](
        [Id]           [int] IDENTITY(1,1) NOT NULL CONSTRAINT [PK_LocalizationResource] PRIMARY KEY CLUSTERED,
        [Key]          [nvarchar](256) NOT NULL,
        [Culture]      [nvarchar](20) NOT NULL,
        [Value]        [nvarchar](max) NOT NULL,
        [Category]     [nvarchar](100) NULL,
        [CreatedAtUtc] [datetime2](7) NOT NULL CONSTRAINT [DF_LocalizationResource_CreatedAtUtc] DEFAULT (SYSUTCDATETIME()),
        [UpdatedAtUtc] [datetime2](7) NULL,
        [UpdatedBy]    [nvarchar](256) NULL,
        [IsHtml]       [bit] NOT NULL CONSTRAINT [DF_LocalizationResource_IsHtml] DEFAULT (0),
        -- Was UNIQUE(AppId, Key, Culture) in AsdamirVault; single-tenant → (Key, Culture).
        CONSTRAINT [UQ_LocalizationResource_Key_Culture] UNIQUE ([Key], [Culture])
    );
END
GO

IF OBJECT_ID(N'[dbo].[LocalizationVersion]', N'U') IS NULL
BEGIN
    CREATE TABLE [dbo].[LocalizationVersion](
        [Id]           [int] IDENTITY(1,1) NOT NULL CONSTRAINT [PK_LocalizationVersion] PRIMARY KEY CLUSTERED,
        [Version]      [bigint] NOT NULL CONSTRAINT [DF_LocalizationVersion_Version] DEFAULT (1),
        [UpdatedAtUtc] [datetime2](7) NOT NULL CONSTRAINT [DF_LocalizationVersion_UpdatedAtUtc] DEFAULT (SYSUTCDATETIME()),
        [UpdatedBy]    [nvarchar](256) NULL
    );
END
GO

------------------------------------------------------------------------------------------------------
-- Runtime configuration (single-tenant: no AppId)
------------------------------------------------------------------------------------------------------
IF OBJECT_ID(N'[dbo].[AppConfigurations]', N'U') IS NULL
BEGIN
    CREATE TABLE [dbo].[AppConfigurations](
        [Id]           [int] IDENTITY(1,1) NOT NULL CONSTRAINT [PK_AppConfigurations] PRIMARY KEY CLUSTERED,
        [Key]          [nvarchar](255) NOT NULL,
        [Value]        [nvarchar](max) NOT NULL,
        [IsActive]     [bit] NOT NULL CONSTRAINT [DF_AppConfigurations_IsActive] DEFAULT (1),
        [CreatedAt]    [datetime2](7) NOT NULL CONSTRAINT [DF_AppConfigurations_CreatedAt] DEFAULT (SYSUTCDATETIME()),
        [UpdatedAtUtc] [datetime2](7) NOT NULL CONSTRAINT [DF_AppConfigurations_UpdatedAtUtc] DEFAULT (SYSUTCDATETIME()),
        [CreatedBy]    [nvarchar](100) NULL,
        [UpdatedBy]    [nvarchar](256) NULL,
        [IsEncrypted]  [bit] NOT NULL CONSTRAINT [DF_AppConfigurations_IsEncrypted] DEFAULT (0),
        [IsSecret]     [bit] NOT NULL CONSTRAINT [DF_AppConfigurations_IsSecret] DEFAULT (0),
        [ValueType]    [nvarchar](50) NULL,
        [Category]     [nvarchar](100) NULL,
        [Description]  [nvarchar](500) NULL,
        -- Was UNIQUE(AppId, Key) in AsdamirVault; single-tenant → (Key).
        CONSTRAINT [UQ_AppConfigurations_Key] UNIQUE ([Key])
    );
END
GO
