// Copyright (C) 2026 Orhan Özşahin — Asdamir.
// Licensed under the GNU Lesser General Public License v3.0. See LICENSE.
// SPDX-License-Identifier: LGPL-3.0-or-later
//
// This file is part of the Asdamir open core. It is free software: you can redistribute it
// and/or modify it under the terms of the GNU Lesser General Public License as published by the
// Free Software Foundation, either version 3 of the License, or (at your option) any later
// version. It is distributed WITHOUT ANY WARRANTY; without even the implied warranty of
// MERCHANTABILITY or FITNESS FOR A PARTICULAR PURPOSE. See the GNU LGPL for more details.

using System.Globalization;
using System.Net.Mail;
using PhoneNumbers;
using System.Text.RegularExpressions;

namespace Asdamir.Core.Validation;

/// <summary>
/// Shared validation helpers for attributes and FluentValidation extensions.
/// </summary>
public static class ValidationUtils
{
    private static readonly PhoneNumberUtil _phoneUtil = PhoneNumberUtil.GetInstance();
    
    private static readonly Regex BasicEmailRegex = new(
        @"^[^@\s]+@[^@\s]+\.[^@\s]+$",
        RegexOptions.Compiled | RegexOptions.IgnoreCase);

    private static readonly string[] DefaultRoleLocalParts = new[]
    {
        "admin", "support", "contact", "info", "sales", "postmaster", "noreply", "no-reply"
    };

    private static readonly Regex InternationalPhoneRegex = new(
        @"^\+?[1-9]\d{1,14}$",
        RegexOptions.Compiled);

    private static readonly Regex USPhoneRegex = new(
        @"^(\+1[-.\s]?)?(\(?[0-9]{3}\)?[-.\s]?)?[0-9]{3}[-.\s]?[0-9]{4}$",
        RegexOptions.Compiled);

    private static readonly Regex TrMobileRegex = new(
        @"^(?:\+90|0)?5\d{9}$",
        RegexOptions.Compiled);

    /// <summary>
    /// Policy inputs for email validation: domain allow/block lists and whether to reject
    /// generic role addresses (admin@, info@, …).
    /// </summary>
    /// <param name="AllowedDomains">When non-empty, only these domains pass; empty means any domain.</param>
    /// <param name="BlockedDomains">Domains that are always rejected.</param>
    /// <param name="BlockRoleAddresses">When true, generic role local-parts (admin, info, sales, …) are rejected.</param>
    public record EmailOptions(string[] AllowedDomains, string[] BlockedDomains, bool BlockRoleAddresses);

    /// <summary>
    /// Policy inputs for phone validation: the default region and whether international / mobile
    /// constraints apply.
    /// </summary>
    /// <param name="CountryCode">Default region (ISO code, e.g. "TR"); "ZZ" means unknown, so only + -prefixed numbers parse.</param>
    /// <param name="AllowInternational">Whether + -prefixed international numbers are accepted (used by the legacy path).</param>
    /// <param name="RequireMobile">When true, the number must be a mobile (or fixed-line-or-mobile) type.</param>
    public record PhoneOptions(
        string? CountryCode = "ZZ",
        bool AllowInternational = true,
        bool RequireMobile = false);

    /// <summary>
    /// Validates an email against RFC syntax plus the domain allow/block and role-address policy.
    /// An empty value passes (deferred to a separate required check).
    /// </summary>
    /// <param name="email">Candidate email; null/empty is treated as valid here.</param>
    /// <param name="options">Domain and role-address policy to enforce.</param>
    /// <param name="error">Set to a stable validation key (e.g. <c>validation.email.domain.blocked</c>) on failure; null on success.</param>
    /// <returns>True when the email is well-formed and satisfies the policy; false otherwise.</returns>
    public static bool TryValidateEmail(string? email, EmailOptions options, out string? error)
    {
        error = null;
        if (string.IsNullOrWhiteSpace(email))
            return true; // Let [Required] handle empties

        // Quick sanity check to avoid expensive operations on obviously bad input
        if (!BasicEmailRegex.IsMatch(email))
        {
            error = "validation.email.invalid";
            return false;
        }

        // MailAddress is not IDisposable in .NET, but for safety and clarity
        MailAddress mailAddress;
        try
        {
            // RFC-compliant parse; will throw if not valid
            mailAddress = new MailAddress(email);
        }
        catch
        {
            error = "validation.email.invalid";
            return false;
        }

        var domain = mailAddress.Host.ToLowerInvariant();

        // IDN support: convert to punycode to normalize comparisons
        try
        {
            domain = new IdnMapping().GetAscii(domain);
        }
        catch
        {
            // If IDN conversion fails, treat as invalid
            error = "validation.email.invalid";
            return false;
        }

        if (options.BlockedDomains.Contains(domain, StringComparer.OrdinalIgnoreCase))
        {
            error = "validation.email.domain.blocked";
            return false;
        }

        if (options.AllowedDomains.Length > 0 &&
            !options.AllowedDomains.Contains(domain, StringComparer.OrdinalIgnoreCase))
        {
            error = "validation.email.domain.notallowed";
            return false;
        }

        if (options.BlockRoleAddresses)
        {
            var localPart = mailAddress.User.ToLowerInvariant();
            if (DefaultRoleLocalParts.Contains(localPart))
            {
                error = "validation.email.roleaddress";
                return false;
            }
        }

        return true;
    }

