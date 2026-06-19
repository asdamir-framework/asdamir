// Copyright (C) 2026 Orhan Özşahin — Asdamir.
// Licensed under the GNU Lesser General Public License v3.0. See LICENSE.
// SPDX-License-Identifier: LGPL-3.0-or-later
//
// This file is part of the Asdamir open core. It is free software: you can redistribute it
// and/or modify it under the terms of the GNU Lesser General Public License as published by the
// Free Software Foundation, either version 3 of the License, or (at your option) any later
// version. It is distributed WITHOUT ANY WARRANTY; without even the implied warranty of
// MERCHANTABILITY or FITNESS FOR A PARTICULAR PURPOSE. See the GNU LGPL for more details.

using FluentValidation;

namespace Asdamir.Core.Validation;

/// <summary>
/// FluentValidation extensions that reuse the shared validation helpers.
/// </summary>
public static class ContactValidationExtensions
{
    public static IRuleBuilderOptionsConditions<T, string?> ValidEmail<T>(
        this IRuleBuilder<T, string?> ruleBuilder,
        string[]? allowedDomains = null,
        string[]? blockedDomains = null,
        bool blockRoleAddresses = false)
    {
        return ruleBuilder.Custom((email, context) =>
        {
            var opts = new ValidationUtils.EmailOptions(
                allowedDomains ?? Array.Empty<string>(),
                blockedDomains ?? Array.Empty<string>(),
                blockRoleAddresses);

            if (!ValidationUtils.TryValidateEmail(email, opts, out var error))
            {
                context.AddFailure(error ?? "Invalid email");
            }
        });
    }

    public static IRuleBuilderOptionsConditions<T, string?> ValidPhone<T>(
        this IRuleBuilder<T, string?> ruleBuilder,
        string? countryCode = "TR",
        bool allowInternational = true,
        bool requireMobile = false)
    {
        return ruleBuilder.Custom((phone, context) =>
        {
            var opts = new ValidationUtils.PhoneOptions(countryCode, allowInternational, requireMobile);
            if (!ValidationUtils.TryValidatePhone(phone, opts, out var error))
            {
                context.AddFailure(error ?? "Invalid phone number");
            }
        });
    }
}
