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
using Microsoft.Extensions.DependencyInjection.Extensions;
using FluentValidation;

namespace Asdamir.Core.Validation;

/// <summary>
/// Extension methods for registering validation services.
///
/// Audit fix vs. v1: registrations were <c>AddScoped</c>, not <c>TryAddScoped</c>, and
/// the built-in <see cref="IBusinessRule"/> implementations used <c>AddScoped</c> instead
/// of <c>TryAddEnumerable</c>. Two effects:
///   1. Calling <c>AddValidation()</c> then <c>AddFullValidation()</c> registered
///      <see cref="IValidationService"/> + friends twice. Resolving the single-interface
///      service still worked (last wins), but <c>IEnumerable&lt;T&gt;</c> enumerated both.
///   2. The three built-in <see cref="IBusinessRule"/> registrations were duplicated when
///      called from multiple entry points. <see cref="BusinessRuleEngine"/> now dedupes by
///      <c>rule.Name</c> internally, but registering once is still the right contract.
/// </summary>
public static class ServiceCollectionExtensions
{
    /// <summary>
    /// Adds validation services. Idempotent.
    /// </summary>
    public static IServiceCollection AddValidation(this IServiceCollection services)
    {
        services.TryAddScoped<IValidationService, ValidationService>();
        services.TryAddScoped<IValidationContextFactory, ValidationContextFactory>();
        services.TryAddScoped<IValidationResultEnricher, ValidationResultEnricher>();
        return services;
    }

    /// <summary>
    /// Adds validation services with full features. Idempotent.
    /// </summary>
    public static IServiceCollection AddFullValidation(this IServiceCollection services)
    {
        services.AddValidation();
        services.TryAddScoped<IBusinessRuleEngine, BusinessRuleEngine>();
        services.TryAddScoped<IBusinessRuleFactory, BusinessRuleFactory>();

        // TryAddEnumerable: each concrete IBusinessRule type is registered at most once
        // even across repeated calls. The engine still receives the full set via DI.
        services.TryAddEnumerable(ServiceDescriptor.Scoped<IBusinessRule, CreditLimitRule>());
        services.TryAddEnumerable(ServiceDescriptor.Scoped<IBusinessRule, InventoryAvailabilityRule>());
        services.TryAddEnumerable(ServiceDescriptor.Scoped<IBusinessRule, BusinessHoursRule>());

        return services;
    }

    /// <summary>
    /// Adds validation services with FluentValidation integration.
    /// </summary>
    public static IServiceCollection AddValidationWithFluentValidation(this IServiceCollection services)
    {
        services.AddFullValidation();
        services.AddValidatorsFromAssembly(typeof(ServiceCollectionExtensions).Assembly);
        return services;
    }

    /// <summary>
    /// Adds validation services with custom configuration.
    /// </summary>
    public static IServiceCollection AddValidation(this IServiceCollection services, Action<ValidationOptions> configure)
    {
        services.Configure(configure);
        services.AddFullValidation();
        return services;
    }

    /// <summary>
    /// Adds a custom business rule. Idempotent per concrete <typeparamref name="T"/>.
    /// </summary>
    public static IServiceCollection AddBusinessRule<T>(this IServiceCollection services) where T : class, IBusinessRule
    {
        services.TryAddEnumerable(ServiceDescriptor.Scoped<IBusinessRule, T>());
        return services;
    }

    /// <summary>
    /// Adds a custom validator. Idempotent per concrete <typeparamref name="TValidator"/>.
    /// </summary>
    public static IServiceCollection AddValidator<TValidator, TEntity>(this IServiceCollection services)
        where TValidator : class, IValidator<TEntity>
        where TEntity : class
    {
        services.TryAddEnumerable(ServiceDescriptor.Scoped<IValidator<TEntity>, TValidator>());
        return services;
    }
}

/// <summary>
/// Configuration options for validation services
/// </summary>
public class ValidationOptions
{
    /// <summary>
    /// Whether to throw exceptions on validation failure
    /// </summary>
    public bool ThrowOnValidationFailure { get; set; } = false;
    
    /// <summary>
    /// Whether to log validation failures
    /// </summary>
    public bool LogValidationFailures { get; set; } = true;
    
    /// <summary>
    /// Whether to include detailed error messages
    /// </summary>
    public bool IncludeDetailedErrors { get; set; } = true;
}
