// Copyright (C) 2026 Orhan Özşahin — Asdamir.
// Licensed under the GNU Lesser General Public License v3.0. See LICENSE.
// SPDX-License-Identifier: LGPL-3.0-or-later
//
// This file is part of the Asdamir open core. It is free software: you can redistribute it
// and/or modify it under the terms of the GNU Lesser General Public License as published by the
// Free Software Foundation, either version 3 of the License, or (at your option) any later
// version. It is distributed WITHOUT ANY WARRANTY; without even the implied warranty of
// MERCHANTABILITY or FITNESS FOR A PARTICULAR PURPOSE. See the GNU LGPL for more details.

using Microsoft.Extensions.Options;
namespace Asdamir.Web.Security.Options;


public sealed class SecurityHeadersOptionsValidator : IValidateOptions<SecurityHeadersOptions>
{
    public ValidateOptionsResult Validate(string? name, SecurityHeadersOptions options)
    {
        if (options.UseXFrameOptions && string.IsNullOrWhiteSpace(options.XFrameOptionsValue))
            return ValidateOptionsResult.Fail("X-Frame-Options değeri boş olamaz.");
        if (options.UseReferrerPolicy && string.IsNullOrWhiteSpace(options.ReferrerPolicyValue))
            return ValidateOptionsResult.Fail("Referrer-Policy değeri boş olamaz.");
        if (options.UsePermissionsPolicy && string.IsNullOrWhiteSpace(options.PermissionsPolicyValue))
            return ValidateOptionsResult.Fail("Permissions-Policy değeri boş olamaz.");
        if (options.UseContentSecurityPolicy && string.IsNullOrWhiteSpace(options.ContentSecurityPolicy))
            return ValidateOptionsResult.Fail("Content-Security-Policy değeri boş olamaz.");
        return ValidateOptionsResult.Success;
    }
}


