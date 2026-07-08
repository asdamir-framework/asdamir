// Copyright (C) 2026 Orhan Özşahin — Asdamir.
// Licensed under the GNU Lesser General Public License v3.0. See LICENSE.
// SPDX-License-Identifier: LGPL-3.0-or-later
//
// This file is part of the Asdamir open core. It is free software: you can redistribute it
// and/or modify it under the terms of the GNU Lesser General Public License as published by the
// Free Software Foundation, either version 3 of the License, or (at your option) any later
// version. It is distributed WITHOUT ANY WARRANTY; without even the implied warranty of
// MERCHANTABILITY or FITNESS FOR A PARTICULAR PURPOSE. See the GNU LGPL for more details.

using Asdamir.Core.Http;
using Asdamir.Web.Security.Services;
using Microsoft.AspNetCore.Http;
using Microsoft.Extensions.DependencyInjection;
using System.Net;
using System.Net.Http.Headers;

namespace Asdamir.Web.Security.Http;

/// <summary>
/// Framework HTTP handler that:
///  1. Attaches the current Bearer token to outbound requests.
///  2. Propagates the correlation id header.
///  3. On 401 Unauthorized, attempts a one-shot refresh and replays the request once.
///
/// Audit fixes vs. v1:
///  - 401 refresh is now actually implemented (v1 logged "not implemented" and returned 401).
///  - Token preview is no longer logged at Information level — only token presence is logged.
/// </summary>
public class BearerHandler : DelegatingHandler
{
    private const string RefreshAttemptHeader = "x-refresh-attempt";

    private readonly IHttpContextAccessor _httpContextAccessor;
    private readonly CorrelationIdProvider _correlation;
    private readonly ILogger<BearerHandler> _logger;

    /// <summary>
    /// Initializes the handler with the accessors needed to resolve the token and correlation id.
    /// </summary>
    /// <param name="httpContextAccessor">Accessor used to resolve the circuit id and stored access token.</param>
    /// <param name="correlation">Provider that supplies (or creates) the correlation id header.</param>
    /// <param name="logger">Logger for token, correlation and refresh diagnostics.</param>
    public BearerHandler(
        IHttpContextAccessor httpContextAccessor,
        CorrelationIdProvider correlation,
        ILogger<BearerHandler> logger)
    {
        _httpContextAccessor = httpContextAccessor;
        _correlation = correlation;
        _logger = logger;
    }

    /// <inheritdoc/>
    protected override async Task<HttpResponseMessage> SendAsync(HttpRequestMessage request, CancellationToken cancellationToken)
    {
        var httpContext = _httpContextAccessor.HttpContext;
        var circuitId = ResolveCircuitId(httpContext);
        var token = ResolveAccessToken(httpContext, circuitId);

        if (!string.IsNullOrWhiteSpace(token))
        {
            request.Headers.Authorization = new AuthenticationHeaderValue("Bearer", token);
        }

        _logger.LogDebug(
            "BearerHandler: CircuitId={CircuitId}, HasToken={HasToken}, Url={Url}",
            circuitId ?? "null", !string.IsNullOrWhiteSpace(token), request.RequestUri);

        if (!request.Headers.Contains(HeaderNames.CorrelationId))
        {
            request.Headers.TryAddWithoutValidation(HeaderNames.CorrelationId, await _correlation.GetOrCreateAsync());
        }

        var response = await base.SendAsync(request, cancellationToken);

        if (response.StatusCode != HttpStatusCode.Unauthorized) return response;

        // Already retried — give up.
        if (request.Headers.Contains(RefreshAttemptHeader)) return response;

        // Attempt a one-shot refresh + replay.
        var tokenSvc = httpContext?.RequestServices?.GetService<IAuthorizationTokenService>();
        if (tokenSvc is null)
        {
            _logger.LogDebug("BearerHandler: IAuthorizationTokenService not available — cannot refresh");
            return response;
        }

        bool refreshed;
        try
        {
            refreshed = await tokenSvc.TryRefreshTokenAsync();
        }
        catch (Exception ex)
        {
            _logger.LogWarning(ex, "BearerHandler: refresh attempt threw");
            return response;
        }

        if (!refreshed)
        {
            _logger.LogInformation("BearerHandler: refresh declined (no refresh token or backend rejected)");
            return response;
        }

        // Re-read the (possibly new) token and replay the request.
        var newToken = ResolveAccessToken(httpContext, circuitId);
        if (string.IsNullOrWhiteSpace(newToken))
        {
            _logger.LogWarning("BearerHandler: refresh reported success but no new token available");
            return response;
        }

        response.Dispose();
        request.Headers.Authorization = new AuthenticationHeaderValue("Bearer", newToken);
        request.Headers.Remove(RefreshAttemptHeader);
        request.Headers.TryAddWithoutValidation(RefreshAttemptHeader, "1");

        _logger.LogInformation("BearerHandler: replaying request after successful refresh");
        return await base.SendAsync(request, cancellationToken);
    }

    private static string? ResolveCircuitId(HttpContext? httpContext)
    {
        if (httpContext is null) return null;

        if (httpContext.Items.TryGetValue("CircuitId", out var obj) && obj is string s && !string.IsNullOrWhiteSpace(s))
            return s;

        var connectionId = httpContext.Connection?.Id;
        if (!string.IsNullOrEmpty(connectionId)) return connectionId;

        return string.IsNullOrEmpty(httpContext.TraceIdentifier) ? null : httpContext.TraceIdentifier;
    }

    private static string? ResolveAccessToken(HttpContext? httpContext, string? circuitId)
    {
        if (httpContext?.Items.TryGetValue("AccessToken", out var tokenObj) == true)
        {
            var t = tokenObj?.ToString();
            if (!string.IsNullOrEmpty(t)) return t;
        }

        return string.IsNullOrEmpty(circuitId) ? null : TokenStore.GetToken(circuitId);
    }
}
