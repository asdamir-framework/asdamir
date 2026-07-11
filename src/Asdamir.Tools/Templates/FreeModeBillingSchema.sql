-- Free-mode (Model B) billing schema — the app's OWN single-tenant subscription/billing tables.
--
-- FREE MODE ONLY. A free-tier app is SELF-CONTAINED: it does NOT reach a central AsdamirVault via
-- AppManagement, so its billing model lives in the app's own database, single-tenant ("the app IS the
-- tenant"). There is therefore NO AppId column and NO TenantId column anywhere here — the whole database
-- belongs to one app. (Commercial mode keeps AsdamirVault's AppId-scoped billing — AsdamirVault_105/114 —
-- and this file is NOT emitted there.)
--
-- Derived from the canonical AsdamirVault_105 billing schema (consolidated through 114) with the multi-app
-- dimension removed: AppId dropped from BillingAccounts/Subscriptions/Invoices, TenantId dropped from
-- BillingAccounts, and the provider columns folded to Paddle (PaddlePriceId / PaddleCustomerRef) as in 107.
-- PaymentMethods stores TOKENS ONLY (PCI: card data lives at the provider). Webhooks are deduplicated by
-- (Provider, ProviderEventId) UNIQUE so a replayed event can never double-charge / double-provision.
--
-- Stored procedures (the operational read+write logic the app-local LocalDbBillingStore calls) are in the
-- companion FreeModeBillingProcs.sql. Money = DECIMAL(19,4); Currency = CHAR(3) (ISO-4217).
-- Idempotent: every object is guarded (IF OBJECT_ID / IF NOT EXISTS / MERGE), so re-applying via the
-- journaled `db apply` runner is a no-op.
SET ANSI_NULLS ON;
GO
SET QUOTED_IDENTIFIER ON;
GO

------------------------------------------------------------------------------------------------------
-- 1. Plans — the subscription catalog (global; single-tenant, no AppId).
------------------------------------------------------------------------------------------------------
IF OBJECT_ID(N'[dbo].[Plans]', N'U') IS NULL
BEGIN
    CREATE TABLE [dbo].[Plans]
    (
        [Id]            UNIQUEIDENTIFIER NOT NULL CONSTRAINT [DF_Plans_Id] DEFAULT (NEWID()),
        [Code]          NVARCHAR(50)     NOT NULL,   -- 'free' | 'pro' | 'business'
        [Name]          NVARCHAR(100)    NOT NULL,
        [Price]         DECIMAL(19,4)    NOT NULL CONSTRAINT [DF_Plans_Price] DEFAULT (0),
        [Currency]      CHAR(3)          NOT NULL CONSTRAINT [DF_Plans_Currency] DEFAULT ('TRY'),
        [Interval]      NVARCHAR(10)     NOT NULL CONSTRAINT [DF_Plans_Interval] DEFAULT ('month'), -- month | year
        [TrialDays]     INT              NOT NULL CONSTRAINT [DF_Plans_TrialDays] DEFAULT (0),
        [FeaturesJson]  NVARCHAR(MAX)    NULL,       -- {"maxUsers":5,"features":[...]}
        [PaddlePriceId] NVARCHAR(200)    NULL,       -- Paddle price id (global Merchant-of-Record)
        [IsActive]      BIT              NOT NULL CONSTRAINT [DF_Plans_IsActive] DEFAULT (1),
        [SortOrder]     INT              NOT NULL CONSTRAINT [DF_Plans_SortOrder] DEFAULT (0),
        [CreatedAtUtc]  DATETIME2(3)     NOT NULL CONSTRAINT [DF_Plans_CreatedAtUtc] DEFAULT (SYSUTCDATETIME()),
        [UpdatedAtUtc]  DATETIME2(3)     NULL,
        CONSTRAINT [PK_Plans] PRIMARY KEY CLUSTERED ([Id]),
        CONSTRAINT [UQ_Plans_Code] UNIQUE ([Code])
    );
END
GO

