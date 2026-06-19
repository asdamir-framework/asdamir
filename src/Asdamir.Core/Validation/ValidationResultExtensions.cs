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
/// Extension methods for ValidationResult
/// </summary>
public static class ValidationResultExtensions
{
    /// <summary>
    /// Checks if there are errors for a specific property
    /// </summary>
    public static bool HasErrorsForProperty(this ValidationResult result, string propertyName)
    {
        return result.Errors.Any(e => e.PropertyName == propertyName);
    }

    /// <summary>
    /// Gets the first error message
    /// </summary>
    public static string? GetFirstError(this ValidationResult result)
    {
        return result.Errors.FirstOrDefault()?.ErrorMessage;
    }

    /// <summary>
    /// Gets all error messages for a specific property
    /// </summary>
    public static IEnumerable<string> GetErrorsForProperty(this ValidationResult result, string propertyName)
    {
        return result.Errors
            .Where(e => e.PropertyName == propertyName)
            .Select(e => e.ErrorMessage);
    }

    /// <summary>
    /// Gets all error messages as a formatted string
    /// </summary>
    public static string GetErrorSummary(this ValidationResult result, string separator = "; ")
    {
        return string.Join(separator, result.Errors.Select(e => e.ErrorMessage));
    }

    /// <summary>
    /// Gets errors grouped by property name
    /// </summary>
    public static Dictionary<string, List<string>> GetErrorsByProperty(this ValidationResult result)
    {
        return result.Errors
            .GroupBy(e => e.PropertyName)
            .ToDictionary(
                g => g.Key,
                g => g.Select(e => e.ErrorMessage).ToList()
            );
    }

    /// <summary>
    /// Converts validation result to a simple error dictionary
    /// </summary>
    public static Dictionary<string, string[]> ToErrorDictionary(this ValidationResult result)
    {
        return result.Errors
            .GroupBy(e => e.PropertyName)
            .ToDictionary(
                g => g.Key,
                g => g.Select(e => e.ErrorMessage).ToArray()
            );
    }

    /// <summary>
    /// Combines multiple validation results
    /// </summary>
    public static ValidationResult Combine(this ValidationResult result, params ValidationResult[] otherResults)
    {
        var combined = new ValidationResult();
        combined.Errors.AddRange(result.Errors);
        
        foreach (var other in otherResults)
        {
            combined.Errors.AddRange(other.Errors);
        }
        
        return combined;
    }

    /// <summary>
    /// Filters errors by severity (if using custom validation failures)
    /// </summary>
    public static ValidationResult FilterBySeverity(this ValidationResult result, string severity)
    {
        var filtered = new ValidationResult();
        
        var filteredErrors = result.Errors
            .Where(e => e.CustomState?.ToString() == severity);
            
        filtered.Errors.AddRange(filteredErrors);
        
        return filtered;
    }

    /// <summary>
    /// Checks if validation result contains any warnings
    /// </summary>
    public static bool HasWarnings(this ValidationResult result)
    {
        return result.Errors.Any(e => e.Severity.ToString() == "Warning");
    }

    /// <summary>
    /// Gets only warning messages
    /// </summary>
    public static IEnumerable<string> GetWarnings(this ValidationResult result)
    {
        return result.Errors
            .Where(e => e.Severity.ToString() == "Warning")
            .Select(e => e.ErrorMessage);
    }

    /// <summary>
    /// Gets only error messages (excluding warnings)
    /// </summary>
    public static IEnumerable<string> GetErrors(this ValidationResult result)
    {
        return result.Errors
            .Where(e => e.Severity.ToString() != "Warning")
            .Select(e => e.ErrorMessage);
    }
}