    /// <summary>
    /// Validates a phone number using Google's libphonenumber for comprehensive international support.
    /// An empty value passes (deferred to a separate required check).
    /// </summary>
    /// <param name="phone">Candidate phone number; null/empty is treated as valid here.</param>
    /// <param name="options">Region and mobile-required policy to enforce.</param>
    /// <param name="error">Set to a stable validation key (e.g. <c>validation.phone.mustBeMobile</c>) on failure; null on success.</param>
    /// <returns>True when the number is a valid dialable number satisfying the policy; false otherwise.</returns>
    public static bool TryValidatePhone(string? phone, PhoneOptions options, out string? error)
    {
        error = null;
        if (string.IsNullOrWhiteSpace(phone))
            return true; // Let [Required] handle empties

        phone = phone.Trim();

        try
        {
            // Parse phone number with country code
            // ZZ means "unknown region" - will only accept numbers with + prefix
            var parsedNumber = _phoneUtil.Parse(phone, options.CountryCode ?? "ZZ");
            
            // Validate if it's a possible valid number
            if (!_phoneUtil.IsValidNumber(parsedNumber))
            {
                error = "validation.phone.invalid";
                return false;
            }

            // Check if mobile number is required
            if (options.RequireMobile)
            {
                var numberType = _phoneUtil.GetNumberType(parsedNumber);
                if (numberType != PhoneNumberType.MOBILE && 
                    numberType != PhoneNumberType.FIXED_LINE_OR_MOBILE)
                {
                    error = "validation.phone.mustBeMobile";
                    return false;
                }
            }

            return true;
        }
        catch (NumberParseException ex)
        {
            // Specific error messages based on parse error
            error = ex.ErrorType switch
            {
                ErrorType.INVALID_COUNTRY_CODE => "validation.phone.invalidCountryCode",
                ErrorType.NOT_A_NUMBER => "validation.phone.notANumber",
                ErrorType.TOO_SHORT_NSN => "validation.phone.tooShort",
                ErrorType.TOO_SHORT_AFTER_IDD => "validation.phone.tooShort",
                ErrorType.TOO_LONG => "validation.phone.tooLong",
                _ => "validation.phone.invalidFormat"
            };
            return false;
        }
        catch
        {
            error = "validation.phone.invalidFormat";
            return false;
        }
    }

    /// <summary>
    /// Legacy regex-based phone validation (E.164 / TR / US fallbacks) kept for backward compatibility.
    /// Prefer <c>TryValidatePhone</c>, which uses libphonenumber.
    /// </summary>
    /// <param name="phone">Candidate phone number; null/empty is treated as valid here.</param>
    /// <param name="options">Region, international-allowed, and mobile-required policy to enforce.</param>
    /// <param name="error">Set to a stable validation key on failure; null on success.</param>
    /// <returns>True when the number matches the region-specific regex under the policy; false otherwise.</returns>
    [Obsolete("Use TryValidatePhone with libphonenumber instead")]
    public static bool TryValidatePhoneLegacy(string? phone, PhoneOptions options, out string? error)
    {
        error = null;
        if (string.IsNullOrWhiteSpace(phone))
            return true;

        phone = phone.Trim();

        // International (E.164-ish) check if prefixed with +
        if (phone.StartsWith("+", StringComparison.Ordinal))
        {
            if (!options.AllowInternational)
            {
                error = "validation.phone.international.notallowed";
                return false;
            }

            if (!InternationalPhoneRegex.IsMatch(phone))
            {
                error = "validation.phone.international.invalid";
                return false;
            }

            return true;
        }

        // Country-specific fallbacks
        var countryCode = options.CountryCode?.ToUpperInvariant() ?? "ZZ";
        switch (countryCode)
        {
            case "TR":
                if (options.RequireMobile)
                {
                    if (!TrMobileRegex.IsMatch(phone))
                    {
                        error = "validation.phone.tr.mobile.invalid";
                        return false;
                    }
                    return true;
                }

                // Looser TR: allow landline-like 10 digits with optional leading 0
                var trNormalized = phone.StartsWith("0") ? phone[1..] : phone;
                if (trNormalized.Length == 10 && trNormalized.All(char.IsDigit))
                {
                    return true;
                }

                error = "validation.phone.tr.invalid";
                return false;

            case "US":
                if (!USPhoneRegex.IsMatch(phone))
                {
                    error = "validation.phone.us.invalid";
                    return false;
                }
                return true;

            default:
                // Generic numeric check
                if (!phone.All(char.IsDigit))
                {
                    error = "validation.phone.digitsonly";
                    return false;
                }

                if (phone.Length < 6 || phone.Length > 15)
                {
                    error = "validation.phone.length";
                    return false;
                }

                return true;
        }
    }
}
