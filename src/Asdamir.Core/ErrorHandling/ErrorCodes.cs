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
/// Stable, string-valued error keys shared across the framework. Each constant is the domain-agnostic
/// key that a <c>DomainException</c> / <c>Result</c> carries and that the two-channel error model
/// maps to a localized user message (via the <c>LocalizationResource</c> table) — matching is always by
/// this key, never by message text. Grouped by concern (general / business / external / data).
/// </summary>
public static class ErrorCodes
{
    // General errors

    /// <summary>A requested resource (by id/key) does not exist — surfaced as a 404-class error.</summary>
    public const string NotFound = "not_found";

    /// <summary>Incoming input failed shape/format/required-field validation before any business logic ran (400-class).</summary>
    public const string Validation = "validation_error";

    /// <summary>The caller is not authenticated (missing/invalid/expired credentials) — a 401-class error.</summary>
    public const string Unauthorized = "unauthorized";

    /// <summary>The caller is authenticated but lacks the permission/role for the action — a 403-class error.</summary>
    public const string Forbidden = "forbidden";

    /// <summary>The request conflicts with current state (e.g. a concurrent edit or already-existing resource) — 409-class.</summary>
    public const string Conflict = "conflict";

    /// <summary>The request is malformed or semantically invalid such that it cannot be processed — 400-class.</summary>
    public const string BadRequest = "bad_request";

    /// <summary>A handled server-side failure occurred while processing an otherwise valid request — 500-class.</summary>
    public const string InternalError = "internal_error";

    /// <summary>Fallback for an unclassified/unexpected failure with no more specific code — the last-resort 500-class key.</summary>
    public const string UnexpectedError = "unexpected_error";

    // Business logic errors

    /// <summary>A domain invariant or business rule rejected the operation even though the input was well-formed.</summary>
    public const string BusinessRuleViolation = "business_rule_violation";

    /// <summary>The operation is not permitted in the entity's current state (an illegal state transition).</summary>
    public const string InvalidOperation = "invalid_operation";

    /// <summary>The target resource is locked (by another user/process) and cannot be mutated right now.</summary>
    public const string ResourceLocked = "resource_locked";

    /// <summary>A usage/rate/allocation quota has been exhausted, so the request is refused until it resets.</summary>
    public const string QuotaExceeded = "quota_exceeded";

    // External service errors

    /// <summary>A downstream/third-party service returned an error while fulfilling the request.</summary>
    public const string ExternalServiceError = "external_service_error";

    /// <summary>A required dependency (DB, provider, downstream API) is temporarily unreachable — 503-class.</summary>
    public const string ServiceUnavailable = "service_unavailable";

    /// <summary>An operation (typically a downstream call) exceeded its time budget before completing.</summary>
    public const string Timeout = "timeout";

    /// <summary>Submitting a work order to its downstream handler/queue failed — a domain-specific external-service error.</summary>
    public const string WorkOrderSubmissionFailed = "workorder_submission_failed";

    // Data errors

    /// <summary>A persistence operation would breach data integrity (referential/relational invariants).</summary>
    public const string DataIntegrityViolation = "data_integrity_violation";

    /// <summary>An insert/update violated a unique key/index — a duplicate value already exists.</summary>
    public const string DuplicateKey = "duplicate_key";

    /// <summary>A database constraint (check/foreign-key/not-null) rejected the write.</summary>
    public const string ConstraintViolation = "constraint_violation";
}
