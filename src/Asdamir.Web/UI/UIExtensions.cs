// Copyright (C) 2026 Orhan Özşahin — Asdamir.
// Licensed under the GNU Lesser General Public License v3.0. See LICENSE.
// SPDX-License-Identifier: LGPL-3.0-or-later
//
// This file is part of the Asdamir open core. It is free software: you can redistribute it
// and/or modify it under the terms of the GNU Lesser General Public License as published by the
// Free Software Foundation, either version 3 of the License, or (at your option) any later
// version. It is distributed WITHOUT ANY WARRANTY; without even the implied warranty of
// MERCHANTABILITY or FITNESS FOR A PARTICULAR PURPOSE. See the GNU LGPL for more details.

using Microsoft.Extensions.DependencyInjection;

namespace Asdamir.Web.UI;

/// <summary>
/// Dependency injection extensions for Asdamir.Web.UI components and services
/// </summary>
public static class UIExtensions
{
    /// <summary>
    /// Register FluentUI components
    /// Keep this name to avoid breaking callers, but note: Microsoft.Fast also provides
    /// an AddFluentUIComponents extension. Prefer calling AddFluentExtras below to ensure
    /// our app-specific services are registered.
    /// </summary>
    public static IServiceCollection AddFluentUIComponents(this IServiceCollection services)
    {
        return services;
    }

    /// <summary>
    /// Register Asdamir.Web.UI services including enterprise notification system
    /// 
    /// Services registered:
    /// - INotificationService: ⭐ Enterprise notification service with localization (PREFERRED)
    /// - IGlobalSearchService: ⭐ Global search across all modules
    /// - IExportService: Multi-format export (Excel, PDF, CSV)
    /// - IDialogService: Modal dialogs and confirmations
    /// - IThemeService: Theme management (dark/light mode)
    /// - IErrorMessageService: Error message handling
    /// - IDataService: Data operations
    /// 
    /// Usage in Razor components:
    /// @inject Asdamir.Web.UI.Services.INotificationService Notifications
    /// @inject Asdamir.Web.UI.Services.IGlobalSearchService GlobalSearch
    /// @inject Asdamir.Web.UI.Services.IExportService ExportService
    /// @inject Asdamir.Web.UI.Services.IDialogService Dialogs
    /// @inject Asdamir.Web.UI.Services.IThemeService Theme
    /// 
    /// Documentation: See NOTIFICATION_STANDARD.md
    /// </summary>
    public static IServiceCollection AddUIServices(this IServiceCollection services)
    {
        // Error message service
        services.AddScoped<Services.IErrorMessageService, Services.ErrorMessageService>();
        
        // ⭐ Enterprise notification service (unified - supports both simple and localized messages)
        services.AddScoped<Services.INotificationService, Services.NotificationService>();
        
        // ⭐ Global search service - Enterprise-wide search across all modules
        services.AddScoped<Services.IGlobalSearchService, Services.GlobalSearchService>();
        
        // Export service - Multi-format export (Excel with ClosedXML, PDF with QuestPDF, CSV)
        services.AddScoped<Services.IExportService, Services.ExportService>();
        
        // Theme service - Dark/light mode management
        services.AddScoped<Services.IThemeService, Services.ThemeService>();
        
        // HTTP client for API calls
        services.AddHttpClient();
        
        // Admin services
        services.AddScoped<Asdamir.Web.UI.Services.IDataService, Asdamir.Web.UI.Services.DataService>();
        
        return services;
    }

    /// <summary>
    /// Register chart components
    /// </summary>
    public static IServiceCollection AddUICharts(this IServiceCollection services)
    {
        // Chart components configuration can be added here in the future
        return services;
    }

    /// <summary>
    /// Register all Asdamir.Web.UI components and services
    /// </summary>
    public static IServiceCollection AddFluentExtras(this IServiceCollection services)
    {
        services.AddFluentUIComponents();
        services.AddUIServices();
        services.AddUICharts();
        return services;
    }
}
