// Copyright (C) 2026 Orhan Özşahin — Asdamir.
// Licensed under the GNU Lesser General Public License v3.0. See LICENSE.
// SPDX-License-Identifier: LGPL-3.0-or-later
//
// This file is part of the Asdamir open core. It is free software: you can redistribute it
// and/or modify it under the terms of the GNU Lesser General Public License as published by the
// Free Software Foundation, either version 3 of the License, or (at your option) any later
// version. It is distributed WITHOUT ANY WARRANTY; without even the implied warranty of
// MERCHANTABILITY or FITNESS FOR A PARTICULAR PURPOSE. See the GNU LGPL for more details.

using System.Net;

namespace Asdamir.Web.Security.Options;

/// <summary>
/// Tuning for <see cref="Middleware.AuditLoggingMiddleware"/>.
///
/// Hardened against the v1 audit findings:
///  - X-Forwarded-For trust: only honored when the immediate peer is in
///    <see cref="TrustedProxies"/>. Direct clients that lie about the header
///    are ignored.
///  - DoS via EnableBuffering: a body larger than <see cref="MaxRequestBodyBytes"/>
///    is rejected with 413 before the application sees it, so a single client
///    cannot pin GB-sized streams in memory.
///  - Default redaction: <see cref="AlwaysRedactProperties"/> is applied even
///    when the endpoint's [AuditLog(ExcludeProperties=...)] attribute is empty,
///    so plaintext passwords / tokens can never silently land in the audit
///    table due to a forgotten attribute.
/// </summary>
public sealed class AuditLoggingOptions
{
    /// <summary>
    /// Max size of a captured request body. Default 256 KB.
    /// Requests larger than this short-circuit with 413 Payload Too Large.
    /// </summary>
    public int MaxRequestBodyBytes { get; set; } = 256 * 1024;

    /// <summary>
    /// Peer addresses (the TCP connection) we trust to set X-Forwarded-For honestly.
    /// Empty list => never trust the header, always use Connection.RemoteIpAddress.
    /// Populate with the load balancer / reverse proxy IPs in production.
    /// </summary>
    public IList<string> TrustedProxies { get; } = new List<string>();

    /// <summary>
    /// Property names that are ALWAYS redacted in captured request bodies,
    /// even if the endpoint forgot to list them in [AuditLog(ExcludeProperties=...)].
    /// Case-insensitive.
    /// </summary>
    public IList<string> AlwaysRedactProperties { get; } = new List<string>
    {
        "password",
        "newPassword",
        "currentPassword",
        "oldPassword",
        "confirmPassword",
        "token",
        "refreshToken",
        "accessToken",
        "secret",
        "clientSecret",
        "apiKey",
        "authorization",
    };

    internal IReadOnlyList<IPAddress> GetTrustedProxyAddresses()
    {
        var list = new List<IPAddress>(TrustedProxies.Count);
        foreach (var raw in TrustedProxies)
        {
            if (IPAddress.TryParse(raw, out var ip))
                list.Add(ip);
        }
        return list;
    }
}
