-- Free-mode (Model B) billing stored procedures — the app-local operational read+write logic the
-- LocalDbBillingStore (Asdamir.Payments) calls, over the single-tenant tables in the companion
-- FreeModeBillingSchema.sql.
--
-- FREE MODE ONLY. Adapted from the canonical AsdamirVault billing procs (105 consolidated through 114)
-- with the multi-app dimension removed: every @appId parameter and every AppId column/projection is gone,
-- because a free app's database belongs to exactly one app ("the app IS the tenant"). The proc NAMES and
-- PARAMETER shapes otherwise match the control-plane store exactly, so one C# store implementation
-- (LocalDbBillingStore) binds against these AppId-free procs.
--
-- DELIBERATELY ABSENT: the operator cross-tenant *_ListAll procs (Subscription_ListAll / Invoice_ListAll /
-- PaymentMethod_ListAll) — free mode has no operator console; those belong to AppManagement (IBillingAdminStore).
--
-- NOCOUNT / @@ROWCOUNT rule: every proc SET NOCOUNT ON; action procs SELECT @@ROWCOUNT so the store reads
-- affected rows as a scalar (never ExecuteAsync()>0). Idempotent: CREATE OR ALTER is re-appliable, and the
-- journaled `db apply` runner skips an already-applied migration anyway.
SET ANSI_NULLS ON;
GO
SET QUOTED_IDENTIFIER ON;
GO

------------------------------------------------------------------------------------------------------
-- Plans (catalog reads)
------------------------------------------------------------------------------------------------------
CREATE OR ALTER PROCEDURE [dbo].[Plan_List]
AS
BEGIN
    SET NOCOUNT ON;
    SELECT Id, Code, Name, Price, Currency, [Interval], TrialDays, FeaturesJson,
           PaddlePriceId, IsActive, SortOrder
      FROM dbo.Plans
     WHERE IsActive = 1
     ORDER BY SortOrder, Price;
END
GO

CREATE OR ALTER PROCEDURE [dbo].[Plan_GetByCode]
    @code NVARCHAR(50)
AS
BEGIN
    SET NOCOUNT ON;
    SELECT TOP 1 Id, Code, Name, Price, Currency, [Interval], TrialDays, FeaturesJson,
                 PaddlePriceId, IsActive, SortOrder
      FROM dbo.Plans
     WHERE Code = @code;
END
GO

------------------------------------------------------------------------------------------------------
-- BillingAccounts
------------------------------------------------------------------------------------------------------
-- Single-tenant: NO @appId parameter, NO AppId column (the free-mode store passes only name/email/ownerUserId).
CREATE OR ALTER PROCEDURE [dbo].[BillingAccount_Create]
    @name        NVARCHAR(200),
    @email       NVARCHAR(256),
    @ownerUserId INT = NULL
AS
BEGIN
    SET NOCOUNT ON;
    DECLARE @id UNIQUEIDENTIFIER = NEWID();
    INSERT INTO dbo.BillingAccounts (Id, Name, Email, OwnerUserId)
    VALUES (@id, @name, @email, @ownerUserId);
    SELECT @id;   -- return the new id
END
GO

CREATE OR ALTER PROCEDURE [dbo].[BillingAccount_GetByOwner]
    @ownerUserId INT
AS
BEGIN
    SET NOCOUNT ON;
    SELECT TOP 1 Id, Name, Email, OwnerUserId, PaddleCustomerRef, IsActive
      FROM dbo.BillingAccounts
     WHERE OwnerUserId = @ownerUserId
     ORDER BY CreatedAtUtc DESC;
END
GO

CREATE OR ALTER PROCEDURE [dbo].[BillingAccount_SetProviderRefs]
    @id                UNIQUEIDENTIFIER,
    @paddleCustomerRef NVARCHAR(200) = NULL
AS
BEGIN
    SET NOCOUNT ON;
    UPDATE dbo.BillingAccounts
       SET PaddleCustomerRef = COALESCE(@paddleCustomerRef, PaddleCustomerRef),
           UpdatedAtUtc       = SYSUTCDATETIME()
     WHERE Id = @id;
    SELECT @@ROWCOUNT;
END
GO

------------------------------------------------------------------------------------------------------
-- Subscriptions
------------------------------------------------------------------------------------------------------
-- Single-tenant: NO AppId column to inherit.
CREATE OR ALTER PROCEDURE [dbo].[Subscription_Create]
    @billingAccountId UNIQUEIDENTIFIER,
    @planId           UNIQUEIDENTIFIER,
    @status           NVARCHAR(20)     = 'trialing',
    @trialEndsAtUtc   DATETIME2(3)     = NULL
AS
BEGIN
    SET NOCOUNT ON;
    DECLARE @id UNIQUEIDENTIFIER = NEWID();
    INSERT INTO dbo.Subscriptions (Id, BillingAccountId, PlanId, Status, TrialEndsAtUtc)
    VALUES (@id, @billingAccountId, @planId, @status, @trialEndsAtUtc);
    SELECT @id;
END
GO

