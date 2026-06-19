// Copyright (C) 2026 Orhan Özşahin — Asdamir.
// Licensed under the GNU Lesser General Public License v3.0. See LICENSE.
// SPDX-License-Identifier: LGPL-3.0-or-later
//
// This file is part of the Asdamir open core. It is free software: you can redistribute it
// and/or modify it under the terms of the GNU Lesser General Public License as published by the
// Free Software Foundation, either version 3 of the License, or (at your option) any later
// version. It is distributed WITHOUT ANY WARRANTY; without even the implied warranty of
// MERCHANTABILITY or FITNESS FOR A PARTICULAR PURPOSE. See the GNU LGPL for more details.

using Microsoft.Extensions.Options;
using FluentValidation.Results;
using Microsoft.AspNetCore.Http;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.DependencyInjection;

namespace Asdamir.Core.Validation;

/// <summary>
/// Enterprise validation service implementation
/// </summary>
public class ValidationService : IValidationService
{
    private readonly IServiceProvider _serviceProvider;
    private readonly ILogger<ValidationService> _logger;
    private readonly IValidationContextFactory _contextFactory;
    private readonly IBusinessRuleEngine? _businessRuleEngine;
    private readonly ValidationOptions _options;
    private readonly IValidationResultEnricher? _enricher;

    public ValidationService(
        IServiceProvider serviceProvider,
        ILogger<ValidationService> logger,
        IValidationContextFactory contextFactory,
        IBusinessRuleEngine? businessRuleEngine = null,
        IOptions<ValidationOptions>? options = null,
        IValidationResultEnricher? enricher = null)
    {
        _serviceProvider = serviceProvider;
        _logger = logger;
        _contextFactory = contextFactory;
        _businessRuleEngine = businessRuleEngine;
        _options = options?.Value ?? new ValidationOptions();
        _enricher = enricher;
    }

    public async Task<ValidationResult> ValidateAsync<T>(T instance, CancellationToken cancellationToken = default)
    {
        try
        {
            var validators = GetValidators<T>();
            if (!validators.Any())
            {
                return new ValidationResult(); // No validators registered => valid
            }

            var result = new ValidationResult();

            foreach (var validator in validators)
            {
                var validationResult = await validator.ValidateAsync(instance, cancellationToken);
                result.Errors.AddRange(validationResult.Errors);
            }

            _enricher?.Enrich<T>(result);

            if (!result.IsValid && _options.LogValidationFailures)
            {
                _logger.LogWarning("Validation failed for {Type}. Errors: {Errors}",
                    typeof(T).Name,
                    string.Join("; ", result.Errors.Select(e => $"{e.PropertyName}:{e.ErrorMessage}")));
            }

            return result;
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Validation process failed. Type: {Type}, ErrorKey: {ErrorKey}, Source: {Source}",
                typeof(T).Name, "VALIDATION_ERROR", "ValidationService");
            throw;
        }
    }

    public async Task<ValidationResult> ValidateAsync<T, TValidator>(T instance, CancellationToken cancellationToken = default)
        where TValidator : class, IValidator<T>
    {
        var validator = _serviceProvider.GetRequiredService<TValidator>();
        return await validator.ValidateAsync(instance, cancellationToken);
    }

    public async Task ValidateAndThrowAsync<T>(T instance, CancellationToken cancellationToken = default)
    {
        var result = await ValidateAsync(instance, cancellationToken);
        if (!result.IsValid)
        {
            if (_options.ThrowOnValidationFailure)
            {
                throw new FluentValidation.ValidationException(result.Errors);
            }
        }
    }

    public async Task<bool> IsValidAsync<T>(T instance, CancellationToken cancellationToken = default)
    {
        var result = await ValidateAsync(instance, cancellationToken);
        return result.IsValid;
    }

    public IEnumerable<IValidator<T>> GetValidators<T>()
    {
        return _serviceProvider.GetServices<IValidator<T>>();
    }

    /// <summary>
    /// Validates with enterprise context and business rules
    /// </summary>
    public async Task<ValidationResult> ValidateWithContextAsync<T>(T instance, IValidationContext context, CancellationToken cancellationToken = default) where T : class
    {
        var result = new ValidationResult();

        // Standard validation
        var standardResult = await ValidateAsync(instance, cancellationToken);
        result.Errors.AddRange(standardResult.Errors);

        // Context-aware validation
        var contextValidators = _serviceProvider.GetServices<IValidator<T>>();
        foreach (var validator in contextValidators)
        {
            var contextResult = await validator.ValidateAsync(instance, context, cancellationToken);
            result.Errors.AddRange(contextResult.Errors);
        }

        // Business rule validation
        if (_businessRuleEngine != null)
        {
            var businessRuleResult = await _businessRuleEngine.EvaluateAsync(instance, context, cancellationToken);
            foreach (var error in businessRuleResult.Errors)
            {
                result.Errors.Add(new ValidationFailure(string.Empty, error));
            }
        }

        if (!result.IsValid && _options.LogValidationFailures)
        {
            _logger.LogWarning("Context validation failed for {Type}. Mode={Mode}, Errors={Errors}",
                typeof(T).Name,
                context.Mode,
                string.Join("; ", result.Errors.Select(e => $"{e.PropertyName}:{e.ErrorMessage}")));
        }

        return result;
    }

    /// <summary>
    /// Gets enterprise validators for a specific type
    /// </summary>
    public IEnumerable<IValidator<T>> GetContextValidators<T>()
    {
        return _serviceProvider.GetServices<IValidator<T>>();
    }
}