------------------------------------------------------------------------------------------------------
-- 2. BillingAccounts — the subscriber. Single-tenant: NO AppId, NO TenantId.
------------------------------------------------------------------------------------------------------
IF OBJECT_ID(N'[dbo].[BillingAccounts]', N'U') IS NULL
BEGIN
    CREATE TABLE [dbo].[BillingAccounts]
    (
        [Id]                UNIQUEIDENTIFIER NOT NULL CONSTRAINT [DF_BillingAccounts_Id] DEFAULT (NEWID()),
        [Name]              NVARCHAR(200)    NOT NULL,
        [Email]             NVARCHAR(256)    NOT NULL,   -- billing contact
        [OwnerUserId]       INT              NULL,       -- FK → dbo.Users(Id); the account owner
        [PaddleCustomerRef] NVARCHAR(200)    NULL,       -- Paddle customer id
        [IsActive]          BIT              NOT NULL CONSTRAINT [DF_BillingAccounts_IsActive] DEFAULT (1),
        [CreatedAtUtc]      DATETIME2(3)     NOT NULL CONSTRAINT [DF_BillingAccounts_CreatedAtUtc] DEFAULT (SYSUTCDATETIME()),
        [UpdatedAtUtc]      DATETIME2(3)     NULL,
        CONSTRAINT [PK_BillingAccounts] PRIMARY KEY CLUSTERED ([Id])
    );
END
GO

-- FK to Users only if the free-mode management schema's dbo.Users exists and the FK isn't already there.
IF OBJECT_ID(N'[dbo].[Users]', N'U') IS NOT NULL
   AND NOT EXISTS (SELECT 1 FROM sys.foreign_keys WHERE name = N'FK_BillingAccounts_Users')
BEGIN
    ALTER TABLE [dbo].[BillingAccounts]
        ADD CONSTRAINT [FK_BillingAccounts_Users]
        FOREIGN KEY ([OwnerUserId]) REFERENCES [dbo].[Users]([Id]);
END
GO

IF NOT EXISTS (SELECT 1 FROM sys.indexes WHERE name = N'IX_BillingAccounts_OwnerUserId' AND object_id = OBJECT_ID(N'[dbo].[BillingAccounts]'))
    CREATE INDEX [IX_BillingAccounts_OwnerUserId] ON [dbo].[BillingAccounts]([OwnerUserId]) WHERE [OwnerUserId] IS NOT NULL;
GO

------------------------------------------------------------------------------------------------------
-- 3. Subscriptions — an account's active plan. Single-tenant: NO AppId.
------------------------------------------------------------------------------------------------------
IF OBJECT_ID(N'[dbo].[Subscriptions]', N'U') IS NULL
BEGIN
    CREATE TABLE [dbo].[Subscriptions]
    (
        [Id]                     UNIQUEIDENTIFIER NOT NULL CONSTRAINT [DF_Subscriptions_Id] DEFAULT (NEWID()),
        [BillingAccountId]       UNIQUEIDENTIFIER NOT NULL,
        [PlanId]                 UNIQUEIDENTIFIER NOT NULL,
        [Status]                 NVARCHAR(20)     NOT NULL CONSTRAINT [DF_Subscriptions_Status] DEFAULT ('trialing'),
                                 -- trialing | active | past_due | canceled | incomplete
        [Provider]               NVARCHAR(20)     NULL,   -- paddle | crypto (null until first paid conversion)
        [ProviderSubscriptionId] NVARCHAR(200)    NULL,
        [CurrentPeriodStartUtc]  DATETIME2(3)     NULL,
        [CurrentPeriodEndUtc]    DATETIME2(3)     NULL,
        [TrialEndsAtUtc]         DATETIME2(3)     NULL,
        [CancelAtPeriodEnd]      BIT              NOT NULL CONSTRAINT [DF_Subscriptions_CancelAtPeriodEnd] DEFAULT (0),
        [CanceledAtUtc]          DATETIME2(3)     NULL,
        [CreatedAtUtc]           DATETIME2(3)     NOT NULL CONSTRAINT [DF_Subscriptions_CreatedAtUtc] DEFAULT (SYSUTCDATETIME()),
        [UpdatedAtUtc]           DATETIME2(3)     NULL,
        CONSTRAINT [PK_Subscriptions] PRIMARY KEY CLUSTERED ([Id]),
        CONSTRAINT [FK_Subscriptions_BillingAccounts] FOREIGN KEY ([BillingAccountId]) REFERENCES [dbo].[BillingAccounts]([Id]),
        CONSTRAINT [FK_Subscriptions_Plans]           FOREIGN KEY ([PlanId])           REFERENCES [dbo].[Plans]([Id])
    );
