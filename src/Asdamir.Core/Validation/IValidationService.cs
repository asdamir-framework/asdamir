// Copyright (C) 2026 Orhan Özşahin — Asdamir.
// Licensed under the GNU Lesser General Public License v3.0. See LICENSE.
// SPDX-License-Identifier: LGPL-3.0-or-later
//
// This file is part of the Asdamir open core. It is free software: you can redistribute it
// and/or modify it under the terms of the GNU Lesser General Public License as published by the
// Free Software Foundation, either version 3 of the License, or (at your option) any later
// version. It is distributed WITHOUT ANY WARRANTY; without even the implied warranty of
// MERCHANTABILITY or FITNESS FOR A PARTICULAR PURPOSE. See the GNU LGPL for more details.

using FluentValidation.Results;

namespace Asdamir.Core.Validation;

/// <summary>
/// Defines the contract for validation services in the enterprise framework
/// </summary>
public interface IValidationService
{
    /// <summary>
    /// Validates an object using registered validators
    /// </summary>
    Task<ValidationResult> ValidateAsync<T>(T instance, CancellationToken cancellationToken = default);
    
    /// <summary>
    /// Validates an object using a specific validator type
    /// </summary>
    Task<ValidationResult> ValidateAsync<T, TValidator>(T instance, CancellationToken cancellationToken = default)
        where TValidator : class, IValidator<T>;
    
    /// <summary>
    /// Validates an object and throws an exception if validation fails
    /// </summary>
    Task ValidateAndThrowAsync<T>(T instance, CancellationToken cancellationToken = default);
    
    /// <summary>
    /// Checks if an object is valid without returning detailed results
    /// </summary>
    Task<bool> IsValidAsync<T>(T instance, CancellationToken cancellationToken = default);
    
    /// <summary>
    /// Gets all registered validators for a specific type
    /// </summary>
    IEnumerable<IValidator<T>> GetValidators<T>();
}


/// <summary>
/// Factory for creating validation contexts
/// </summary>
public interface IValidationContextFactory
{
    /// <summary>
    /// Creates a validation context for the current request
    /// </summary>
    IValidationContext CreateContext(string? operation = null);
    
    /// <summary>
    /// Creates a validation context with specific parameters
    /// </summary>
    IValidationContext CreateContext(string? userId, string? tenantId, string? operation = null);
}
