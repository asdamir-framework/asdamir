-- Free-mode management stored procedures — the app's OWN login / RBAC / menu / localization / config
-- read+write logic, reading the single-tenant management tables created by the companion
-- FreeModeManagementSchema.sql.
--
-- FREE MODE ONLY. Adapted from the canonical AsdamirVault procs with the multi-app dimension removed:
-- every @appId parameter and every `AppId = @appId OR AppId IS NULL` filter is gone, because a free
-- app's database belongs to exactly one app ("the app IS the tenant"). Column-shape notes:
--   * IsSuperAdmin — the free Users table has no operator/end-user split, so the auth procs project a
--     constant CAST(0 AS BIT); the UserAuth shape stays identical to commercial for the local store.
--   * [Context]    — the free RefreshTokens table has no Context column (single audience), so the
--     create/read procs drop it.
--   * TenantId     — the free LocalizationResource is single-tenant, so the upsert drops it.
--
-- Auth safety: removing the AppId dimension does NOT weaken any gate — there is no other app to leak
-- across in a single-app DB. Every RBAC decision (UserRoles -> RolePermissions -> Permissions, the
-- admin.access bypass, the UserMenuPermissions override, and the IsActive login filter) is preserved.
--
-- Idempotent: CREATE OR ALTER is re-appliable, and the journaled `db apply` runner skips an
-- already-applied migration anyway.

------------------------------------------------------------------------------------------------------
-- Login / identity
------------------------------------------------------------------------------------------------------
CREATE OR ALTER PROCEDURE [dbo].[User_GetAuthByEmail]
    @email NVARCHAR(256)
AS
BEGIN
    SET NOCOUNT ON;
    -- IsActive = 1 IS the free login gate ("user exists + active"); there is no per-app grant to check.
    SELECT TOP 1 Id, Email, Name, PasswordHash, IsActive, IsTwoFactorEnabled,
                 PhoneNumber, PhoneNumberVerified, CAST(0 AS BIT) AS IsSuperAdmin
      FROM dbo.Users
     WHERE Email = @email AND IsActive = 1;
END
GO

CREATE OR ALTER PROCEDURE [dbo].[User_GetAuthById]
    @userId INT
AS
BEGIN
    SET NOCOUNT ON;
    SELECT TOP 1 Id, Email, Name, PasswordHash, IsActive, IsTwoFactorEnabled,
                 PhoneNumber, PhoneNumberVerified, CAST(0 AS BIT) AS IsSuperAdmin
      FROM dbo.Users
     WHERE Id = @userId AND IsActive = 1;
END
GO

CREATE OR ALTER PROCEDURE [dbo].[User_GetLockState]
    @userId INT
AS
BEGIN
    SET NOCOUNT ON;
    SELECT LockedUntilUtc FROM dbo.Users WHERE Id = @userId;
END
GO

CREATE OR ALTER PROCEDURE [dbo].[User_IncrementFailedLogin]
    @userId         INT,
    @max            INT,
    @lockoutMinutes INT
AS
BEGIN
    SET NOCOUNT ON;
    UPDATE dbo.Users
       SET FailedLoginCount = FailedLoginCount + 1,
           LockedUntilUtc =
               CASE
                   WHEN FailedLoginCount + 1 >= @max THEN DATEADD(MINUTE, @lockoutMinutes, SYSUTCDATETIME())
                   ELSE LockedUntilUtc
               END
     WHERE Id = @userId;
END
GO

CREATE OR ALTER PROCEDURE [dbo].[User_ResetFailedLogins]
    @userId INT
AS
BEGIN
    SET NOCOUNT ON;
    UPDATE dbo.Users SET FailedLoginCount = 0, LockedUntilUtc = NULL WHERE Id = @userId;
END
GO

CREATE OR ALTER PROCEDURE [dbo].[User_ClearLockout]
    @userId INT
AS
BEGIN
    SET NOCOUNT ON;
    UPDATE dbo.Users
       SET LockedUntilUtc   = NULL,
           FailedLoginCount = 0
     WHERE Id = @userId;
END
GO

CREATE OR ALTER PROCEDURE [dbo].[User_UpdatePasswordHash]
    @userId  INT,
    @newHash NVARCHAR(256)
AS
BEGIN
    SET NOCOUNT ON;
    UPDATE dbo.Users SET PasswordHash = @newHash WHERE Id = @userId;
END
GO

-- First-login / forced password change: read the flag so login can redirect to the change-password page.
CREATE OR ALTER PROCEDURE [dbo].[User_GetForcePasswordChange]
    @userId INT
AS
BEGIN
    SET NOCOUNT ON;
    SELECT ForcePasswordChange FROM dbo.Users WHERE Id = @userId;
END
GO

