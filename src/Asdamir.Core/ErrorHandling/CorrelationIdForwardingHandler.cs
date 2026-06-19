// Copyright (C) 2026 Orhan Özşahin — Asdamir.
// Licensed under the GNU Lesser General Public License v3.0. See LICENSE.
// SPDX-License-Identifier: LGPL-3.0-or-later
//
// This file is part of the Asdamir open core. It is free software: you can redistribute it
// and/or modify it under the terms of the GNU Lesser General Public License as published by the
// Free Software Foundation, either version 3 of the License, or (at your option) any later
// version. It is distributed WITHOUT ANY WARRANTY; without even the implied warranty of
// MERCHANTABILITY or FITNESS FOR A PARTICULAR PURPOSE. See the GNU LGPL for more details.

namespace Asdamir.Core.ErrorHandling.Http;

/// <summary>
/// DelegatingHandler that propagates the current request's correlation id to outbound
/// HttpClient calls. Register it on any HttpClient that should carry tracing across
/// service boundaries:
///
/// <code>
/// services.AddScoped&lt;CorrelationIdForwardingHandler&gt;();
/// services.AddHttpClient("MyApi")
///         .AddHttpMessageHandler&lt;CorrelationIdForwardingHandler&gt;();
/// </code>
///
/// The handler is intentionally a no-op when no id is set yet (background services,
/// startup health probes) so it stays safe to register globally.
/// </summary>
public sealed class CorrelationIdForwardingHandler : DelegatingHandler
{
    private readonly ICorrelationIdAccessor _accessor;

    public CorrelationIdForwardingHandler(ICorrelationIdAccessor accessor)
    {
        _accessor = accessor;
    }

    protected override Task<HttpResponseMessage> SendAsync(HttpRequestMessage request, CancellationToken cancellationToken)
    {
        var id = _accessor.CurrentId;
        if (!string.IsNullOrWhiteSpace(id) && !request.Headers.Contains(CorrelationIdMiddleware.HeaderName))
        {
            request.Headers.TryAddWithoutValidation(CorrelationIdMiddleware.HeaderName, id);
        }
        return base.SendAsync(request, cancellationToken);
    }
}