CREATE OR ALTER PROCEDURE [dbo].[Subscription_UpdateStatus]
    @id                     UNIQUEIDENTIFIER,
    @status                 NVARCHAR(20),
    @provider               NVARCHAR(20)     = NULL,
    @providerSubscriptionId NVARCHAR(200)    = NULL,
    @currentPeriodStartUtc  DATETIME2(3)     = NULL,
    @currentPeriodEndUtc    DATETIME2(3)     = NULL
AS
BEGIN
    SET NOCOUNT ON;
    UPDATE dbo.Subscriptions
       SET Status                 = @status,
           Provider               = COALESCE(@provider, Provider),
           ProviderSubscriptionId = COALESCE(@providerSubscriptionId, ProviderSubscriptionId),
           CurrentPeriodStartUtc  = COALESCE(@currentPeriodStartUtc, CurrentPeriodStartUtc),
           CurrentPeriodEndUtc    = COALESCE(@currentPeriodEndUtc, CurrentPeriodEndUtc),
           CanceledAtUtc          = CASE WHEN @status = 'canceled' THEN SYSUTCDATETIME() ELSE CanceledAtUtc END,
           UpdatedAtUtc           = SYSUTCDATETIME()
     WHERE Id = @id;
    SELECT @@ROWCOUNT;
END
GO

CREATE OR ALTER PROCEDURE [dbo].[Subscription_GetActiveByAccount]
    @billingAccountId UNIQUEIDENTIFIER
AS
BEGIN
    SET NOCOUNT ON;
    SELECT TOP 1 s.Id, s.BillingAccountId, s.PlanId, p.Code AS PlanCode, p.Name AS PlanName,
                 p.FeaturesJson, s.Status, s.Provider, s.ProviderSubscriptionId,
                 s.CurrentPeriodStartUtc, s.CurrentPeriodEndUtc, s.TrialEndsAtUtc, s.CancelAtPeriodEnd
      FROM dbo.Subscriptions s
      JOIN dbo.Plans p ON p.Id = s.PlanId
     WHERE s.BillingAccountId = @billingAccountId
       AND s.Status IN ('trialing','active','past_due')
     ORDER BY s.CreatedAtUtc DESC;
END
GO

-- Webhook activation: find a subscription by its provider reference, and link one to it.
CREATE OR ALTER PROCEDURE [dbo].[Subscription_GetByProviderRef]
    @provider    NVARCHAR(20),
    @providerRef NVARCHAR(200)
AS
BEGIN
    SET NOCOUNT ON;
    SELECT TOP 1 s.Id, s.BillingAccountId, s.PlanId, p.Code AS PlanCode, p.Name AS PlanName,
                 p.FeaturesJson, s.Status, s.Provider, s.ProviderSubscriptionId,
                 s.CurrentPeriodStartUtc, s.CurrentPeriodEndUtc, s.TrialEndsAtUtc, s.CancelAtPeriodEnd
      FROM dbo.Subscriptions s
      JOIN dbo.Plans p ON p.Id = s.PlanId
     WHERE s.Provider = @provider AND s.ProviderSubscriptionId = @providerRef
     ORDER BY s.CreatedAtUtc DESC;
END
GO

CREATE OR ALTER PROCEDURE [dbo].[Subscription_SetProviderRef]
    @id          UNIQUEIDENTIFIER,
    @provider    NVARCHAR(20),
    @providerRef NVARCHAR(200)
AS
BEGIN
    SET NOCOUNT ON;
    UPDATE dbo.Subscriptions
       SET Provider               = @provider,
           ProviderSubscriptionId = @providerRef,
           UpdatedAtUtc           = SYSUTCDATETIME()
     WHERE Id = @id;
    SELECT @@ROWCOUNT;
END
GO

------------------------------------------------------------------------------------------------------
-- Invoices (single-account operational; NO operator ListAll)
------------------------------------------------------------------------------------------------------
CREATE OR ALTER PROCEDURE [dbo].[Invoice_Create]
    @billingAccountId UNIQUEIDENTIFIER,
    @subscriptionId   UNIQUEIDENTIFIER = NULL,
    @amount           DECIMAL(19,4),
    @currency         CHAR(3)          = 'TRY',
    @dueAtUtc         DATETIME2(3)     = NULL
AS
BEGIN
    SET NOCOUNT ON;
    DECLARE @id UNIQUEIDENTIFIER = NEWID();
    INSERT INTO dbo.Invoices (Id, BillingAccountId, SubscriptionId, Amount, Currency, [Status], DueAtUtc)
    VALUES (@id, @billingAccountId, @subscriptionId, @amount, @currency, 'open', @dueAtUtc);
    SELECT @id;
END
GO

CREATE OR ALTER PROCEDURE [dbo].[Invoice_MarkPaid]
    @id                UNIQUEIDENTIFIER,
    @providerInvoiceId NVARCHAR(200) = NULL,
    @paidAtUtc         DATETIME2(3)  = NULL