-- Change the password AND clear the ForcePasswordChange flag in one statement (the change-password
-- endpoint calls this after verifying the current password). PasswordHash is NVARCHAR(500) in the schema.
CREATE OR ALTER PROCEDURE [dbo].[User_ChangePassword]
    @userId  INT,
    @newHash NVARCHAR(500)
AS
BEGIN
    SET NOCOUNT ON;
    UPDATE dbo.Users
       SET PasswordHash        = @newHash,
           ForcePasswordChange = 0,
           UpdatedAt           = SYSUTCDATETIME()
     WHERE Id = @userId;
END
GO

------------------------------------------------------------------------------------------------------
-- RBAC — the user's permissions (source of the JWT `perm` claims). In free mode these come from the
-- local role chain (UserRoles -> RolePermissions -> Permissions), NOT the commercial UserAppRoles matrix.
------------------------------------------------------------------------------------------------------
CREATE OR ALTER PROCEDURE [dbo].[User_GetPermissions]
    @UserId INT
AS
BEGIN
    SET NOCOUNT ON;
    SELECT p.Name
      FROM dbo.Permissions p
      INNER JOIN dbo.RolePermissions rp ON p.Id = rp.PermissionId
      INNER JOIN dbo.UserRoles ur ON rp.RoleId = ur.RoleId
     WHERE ur.UserId = @UserId;
END
GO

------------------------------------------------------------------------------------------------------
-- Sessions (refresh tokens) — no [Context] column in free mode (single audience).
------------------------------------------------------------------------------------------------------
CREATE OR ALTER PROCEDURE [dbo].[RefreshToken_Create]
    @userId       INT,
    @hash         NVARCHAR(256),
    @expiresAtUtc DATETIME2(7)
AS
BEGIN
    SET NOCOUNT ON;
    INSERT INTO dbo.RefreshTokens (UserId, TokenHash, ExpiresAtUtc)
    VALUES (@userId, @hash, @expiresAtUtc);
END
GO

CREATE OR ALTER PROCEDURE [dbo].[RefreshToken_GetActiveByHash]
    @hash NVARCHAR(256)
AS
BEGIN
    SET NOCOUNT ON;
    SELECT TOP 1 Id, UserId, ExpiresAtUtc
      FROM dbo.RefreshTokens
     WHERE TokenHash = @hash AND RevokedAtUtc IS NULL;
END
GO

CREATE OR ALTER PROCEDURE [dbo].[RefreshToken_RevokeById]
    @id INT
AS
BEGIN
    SET NOCOUNT ON;
    UPDATE dbo.RefreshTokens
       SET RevokedAtUtc = SYSUTCDATETIME()
     WHERE Id = @id;
END
GO

CREATE OR ALTER PROCEDURE [dbo].[RefreshToken_RevokeAllByUser]
    @userId INT
AS
BEGIN
    SET NOCOUNT ON;
    UPDATE dbo.RefreshTokens
       SET RevokedAtUtc = SYSUTCDATETIME()
     WHERE UserId = @userId AND RevokedAtUtc IS NULL;
END
GO

CREATE OR ALTER PROCEDURE [dbo].[RefreshToken_GetReusedUser]
    @hash NVARCHAR(256)
AS
BEGIN
    SET NOCOUNT ON;
    SELECT TOP 1 UserId FROM dbo.RefreshTokens
     WHERE TokenHash = @hash AND RevokedAtUtc IS NOT NULL;
END
GO

------------------------------------------------------------------------------------------------------
-- Two-factor challenge (SMS code) — already AppId-free upstream.
------------------------------------------------------------------------------------------------------
CREATE OR ALTER PROCEDURE [dbo].[TwoFactorChallenge_Create]
    @token     NVARCHAR(64),
    @userId    INT,
    @code      NVARCHAR(10),
    @attempts  INT,
    @expiresAt DATETIME2(7)
AS
BEGIN
    SET NOCOUNT ON;
    INSERT INTO dbo.TwoFactorCodes (TwoFactorToken, UserId, Code, AttemptsRemaining, ExpiresAtUtc, IsUsed)
    VALUES (@token, @userId, @code, @attempts, @expiresAt, 0);
END
GO

CREATE OR ALTER PROCEDURE [dbo].[TwoFactorChallenge_DecrementAttempt]
    @token NVARCHAR(64)
AS
BEGIN
    SET NOCOUNT ON;
    UPDATE dbo.TwoFactorCodes
       SET AttemptsRemaining = AttemptsRemaining - 1
     WHERE TwoFactorToken    = @token
       AND IsUsed            = 0
       AND ExpiresAtUtc      > SYSUTCDATETIME()
       AND AttemptsRemaining > 0;
END
GO

CREATE OR ALTER PROCEDURE [dbo].[TwoFactorChallenge_Verify]
    @token NVARCHAR(64),
    @code  NVARCHAR(10)
