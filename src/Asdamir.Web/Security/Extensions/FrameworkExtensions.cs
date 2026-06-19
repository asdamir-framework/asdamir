// Copyright (C) 2026 Orhan Özşahin — Asdamir.
// Licensed under the GNU Lesser General Public License v3.0. See LICENSE.
// SPDX-License-Identifier: LGPL-3.0-or-later
//
// This file is part of the Asdamir open core. It is free software: you can redistribute it
// and/or modify it under the terms of the GNU Lesser General Public License as published by the
// Free Software Foundation, either version 3 of the License, or (at your option) any later
// version. It is distributed WITHOUT ANY WARRANTY; without even the implied warranty of
// MERCHANTABILITY or FITNESS FOR A PARTICULAR PURPOSE. See the GNU LGPL for more details.

using Microsoft.AspNetCore.Builder;
using Microsoft.Extensions.DependencyInjection;
using Asdamir.Core.MultiTenancy;

namespace Asdamir.Web.Security.Extensions;

/// <summary>
/// Framework extensions for easy integration into Business API or Server applications.
/// Provides single-method registration for all Framework services.
/// </summary>
public static class FrameworkExtensions
{
    /// <summary>
    /// Registers all Framework services including:
    /// - Gateway (Repositories, Services, AuditLog, Localization, User/Role/Permission)
    /// - Multi-Tenancy support
    /// - HttpContextAccessor
    /// - Memory Cache (required by LocalizationService)
    /// 
    /// Usage in Business API:
    /// builder.Services.AddFramework();
    /// </summary>
    public static IServiceCollection AddFramework(this IServiceCollection services)
    {
        // Register Memory Cache (required by LocalizationService and other framework services)
        services.AddMemoryCache();
        
        // NOTE: Gateway services (repositories, implementations) are now registered in Gateway
        // This method only adds framework-level services (security, multi-tenancy)
        
        // Register Multi-Tenancy support
        services.AddMultiTenancy();
        
        return services;
    }
    
    /// <summary>
    /// Configures Framework middleware pipeline including:
    /// - AuditLogging middleware ([AuditLog] attribute support)
    /// 
    /// Usage in Business API:
    /// app.UseFramework();
    /// 
    /// Note: Add AFTER UseRouting() and BEFORE UseAuthorization()
    /// </summary>
    public static IApplicationBuilder UseFramework(this IApplicationBuilder app)
    {
        // AuditLog middleware (from Asdamir.Web.Security)
        // IAuditLogService is optional - if registered, audit logs will be persisted to DB
        app.UseMiddleware<Asdamir.Web.Security.Middleware.AuditLoggingMiddleware>();
        
        return app;
    }
}