END
GO

-- One ACTIVE-ish subscription per account (trialing/active/past_due). Filtered unique index.
IF NOT EXISTS (SELECT 1 FROM sys.indexes WHERE name = N'UX_Subscriptions_ActivePerAccount' AND object_id = OBJECT_ID(N'[dbo].[Subscriptions]'))
    CREATE UNIQUE INDEX [UX_Subscriptions_ActivePerAccount]
        ON [dbo].[Subscriptions]([BillingAccountId])
        WHERE [Status] IN ('trialing','active','past_due');
GO

------------------------------------------------------------------------------------------------------
-- 4. Invoices — single-tenant: NO AppId.
------------------------------------------------------------------------------------------------------
IF OBJECT_ID(N'[dbo].[Invoices]', N'U') IS NULL
BEGIN
    CREATE TABLE [dbo].[Invoices]
    (
        [Id]                UNIQUEIDENTIFIER NOT NULL CONSTRAINT [DF_Invoices_Id] DEFAULT (NEWID()),
        [BillingAccountId]  UNIQUEIDENTIFIER NOT NULL,
        [SubscriptionId]    UNIQUEIDENTIFIER NULL,
        [Number]            NVARCHAR(40)     NULL,       -- human invoice number (assigned on issue)
        [Amount]            DECIMAL(19,4)    NOT NULL,
        [Currency]          CHAR(3)          NOT NULL CONSTRAINT [DF_Invoices_Currency] DEFAULT ('TRY'),
        [Status]            NVARCHAR(20)     NOT NULL CONSTRAINT [DF_Invoices_Status] DEFAULT ('open'),
                            -- open | paid | void | uncollectible
        [DueAtUtc]          DATETIME2(3)     NULL,
        [PaidAtUtc]         DATETIME2(3)     NULL,
        [ProviderInvoiceId] NVARCHAR(200)    NULL,
        [PdfPath]           NVARCHAR(400)    NULL,
        [CreatedAtUtc]      DATETIME2(3)     NOT NULL CONSTRAINT [DF_Invoices_CreatedAtUtc] DEFAULT (SYSUTCDATETIME()),
        CONSTRAINT [PK_Invoices] PRIMARY KEY CLUSTERED ([Id]),
        CONSTRAINT [FK_Invoices_BillingAccounts] FOREIGN KEY ([BillingAccountId]) REFERENCES [dbo].[BillingAccounts]([Id]),
        CONSTRAINT [FK_Invoices_Subscriptions]   FOREIGN KEY ([SubscriptionId])   REFERENCES [dbo].[Subscriptions]([Id])
    );
END
GO

IF NOT EXISTS (SELECT 1 FROM sys.indexes WHERE name = N'IX_Invoices_Account' AND object_id = OBJECT_ID(N'[dbo].[Invoices]'))
    CREATE INDEX [IX_Invoices_Account] ON [dbo].[Invoices]([BillingAccountId], [CreatedAtUtc] DESC);
GO

------------------------------------------------------------------------------------------------------
-- 5. PaymentMethods — TOKENS ONLY (no raw card data; PCI). Has no AppId in any model.
------------------------------------------------------------------------------------------------------
IF OBJECT_ID(N'[dbo].[PaymentMethods]', N'U') IS NULL
BEGIN
    CREATE TABLE [dbo].[PaymentMethods]
    (
        [Id]                UNIQUEIDENTIFIER NOT NULL CONSTRAINT [DF_PaymentMethods_Id] DEFAULT (NEWID()),
        [BillingAccountId]  UNIQUEIDENTIFIER NOT NULL,
        [Provider]          NVARCHAR(20)     NOT NULL,   -- paddle | crypto
        [ProviderMethodRef] NVARCHAR(200)    NOT NULL,   -- card token / payment_method id at the provider
        [Brand]             NVARCHAR(30)     NULL,        -- visa | mastercard | troy | ...
        [Last4]             CHAR(4)          NULL,
        [ExpMonth]          TINYINT          NULL,
        [ExpYear]           SMALLINT         NULL,
        [IsDefault]         BIT              NOT NULL CONSTRAINT [DF_PaymentMethods_IsDefault] DEFAULT (0),
        [CreatedAtUtc]      DATETIME2(3)     NOT NULL CONSTRAINT [DF_PaymentMethods_CreatedAtUtc] DEFAULT (SYSUTCDATETIME()),
        CONSTRAINT [PK_PaymentMethods] PRIMARY KEY CLUSTERED ([Id]),
        CONSTRAINT [FK_PaymentMethods_BillingAccounts] FOREIGN KEY ([BillingAccountId]) REFERENCES [dbo].[BillingAccounts]([Id])
    );
