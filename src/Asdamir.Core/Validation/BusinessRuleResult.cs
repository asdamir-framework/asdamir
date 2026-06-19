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
/// Represents the result of a business rule evaluation
/// </summary>
public class BusinessRuleResult
{
    /// <summary>
    /// Gets or sets whether the business rule passed
    /// </summary>
    public bool IsValid { get; set; }

    /// <summary>
    /// Gets or sets the error message if the rule failed
    /// </summary>
    public string? ErrorMessage { get; set; }

    /// <summary>
    /// Gets or sets the error code if the rule failed
    /// </summary>
    public string? ErrorCode { get; set; }

    /// <summary>
    /// Gets or sets additional metadata about the rule evaluation
    /// </summary>
    public Dictionary<string, object>? Metadata { get; set; }

    /// <summary>
    /// Gets or sets the severity level of the rule failure
    /// </summary>
    public BusinessRuleSeverity Severity { get; set; } = BusinessRuleSeverity.Error;

    /// <summary>
    /// Gets the list of errors (for backward compatibility)
    /// </summary>
    public IReadOnlyList<string> Errors => string.IsNullOrEmpty(ErrorMessage) ? Array.Empty<string>() : new[] { ErrorMessage };

    /// <summary>
    /// Creates a successful business rule result
    /// </summary>
    public static BusinessRuleResult Success()
    {
        return new BusinessRuleResult { IsValid = true };
    }

    /// <summary>
    /// Creates a failed business rule result
    /// </summary>
    public static BusinessRuleResult Failure(string errorMessage, string? errorCode = null, BusinessRuleSeverity severity = BusinessRuleSeverity.Error)
    {
        return new BusinessRuleResult
        {
            IsValid = false,
            ErrorMessage = errorMessage,
            ErrorCode = errorCode,
            Severity = severity
        };
    }

    /// <summary>
    /// Creates a failed business rule result with metadata
    /// </summary>
    public static BusinessRuleResult Failure(string errorMessage, string? errorCode, Dictionary<string, object> metadata, BusinessRuleSeverity severity = BusinessRuleSeverity.Error)
    {
        return new BusinessRuleResult
        {
            IsValid = false,
            ErrorMessage = errorMessage,
            ErrorCode = errorCode,
            Metadata = metadata,
            Severity = severity
        };
    }

    /// <summary>
    /// Combines this result with another result
    /// </summary>
    public BusinessRuleResult Combine(BusinessRuleResult other)
    {
        if (other == null) return this;

        // If either result is invalid, the combined result is invalid
        IsValid = IsValid && other.IsValid;

        // Combine error messages
        if (!string.IsNullOrEmpty(other.ErrorMessage))
        {
            if (string.IsNullOrEmpty(ErrorMessage))
            {
                ErrorMessage = other.ErrorMessage;
            }
            else
            {
                ErrorMessage += $"; {other.ErrorMessage}";
            }
        }

        // Use the more severe error code
        if (!string.IsNullOrEmpty(other.ErrorCode))
        {
            ErrorCode = other.ErrorCode;
        }

        // Combine metadata
        if (other.Metadata != null)
        {
            Metadata ??= new Dictionary<string, object>();
            foreach (var kvp in other.Metadata)
            {
                Metadata[kvp.Key] = kvp.Value;
            }
        }

        // Use the more severe severity
        if (other.Severity > Severity)
        {
            Severity = other.Severity;
        }

        return this;
    }
}

/// <summary>
/// Represents the severity level of a business rule failure
/// </summary>
public enum BusinessRuleSeverity
{
    /// <summary>
    /// Information level - rule failed but not critical
    /// </summary>
    Info,

    /// <summary>
    /// Warning level - rule failed and should be noted
    /// </summary>
    Warning,

    /// <summary>
    /// Error level - rule failed and is critical
    /// </summary>
    Error,

    /// <summary>
    /// Critical level - rule failed and is blocking
    /// </summary>
    Critical
}
