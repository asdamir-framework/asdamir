// Copyright (C) 2026 Orhan Özşahin — Asdamir.
// Licensed under the GNU Lesser General Public License v3.0. See LICENSE.
// SPDX-License-Identifier: LGPL-3.0-or-later
//
// This file is part of the Asdamir open core. It is free software: you can redistribute it
// and/or modify it under the terms of the GNU Lesser General Public License as published by the
// Free Software Foundation, either version 3 of the License, or (at your option) any later
// version. It is distributed WITHOUT ANY WARRANTY; without even the implied warranty of
// MERCHANTABILITY or FITNESS FOR A PARTICULAR PURPOSE. See the GNU LGPL for more details.

namespace Asdamir.Web.Security.Options;

public sealed class SecurityHeadersOptions
{
    public bool UseHsts { get; set; } = true;
    public string HstsValue { get; set; } = "max-age=31536000; includeSubDomains";
    public bool UseXContentTypeOptions { get; set; } = true;
    public bool UseXFrameOptions { get; set; } = true;
    public string XFrameOptionsValue { get; set; } = "DENY";
    public bool UseReferrerPolicy { get; set; } = true;
    public string ReferrerPolicyValue { get; set; } = "no-referrer";
    public bool UsePermissionsPolicy { get; set; } = true;
    public string PermissionsPolicyValue { get; set; } = "geolocation=(), microphone=()";
    public bool UseContentSecurityPolicy { get; set; } = false;
    public string? ContentSecurityPolicy { get; set; }
        = "default-src 'self'; object-src 'none'; frame-ancestors 'none'; base-uri 'self'";
}