END
GO

IF NOT EXISTS (SELECT 1 FROM sys.indexes WHERE name = N'IX_PaymentMethods_Account' AND object_id = OBJECT_ID(N'[dbo].[PaymentMethods]'))
    CREATE INDEX [IX_PaymentMethods_Account] ON [dbo].[PaymentMethods]([BillingAccountId]);
GO

------------------------------------------------------------------------------------------------------
-- 6. BillingWebhookEvents — idempotency journal for provider webhooks.
------------------------------------------------------------------------------------------------------
IF OBJECT_ID(N'[dbo].[BillingWebhookEvents]', N'U') IS NULL
BEGIN
    CREATE TABLE [dbo].[BillingWebhookEvents]
    (
        [Id]              BIGINT        IDENTITY(1,1) NOT NULL,
        [Provider]        NVARCHAR(20)  NOT NULL,     -- paddle | crypto
        [ProviderEventId] NVARCHAR(200) NOT NULL,     -- the provider's event id (dedup key)
        [Type]            NVARCHAR(100) NULL,
        [Payload]         NVARCHAR(MAX) NULL,
        [ReceivedAtUtc]   DATETIME2(3)  NOT NULL CONSTRAINT [DF_BillingWebhookEvents_ReceivedAtUtc] DEFAULT (SYSUTCDATETIME()),
        [ProcessedAtUtc]  DATETIME2(3)  NULL,
        CONSTRAINT [PK_BillingWebhookEvents] PRIMARY KEY CLUSTERED ([Id]),
        CONSTRAINT [UQ_BillingWebhookEvents_ProviderEvent] UNIQUE ([Provider], [ProviderEventId])
    );
END
GO

------------------------------------------------------------------------------------------------------
-- 7. Plan seed — Free / Pro / Business (PaddlePriceId is a placeholder, filled once the Paddle products
--    exist). MERGE on Code → idempotent, re-appliable.
------------------------------------------------------------------------------------------------------
MERGE dbo.Plans AS t
USING (VALUES
    ('free',     N'Free',     0.00,    'TRY', 'month',  0, N'{"maxUsers":2,"features":["core"]}',                                     10),
    ('pro',      N'Pro',      499.00,  'TRY', 'month', 14, N'{"maxUsers":10,"features":["core","export","2fa"]}',                     20),
    ('business', N'Business', 1499.00, 'TRY', 'month', 14, N'{"maxUsers":50,"features":["core","export","2fa","webhooks","priority_support"]}', 30)
) AS s ([Code],[Name],[Price],[Currency],[Interval],[TrialDays],[FeaturesJson],[SortOrder])
   ON t.[Code] = s.[Code]
WHEN MATCHED THEN
    UPDATE SET [Name] = s.[Name], [Price] = s.[Price], [Currency] = s.[Currency],
               [Interval] = s.[Interval], [TrialDays] = s.[TrialDays],
               [FeaturesJson] = s.[FeaturesJson], [SortOrder] = s.[SortOrder],
               [IsActive] = 1, [UpdatedAtUtc] = SYSUTCDATETIME()
WHEN NOT MATCHED THEN
    INSERT ([Code],[Name],[Price],[Currency],[Interval],[TrialDays],[FeaturesJson],[SortOrder])
    VALUES (s.[Code],s.[Name],s.[Price],s.[Currency],s.[Interval],s.[TrialDays],s.[FeaturesJson],s.[SortOrder]);
GO

PRINT '[OK] FreeModeBillingSchema: single-tenant Plans/BillingAccounts/Subscriptions/Invoices/PaymentMethods/BillingWebhookEvents (NO AppId) + Free/Pro/Business seed.';
GO