AS
BEGIN
    SET NOCOUNT ON;
    UPDATE dbo.TwoFactorCodes
       SET IsUsed = 1
    OUTPUT inserted.UserId
     WHERE TwoFactorToken    = @token
       AND Code              = @code
       AND IsUsed            = 0
       AND ExpiresAtUtc      > SYSUTCDATETIME()
       AND AttemptsRemaining > 0;
END
GO

------------------------------------------------------------------------------------------------------
-- Navigation menu — permission-filtered. No AppId dimension in free mode.
------------------------------------------------------------------------------------------------------
CREATE OR ALTER PROCEDURE [dbo].[Menu_GetByUserPermissions]
    @UserId INT
AS
BEGIN
    SET NOCOUNT ON;

    -- admin.access bypass: see every active menu.
    IF EXISTS (SELECT 1 FROM dbo.UserRoles ur
               JOIN dbo.RolePermissions rp ON ur.RoleId = rp.RoleId
               JOIN dbo.Permissions p ON rp.PermissionId = p.Id
               WHERE ur.UserId = @UserId AND p.Name = 'admin.access')
    BEGIN
        SELECT Id, Name, Description, Icon, Url, ParentId, [Order], IsActive, IsVisible, PermissionId,
               CreatedAtUtc, UpdatedAtUtc, CreatedBy, UpdatedBy
          FROM dbo.Menus
         WHERE IsActive = 1
         ORDER BY [Order], Name;
        RETURN;
    END

    SELECT m.Id, m.Name, m.Description, m.Icon, m.Url, m.ParentId, m.[Order], m.IsActive, m.IsVisible,
           m.PermissionId, m.CreatedAtUtc, m.UpdatedAtUtc, m.CreatedBy, m.UpdatedBy
      FROM dbo.Menus m
      OUTER APPLY (
            SELECT TOP 1 ump.CanView
              FROM dbo.UserMenuPermissions ump
             WHERE ump.UserId = @UserId AND ump.MenuId = m.Id
               AND (ump.ExpiresAt IS NULL OR ump.ExpiresAt > SYSUTCDATETIME())
             ORDER BY ump.Id DESC
      ) ov
     WHERE m.IsActive = 1
       AND (
            CASE
                WHEN ov.CanView IS NOT NULL THEN ov.CanView                  -- override wins (grant/deny)
                WHEN m.PermissionId IS NULL THEN 1                            -- unguarded menu
                WHEN EXISTS (SELECT 1 FROM dbo.UserRoles ur
                              JOIN dbo.RolePermissions rp ON ur.RoleId = rp.RoleId
                             WHERE ur.UserId = @UserId AND rp.PermissionId = m.PermissionId) THEN 1
                ELSE 0
            END = 1
       )
     ORDER BY m.[Order], m.Name;
END
GO

------------------------------------------------------------------------------------------------------
-- Localization — read the culture map + upsert a value. Single-tenant (no AppId, no TenantId).
------------------------------------------------------------------------------------------------------
CREATE OR ALTER PROCEDURE [dbo].[Localization_GetMapForApp]
    @culture NVARCHAR(20)
AS
BEGIN
    SET NOCOUNT ON;
    SELECT [Key], [Value]
      FROM dbo.LocalizationResource
     WHERE Culture = @culture;
END
GO

CREATE OR ALTER PROCEDURE [dbo].[LocalizationResource_UpsertValue]
    @key       NVARCHAR(200),
    @category  NVARCHAR(100),
    @culture   NVARCHAR(20),
    @value     NVARCHAR(MAX),
    @updatedBy NVARCHAR(256) = NULL
AS
BEGIN
    SET NOCOUNT ON;
    MERGE dbo.LocalizationResource AS t
    USING (SELECT @key AS [Key], @culture AS Culture) AS s
       ON t.[Key] = s.[Key] AND t.Culture = s.Culture
    WHEN MATCHED THEN
        UPDATE SET Category = @category, [Value] = @value,
                   UpdatedAtUtc = SYSUTCDATETIME(), UpdatedBy = @updatedBy
    WHEN NOT MATCHED THEN
        INSERT ([Key], Category, Culture, [Value], UpdatedBy)
        VALUES (@key, @category, @culture, @value, @updatedBy);
END
GO

------------------------------------------------------------------------------------------------------
-- Runtime configuration — all active rows. Single-tenant (no AppId). The free schema keeps only
-- UpdatedAtUtc, so it is projected AS UpdatedAt to preserve the commercial output shape.
------------------------------------------------------------------------------------------------------
CREATE OR ALTER PROCEDURE [dbo].[AppConfiguration_GetAll]
AS
BEGIN
    SET NOCOUNT ON;
    SELECT Id, [Key], Value, IsActive, IsEncrypted, Category, Description,
           CreatedAt, UpdatedAtUtc AS UpdatedAt, CreatedBy, UpdatedBy
      FROM dbo.AppConfigurations
     ORDER BY [Key];
END
GO
