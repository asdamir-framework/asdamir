/*
 * Generated app — OWN database seed (demo data only).
 *
 * No management/reference data here (no AppConfigurations, no LocalizationResource,
 * no users/roles/permissions) — that all lives in AsdamirVault, managed from AppManagement.
 * Just a couple of demo rows so a freshly-created app shows something. Idempotent.
 */
SET ANSI_NULLS ON;
GO
SET QUOTED_IDENTIFIER ON;
GO

IF NOT EXISTS (SELECT 1 FROM dbo.DemoItems)
    INSERT INTO dbo.DemoItems ([Name], [IsActive])
    VALUES (N'Demo item 1', 1), (N'Demo item 2', 1);
GO
