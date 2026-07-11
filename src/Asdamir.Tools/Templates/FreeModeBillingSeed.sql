-- Free-mode (Model B) billing seed — the permission / nav-menu / localization the Payment page + nav need.
--
-- FREE MODE ONLY. Single-tenant counterpart of the commercial `seed_billing.sql` (which seeds AppId-scoped
-- rows into AsdamirVault): this targets THIS app's OWN management tables (created by the free-mode management
-- schema) — NO AppId, NO dbo.Apps. Emitted as a journaled migration, so `asdamir db apply` runs it
-- automatically alongside the billing schema/procs. Idempotent (MERGE / IF NOT EXISTS). The Paddle secrets
-- are NOT seeded here: a free app reads Payment:Paddle:* from its OWN configuration (user-secrets / env or its
-- dbo.AppConfigurations) — set them there, then the Payment page's checkout goes live.
SET ANSI_NULLS ON;
GO
SET QUOTED_IDENTIFIER ON;
GO

-------------------------------------------------------------------------------
-- 1) Permission + Admin grant + nav menu row (Url = /billing). Single-tenant → no AppId scoping.
IF NOT EXISTS (SELECT 1 FROM dbo.Permissions WHERE Name = N'billing.view')
    INSERT INTO dbo.Permissions (Name, Description) VALUES (N'billing.view', N'View the billing/payment page');
DECLARE @PermId INT = (SELECT Id FROM dbo.Permissions WHERE Name = N'billing.view');

IF NOT EXISTS (SELECT 1 FROM dbo.Roles WHERE Name = N'Admin')
    INSERT INTO dbo.Roles (Name, Description) VALUES (N'Admin', N'Full administrative access');
DECLARE @RoleId INT = (SELECT Id FROM dbo.Roles WHERE Name = N'Admin');
IF NOT EXISTS (SELECT 1 FROM dbo.RolePermissions WHERE RoleId = @RoleId AND PermissionId = @PermId)
    INSERT INTO dbo.RolePermissions (RoleId, PermissionId) VALUES (@RoleId, @PermId);

IF NOT EXISTS (SELECT 1 FROM dbo.Menus WHERE Url = N'/billing')
    INSERT INTO dbo.Menus (Name, Description, Icon, Url, [Order], IsActive, IsVisible, PermissionId, CreatedBy)
    VALUES (N'Billing', N'Billing / payment page', N'payment', N'/billing',
            (SELECT ISNULL(MAX([Order]), 0) + 1 FROM dbo.Menus),
            1, 1, @PermId, N'seed');

-------------------------------------------------------------------------------
-- 2) Localization (Menu.Billing + Billing.Page.*; tr-TR / en-US / ru-RU) via the single-tenant proc (no @appId).
DECLARE @Seed TABLE ([Key] NVARCHAR(200), [Culture] NVARCHAR(20), [Value] NVARCHAR(MAX));
INSERT INTO @Seed ([Key],[Culture],[Value]) VALUES
    (N'Menu.Billing',                 N'tr-TR', N'Faturalama'),
    (N'Menu.Billing',                 N'en-US', N'Billing'),
    (N'Menu.Billing',                 N'ru-RU', N'Оплата'),
    (N'Billing.Page.Title',           N'tr-TR', N'Faturalama'),
    (N'Billing.Page.Title',           N'en-US', N'Billing'),
    (N'Billing.Page.Title',           N'ru-RU', N'Оплата'),
    (N'Billing.Page.CurrentPlan',     N'tr-TR', N'Mevcut plan'),
    (N'Billing.Page.CurrentPlan',     N'en-US', N'Current plan'),
    (N'Billing.Page.CurrentPlan',     N'ru-RU', N'Текущий тариф'),
    (N'Billing.Page.Cancel',          N'tr-TR', N'Aboneliği iptal et'),
    (N'Billing.Page.Cancel',          N'en-US', N'Cancel subscription'),
    (N'Billing.Page.Cancel',          N'ru-RU', N'Отменить подписку'),
    (N'Billing.Page.NoPlans',         N'tr-TR', N'Tanımlı plan yok.'),
    (N'Billing.Page.NoPlans',         N'en-US', N'No plans available.'),
    (N'Billing.Page.NoPlans',         N'ru-RU', N'Нет доступных тарифов.'),
    (N'Billing.Page.Trial',           N'tr-TR', N'{0} gün deneme'),
    (N'Billing.Page.Trial',           N'en-US', N'{0}-day trial'),
    (N'Billing.Page.Trial',           N'ru-RU', N'Пробный период {0} дн.'),
    (N'Billing.Page.Current',         N'tr-TR', N'Mevcut'),
    (N'Billing.Page.Current',         N'en-US', N'Current'),
    (N'Billing.Page.Current',         N'ru-RU', N'Текущий'),
    (N'Billing.Page.Choose',          N'tr-TR', N'Seç'),
    (N'Billing.Page.Choose',          N'en-US', N'Choose'),
    (N'Billing.Page.Choose',          N'ru-RU', N'Выбрать'),
    (N'Billing.Page.Checkout',        N'tr-TR', N'Öde'),
    (N'Billing.Page.Checkout',        N'en-US', N'Checkout'),
    (N'Billing.Page.Checkout',        N'ru-RU', N'Оплатить'),
    (N'Billing.Page.CheckoutStarted', N'tr-TR', N'Ödeme oturumu oluşturuldu.'),
    (N'Billing.Page.CheckoutStarted', N'en-US', N'Checkout session created.'),
    (N'Billing.Page.CheckoutStarted', N'ru-RU', N'Сессия оплаты создана.'),
    (N'Billing.Page.LoadError',       N'tr-TR', N'Faturalama bilgileri yüklenemedi.'),
    (N'Billing.Page.LoadError',       N'en-US', N'Failed to load billing.'),
    (N'Billing.Page.LoadError',       N'ru-RU', N'Не удалось загрузить данные оплаты.');

DECLARE @Key NVARCHAR(200), @Culture NVARCHAR(20), @Value NVARCHAR(MAX);
DECLARE seed_cur CURSOR LOCAL FAST_FORWARD FOR SELECT [Key],[Culture],[Value] FROM @Seed;
OPEN seed_cur;
FETCH NEXT FROM seed_cur INTO @Key, @Culture, @Value;
WHILE @@FETCH_STATUS = 0
BEGIN
    EXEC dbo.LocalizationResource_UpsertValue
        @key = @Key, @category = N'AppShell', @culture = @Culture, @value = @Value, @updatedBy = N'onboarding';
    FETCH NEXT FROM seed_cur INTO @Key, @Culture, @Value;
END
CLOSE seed_cur; DEALLOCATE seed_cur;

PRINT '[OK] FreeModeBillingSeed: billing.view + /billing menu + Menu.Billing + Billing.Page.* localization (single-tenant).';
GO