AS
BEGIN
    SET NOCOUNT ON;
    UPDATE dbo.Invoices
       SET [Status]          = 'paid',
           PaidAtUtc         = COALESCE(@paidAtUtc, SYSUTCDATETIME()),
           ProviderInvoiceId = COALESCE(@providerInvoiceId, ProviderInvoiceId)
     WHERE Id = @id AND [Status] <> 'paid';
    SELECT @@ROWCOUNT;
END
GO

CREATE OR ALTER PROCEDURE [dbo].[Invoice_ListByAccount]
    @billingAccountId UNIQUEIDENTIFIER
AS
BEGIN
    SET NOCOUNT ON;
    SELECT Id, BillingAccountId, SubscriptionId, Number, Amount, Currency, [Status],
           DueAtUtc, PaidAtUtc, ProviderInvoiceId, PdfPath, CreatedAtUtc
      FROM dbo.Invoices
     WHERE BillingAccountId = @billingAccountId
     ORDER BY CreatedAtUtc DESC;
END
GO

------------------------------------------------------------------------------------------------------
-- PaymentMethods (single-account operational; NO operator ListAll)
------------------------------------------------------------------------------------------------------
CREATE OR ALTER PROCEDURE [dbo].[PaymentMethod_Upsert]
    @billingAccountId  UNIQUEIDENTIFIER,
    @provider          NVARCHAR(20),
    @providerMethodRef NVARCHAR(200),
    @brand             NVARCHAR(30) = NULL,
    @last4             CHAR(4)      = NULL,
    @expMonth          TINYINT      = NULL,
    @expYear           SMALLINT     = NULL,
    @isDefault         BIT          = 0
AS
BEGIN
    SET NOCOUNT ON;
    -- If made default, clear the flag on the account's other methods first.
    IF @isDefault = 1
        UPDATE dbo.PaymentMethods SET IsDefault = 0 WHERE BillingAccountId = @billingAccountId;

    MERGE dbo.PaymentMethods AS t
    USING (SELECT @billingAccountId AS BillingAccountId, @provider AS Provider, @providerMethodRef AS ProviderMethodRef) AS s
       ON t.BillingAccountId = s.BillingAccountId AND t.Provider = s.Provider AND t.ProviderMethodRef = s.ProviderMethodRef
    WHEN MATCHED THEN
        UPDATE SET Brand = @brand, Last4 = @last4, ExpMonth = @expMonth, ExpYear = @expYear, IsDefault = @isDefault
    WHEN NOT MATCHED THEN
        INSERT (BillingAccountId, Provider, ProviderMethodRef, Brand, Last4, ExpMonth, ExpYear, IsDefault)
        VALUES (@billingAccountId, @provider, @providerMethodRef, @brand, @last4, @expMonth, @expYear, @isDefault);
    SELECT @@ROWCOUNT;
END
GO

CREATE OR ALTER PROCEDURE [dbo].[PaymentMethod_ListByAccount]
    @billingAccountId UNIQUEIDENTIFIER
AS
BEGIN
    SET NOCOUNT ON;
    SELECT Id, BillingAccountId, Provider, ProviderMethodRef, Brand, Last4, ExpMonth, ExpYear, IsDefault, CreatedAtUtc
      FROM dbo.PaymentMethods
     WHERE BillingAccountId = @billingAccountId
     ORDER BY IsDefault DESC, CreatedAtUtc DESC;
END
GO

------------------------------------------------------------------------------------------------------
-- Webhook idempotency journal
------------------------------------------------------------------------------------------------------
-- Idempotent record: inserts the event iff (Provider, ProviderEventId) is new.
-- Returns 1 when newly recorded (caller should process), 0 when it's a duplicate (caller must SKIP).
CREATE OR ALTER PROCEDURE [dbo].[BillingWebhookEvent_TryRecord]
    @provider        NVARCHAR(20),
    @providerEventId NVARCHAR(200),
    @type            NVARCHAR(100) = NULL,
    @payload         NVARCHAR(MAX) = NULL
AS
BEGIN
    SET NOCOUNT ON;
    IF EXISTS (SELECT 1 FROM dbo.BillingWebhookEvents WHERE Provider = @provider AND ProviderEventId = @providerEventId)
    BEGIN
        SELECT 0;   -- duplicate → skip
        RETURN;
    END
    INSERT INTO dbo.BillingWebhookEvents (Provider, ProviderEventId, [Type], Payload)
    VALUES (@provider, @providerEventId, @type, @payload);
    SELECT 1;       -- newly recorded → process
END
GO

CREATE OR ALTER PROCEDURE [dbo].[BillingWebhookEvent_MarkProcessed]
    @provider        NVARCHAR(20),
    @providerEventId NVARCHAR(200)
AS
BEGIN
    SET NOCOUNT ON;
    UPDATE dbo.BillingWebhookEvents
       SET ProcessedAtUtc = SYSUTCDATETIME()
     WHERE Provider = @provider AND ProviderEventId = @providerEventId;
    SELECT @@ROWCOUNT;
END
GO

PRINT '[OK] FreeModeBillingProcs: single-tenant billing procs (Plan/BillingAccount/Subscription/Invoice/PaymentMethod/Webhook, NO AppId, NO operator ListAll).';
GO
