// Copyright (C) 2026 Orhan Özşahin — Asdamir.
// Licensed under the GNU Lesser General Public License v3.0. See LICENSE.
// SPDX-License-Identifier: LGPL-3.0-or-later
//
// This file is part of the Asdamir open core. It is free software: you can redistribute it
// and/or modify it under the terms of the GNU Lesser General Public License as published by the
// Free Software Foundation, either version 3 of the License, or (at your option) any later
// version. It is distributed WITHOUT ANY WARRANTY; without even the implied warranty of
// MERCHANTABILITY or FITNESS FOR A PARTICULAR PURPOSE. See the GNU LGPL for more details.

using Microsoft.AspNetCore.Mvc;
using Asdamir.Core.ErrorHandling.Abstractions;
using Asdamir.Core.ErrorHandling.Domain;
using Asdamir.Core.Contracts;

namespace Asdamir.Core.ErrorHandling.Http;

/// <summary>
/// Default implementation of <see cref="IProblemDetailsMapper"/>.
///
/// Audit fixes vs. v1:
///  - <c>code = exception.GetType().Name</c> leaked internal class names to the client
///    (e.g. <c>SqlException</c>, <c>NpgsqlException</c>) — telling a probing attacker
///    the exact backend DB / library version. The mapped <see cref="ProblemDetails.Type"/>
///    URN, the <c>Extensions["code"]</c> field, and the <see cref="ProblemDetails.Title"/>
///    all leaked this. We now map non-domain exceptions to the stable <see cref="ErrorCodes.InternalError"/>
///    code; the type name remains in the server-side log only.
///  - <see cref="ProblemDetails.Title"/> used to be <c>exception.Message</c> for the
///    non-domain path (and for <c>MapBadRequest</c>) — e.g. a <c>SqlException</c> message
///    like "Cannot open database 'foo' requested by the login 'bar'". The client now
///    sees a stable, localised string; the original Message is in the audit log only.
///  - <see cref="ProblemDetails.Extensions"/> no longer carries the raw Turkish/English/Russian
///    exception messages either — those would have leaked the same internal text. The translation
///    flow is purely keyed by error CODE, not exception message.
/// </summary>
public class DefaultProblemDetailsMapper : IProblemDetailsMapper
{
    private readonly ILocalizationService? _localizationService;

    /// <summary>
    /// Creates the mapper, optionally with a localization service used to pre-translate a domain
    /// exception's title; when none is supplied, titles stay as stable <c>error.&lt;code&gt;</c> keys for
    /// the middleware to translate by request culture.
    /// </summary>
    /// <param name="localizationService">Resolver for <c>error.*</c> keys; null disables pre-translation.</param>
    public DefaultProblemDetailsMapper(ILocalizationService? localizationService = null)
    {
        _localizationService = localizationService;
    }

    /// <summary>
    /// Maps an exception to its HTTP status, a stable error code, and a base ProblemDetails: a
    /// <see cref="DomainException"/> keeps its own <c>Code</c>, while every other type is folded into a
    /// small fixed set of codes so the client can never enumerate internal exception types.
    /// </summary>
    /// <param name="exception">The unhandled exception to classify.</param>
    /// <param name="traceId">Correlation id recorded as <see cref="ProblemDetails.Instance"/> for log matching.</param>
    /// <param name="cancellationToken">Cancels the optional localization lookup.</param>
    /// <returns>The chosen status code, the stable error code, and the seed ProblemDetails (its Title may be
    /// re-localized by the middleware per the caller's culture).</returns>
    public async Task<(int StatusCode, string ErrorCode, ProblemDetails ProblemDetails)> MapAsync(
        Exception exception, string traceId, CancellationToken cancellationToken = default)
    {
        // Domain exceptions carry an explicit, intentional Code. Anything else is folded
        // into a small set of stable codes so the client cannot enumerate internal types.
        return exception switch
        {
            DomainException domainEx => await MapDomainAsync(domainEx, traceId, cancellationToken).ConfigureAwait(false),
            UnauthorizedAccessException => MapStable(401, ErrorCodes.Unauthorized, traceId),
            KeyNotFoundException => MapStable(404, ErrorCodes.NotFound, traceId),
            ArgumentException => MapStable(400, ErrorCodes.BadRequest, traceId),
            InvalidOperationException => MapStable(400, ErrorCodes.BadRequest, traceId),
            TimeoutException => MapStable(408, ErrorCodes.Timeout, traceId),
            _ => MapStable(500, ErrorCodes.InternalError, traceId),
        };
    }

    private async Task<(int, string, ProblemDetails)> MapDomainAsync(DomainException ex, string traceId, CancellationToken ct)
    {
        // For domain exceptions, the Title is the localized message keyed by code.
        // We still don't expose Message verbatim: a DomainException's Message can carry
        // user-supplied content (search terms etc.) so we route through the locale lookup
        // and fall back to a stable code title rather than the raw Message.
        string title = await TranslateAsync(ex.Code, ct).ConfigureAwait(false) ?? $"error.{ex.Code}";

        var pd = new ProblemDetails
        {
            Status = 400,
            Title = title,
            Type = $"urn:error:{ex.Code}",
            Instance = traceId,
        };
        pd.Extensions["code"] = ex.Code;
        return (400, ex.Code, pd);
    }

    private static (int, string, ProblemDetails) MapStable(int status, string code, string traceId)
    {
        var pd = new ProblemDetails
        {
            Status = status,
            Title = $"error.{code}", // GlobalExceptionMiddleware overwrites with the locale-translated message
            Type = $"urn:error:{code}",
            Instance = traceId,
        };
        pd.Extensions["code"] = code;
        return (status, code, pd);
    }

    private async Task<string?> TranslateAsync(string code, CancellationToken ct)
    {
        if (_localizationService is null) return null;
        try
        {
            // Pick a single canonical default for the mapper; the middleware re-translates
            // based on the user's culture if it wants something different. We do NOT round-trip
            // three languages here anymore — that was just leaking the same raw text in three
            // places in v1.
            return await _localizationService.GetAsync("tr-TR", $"error.{code}", ct).ConfigureAwait(false);
        }
        catch
        {
            return null;
        }
    }
}
