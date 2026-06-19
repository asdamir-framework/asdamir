// Copyright (C) 2026 Orhan Özşahin — Asdamir.
// Licensed under the GNU Lesser General Public License v3.0. See LICENSE.
// SPDX-License-Identifier: LGPL-3.0-or-later
//
// This file is part of the Asdamir open core. It is free software: you can redistribute it
// and/or modify it under the terms of the GNU Lesser General Public License as published by the
// Free Software Foundation, either version 3 of the License, or (at your option) any later
// version. It is distributed WITHOUT ANY WARRANTY; without even the implied warranty of
// MERCHANTABILITY or FITNESS FOR A PARTICULAR PURPOSE. See the GNU LGPL for more details.

namespace Asdamir.Core.ErrorHandling.Domain;

/// <summary>
/// Standard error codes used throughout the application
/// </summary>
public static class ErrorCodes
{
    // General errors
    public const string NotFound = "not_found";
    public const string Validation = "validation_error";
    public const string Unauthorized = "unauthorized";
    public const string Forbidden = "forbidden";
    public const string Conflict = "conflict";
    public const string BadRequest = "bad_request";
    public const string InternalError = "internal_error";
    public const string UnexpectedError = "unexpected_error";
    
    // Business logic errors
    public const string BusinessRuleViolation = "business_rule_violation";
    public const string InvalidOperation = "invalid_operation";
    public const string ResourceLocked = "resource_locked";
    public const string QuotaExceeded = "quota_exceeded";
    
    // External service errors
    public const string ExternalServiceError = "external_service_error";
    public const string ServiceUnavailable = "service_unavailable";
    public const string Timeout = "timeout";
    public const string WorkOrderSubmissionFailed = "workorder_submission_failed";
    
    // Data errors
    public const string DataIntegrityViolation = "data_integrity_violation";
    public const string DuplicateKey = "duplicate_key";
    public const string ConstraintViolation = "constraint_violation";
}
