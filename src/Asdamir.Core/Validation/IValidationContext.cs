// Copyright (C) 2026 Orhan Özşahin — Asdamir.
// Licensed under the GNU Lesser General Public License v3.0. See LICENSE.
// SPDX-License-Identifier: LGPL-3.0-or-later
//
// This file is part of the Asdamir open core. It is free software: you can redistribute it
// and/or modify it under the terms of the GNU Lesser General Public License as published by the
// Free Software Foundation, either version 3 of the License, or (at your option) any later
// version. It is distributed WITHOUT ANY WARRANTY; without even the implied warranty of
// MERCHANTABILITY or FITNESS FOR A PARTICULAR PURPOSE. See the GNU LGPL for more details.

namespace Asdamir.Core.Validation;

/// <summary>
/// Validation context for enterprise validation scenarios
/// </summary>
public interface IValidationContext
{
    /// <summary>
    /// Gets the current user ID
    /// </summary>
    string? UserId { get; }
    
    /// <summary>
    /// Gets the current tenant ID
    /// </summary>
    string? TenantId { get; }
    
    /// <summary>
    /// Gets the validation mode
    /// </summary>
    ValidationMode Mode { get; }
    
    /// <summary>
    /// Gets additional context data
    /// </summary>
    IDictionary<string, object> Data { get; }
}

/// <summary>
/// Validation modes
/// </summary>
public enum ValidationMode
{
    /// <summary>
    /// Standard validation
    /// </summary>
    Standard,
    
    /// <summary>
    /// Strict validation with all rules
    /// </summary>
    Strict,
    
    /// <summary>
    /// Quick validation with only critical rules
    /// </summary>
    Quick
}