/// <summary>
/// Validation context implementation
/// </summary>
public class ValidationContext : IValidationContext
{
    public string? UserId { get; set; }
    public string? TenantId { get; set; }
    public string? Operation { get; set; }
    public IDictionary<string, object> Properties { get; set; } = new Dictionary<string, object>();
    public IServiceProvider Services { get; set; } = null!;
    public ValidationMode Mode { get; set; } = ValidationMode.Standard;
    public IDictionary<string, object> Data => Properties;

    public T? GetProperty<T>(string key)
    {
        if (Properties.TryGetValue(key, out var value) && value is T typedValue)
        {
            return typedValue;
        }
        return default;
    }

    public void SetProperty<T>(string key, T value)
    {
        if (value != null)
        {
            Properties[key] = value;
        }
        else
        {
            Properties.Remove(key);
        }
    }
}

/// <summary>
/// Factory for creating validation contexts
/// </summary>
public class ValidationContextFactory : IValidationContextFactory
{
    private readonly IServiceProvider _serviceProvider;
    private readonly IHttpContextAccessor? _httpContextAccessor;

    public ValidationContextFactory(IServiceProvider serviceProvider, IHttpContextAccessor? httpContextAccessor = null)
    {
        _serviceProvider = serviceProvider;
        _httpContextAccessor = httpContextAccessor;
    }

    public IValidationContext CreateContext(string? operation = null)
    {
        var (userId, tenantId) = ResolveUserAndTenant();
        return new ValidationContext
        {
            UserId = userId,
            TenantId = tenantId,
            Operation = operation,
            Services = _serviceProvider
        };
    }

    public IValidationContext CreateContext(string? userId, string? tenantId, string? operation = null)
    {
        return new ValidationContext
        {
            UserId = userId,
            TenantId = tenantId,
            Operation = operation,
            Services = _serviceProvider
        };
    }

    private (string userId, string tenantId) ResolveUserAndTenant()
    {
        var httpContext = _httpContextAccessor?.HttpContext;
        if (httpContext?.User?.Identity?.IsAuthenticated == true)
        {
            var uid = httpContext.User.FindFirst("sub")?.Value ?? httpContext.User.Identity?.Name ?? "system";
            var tid = httpContext.User.FindFirst("tenant")?.Value ?? "default";
            return (uid, tid);
        }
        return ("system", "default");
    }
}
