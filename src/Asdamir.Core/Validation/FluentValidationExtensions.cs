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

namespace Asdamir.Core.Validation.Extensions;

/// <summary>
/// FluentValidation extension methods for common validation scenarios
/// </summary>
public static class FluentValidationExtensions
{
    /// <summary>
    /// Validates phone number using libphonenumber for comprehensive international support
    /// </summary>
    /// <param name="ruleBuilder">The rule builder</param>
    /// <param name="defaultRegion">Default region code (e.g., "TR", "US"). Use "ZZ" for international-only</param>
    /// <returns>Rule builder options</returns>
    public static IRuleBuilderOptionsConditions<T, string> ValidPhoneNumber<T>(
        this IRuleBuilder<T, string> ruleBuilder, 
        string? defaultRegion = "ZZ")
    {
        return ruleBuilder.Custom((phone, context) =>
        {
            if (string.IsNullOrWhiteSpace(phone))
                return; // Let [Required] handle empties

            var options = new ValidationUtils.PhoneOptions(
                CountryCode: defaultRegion, 
                AllowInternational: true, 
                RequireMobile: false
            );

            if (!ValidationUtils.TryValidatePhone(phone, options, out var error))
            {
                context.AddFailure(context.PropertyPath, error ?? "Invalid phone number");
            }
        });
    }

    /// <summary>
    /// Validates mobile phone number specifically
    /// </summary>
    /// <param name="ruleBuilder">The rule builder</param>
    /// <param name="defaultRegion">Default region code (e.g., "TR", "US")</param>
    /// <returns>Rule builder options</returns>
    public static IRuleBuilderOptionsConditions<T, string> ValidMobileNumber<T>(
        this IRuleBuilder<T, string> ruleBuilder, 
        string? defaultRegion = "TR")
    {
        return ruleBuilder.Custom((phone, context) =>
        {
            if (string.IsNullOrWhiteSpace(phone))
                return; // Let [Required] handle empties

            var options = new ValidationUtils.PhoneOptions(
                CountryCode: defaultRegion, 
                AllowInternational: true, 
                RequireMobile: true
            );

            if (!ValidationUtils.TryValidatePhone(phone, options, out var error))
            {
                context.AddFailure(context.PropertyPath, error ?? "Invalid mobile number");
            }
        });
    }

    /// <summary>
    /// Validates email address with optional domain restrictions
    /// </summary>
    /// <param name="ruleBuilder">The rule builder</param>
    /// <param name="allowedDomains">Allowed email domains (empty = all allowed)</param>
    /// <param name="blockedDomains">Blocked email domains</param>
    /// <param name="blockRoleAddresses">Block role-based addresses (admin, support, etc.)</param>
    /// <returns>Rule builder options</returns>
    public static IRuleBuilderOptionsConditions<T, string> ValidEmail<T>(
        this IRuleBuilder<T, string> ruleBuilder,
        string[]? allowedDomains = null,
        string[]? blockedDomains = null,
        bool blockRoleAddresses = false)
    {
        return ruleBuilder.Custom((email, context) =>
        {
            if (string.IsNullOrWhiteSpace(email))
                return; // Let [Required] handle empties

            var options = new ValidationUtils.EmailOptions(
                AllowedDomains: allowedDomains ?? Array.Empty<string>(),
                BlockedDomains: blockedDomains ?? Array.Empty<string>(),
                BlockRoleAddresses: blockRoleAddresses
            );

            if (!ValidationUtils.TryValidateEmail(email, options, out var error))
            {
                context.AddFailure(context.PropertyPath, error ?? "Invalid email address");
            }
        });
    }

    /// <summary>
    /// Validates Turkish TC Identity Number (11 digits with algorithm validation)
    /// </summary>
    /// <param name="ruleBuilder">The rule builder</param>
    /// <returns>Rule builder options</returns>
    public static IRuleBuilderOptionsConditions<T, string> ValidTcIdentityNumber<T>(
        this IRuleBuilder<T, string> ruleBuilder)
    {
        return ruleBuilder.Custom((tcNo, context) =>
        {
            if (string.IsNullOrWhiteSpace(tcNo))
                return; // Let [Required] handle empties

            // TC Identity Number must be 11 digits
            if (tcNo.Length != 11 || !tcNo.All(char.IsDigit))
            {
                context.AddFailure(context.PropertyPath, "TC Identity Number must be 11 digits");
                return;
            }

            // First digit cannot be 0
            if (tcNo[0] == '0')
            {
                context.AddFailure(context.PropertyPath, "TC Identity Number cannot start with 0");
                return;
            }

            // Algorithm validation
            var digits = tcNo.Select(c => int.Parse(c.ToString())).ToArray();
            
            // 10th digit check: (sum of odd positions * 7 - sum of even positions) % 10
            var oddSum = digits[0] + digits[2] + digits[4] + digits[6] + digits[8];
            var evenSum = digits[1] + digits[3] + digits[5] + digits[7];
            var check10 = ((oddSum * 7) - evenSum) % 10;
            
            if (check10 != digits[9])
            {
                context.AddFailure(context.PropertyPath, "Invalid TC Identity Number");
                return;
            }

            // 11th digit check: sum of first 10 digits % 10
            var totalSum = digits.Take(10).Sum();
            var check11 = totalSum % 10;
            
            if (check11 != digits[10])
            {
                context.AddFailure(context.PropertyPath, "Invalid TC Identity Number");
            }
        });
    }

    /// <summary>
    /// Validates IBAN (International Bank Account Number)
    /// </summary>
    /// <param name="ruleBuilder">The rule builder</param>
    /// <param name="countryCode">Optional country code to restrict (e.g., "TR")</param>
    /// <returns>Rule builder options</returns>
    public static IRuleBuilderOptionsConditions<T, string> ValidIban<T>(
        this IRuleBuilder<T, string> ruleBuilder,
        string? countryCode = null)
    {
        return ruleBuilder.Custom((iban, context) =>
        {
            if (string.IsNullOrWhiteSpace(iban))
                return; // Let [Required] handle empties

            // Remove spaces and convert to uppercase
            var cleanIban = iban.Replace(" ", "").ToUpperInvariant();

            // IBAN must be between 15 and 34 characters
            if (cleanIban.Length < 15 || cleanIban.Length > 34)
            {
                context.AddFailure(context.PropertyPath, "IBAN length must be between 15 and 34 characters");
                return;
            }

            // First two characters must be country code
            if (!char.IsLetter(cleanIban[0]) || !char.IsLetter(cleanIban[1]))
            {
                context.AddFailure(context.PropertyPath, "IBAN must start with country code");
                return;
            }

            // Check if country code matches if specified
            if (!string.IsNullOrEmpty(countryCode))
            {
                var ibanCountry = cleanIban.Substring(0, 2);
                if (ibanCountry != countryCode.ToUpperInvariant())
                {
                    context.AddFailure(context.PropertyPath, $"IBAN must be from {countryCode}");
                    return;
                }
            }

            // Next two characters must be check digits
            if (!char.IsDigit(cleanIban[2]) || !char.IsDigit(cleanIban[3]))
            {
                context.AddFailure(context.PropertyPath, "Invalid IBAN format");
                return;
            }

            // Mod-97 algorithm validation
            try
            {
                // Move first 4 chars to end
                var rearranged = cleanIban.Substring(4) + cleanIban.Substring(0, 4);
                
                // Replace letters with numbers (A=10, B=11, ..., Z=35)
                var numericIban = "";
                foreach (var c in rearranged)
                {
                    if (char.IsDigit(c))
                        numericIban += c;
                    else if (char.IsLetter(c))
                        numericIban += (c - 'A' + 10).ToString();
                    else
                    {
                        context.AddFailure(context.PropertyPath, "IBAN contains invalid characters");
                        return;
                    }
                }

                // Calculate mod 97
                var remainder = BigIntegerMod97(numericIban);
                
                if (remainder != 1)
                {
                    context.AddFailure(context.PropertyPath, "Invalid IBAN checksum");
                }
            }
            catch
            {
                context.AddFailure(context.PropertyPath, "Invalid IBAN format");
            }
        });
    }

    private static int BigIntegerMod97(string number)
    {
        var remainder = 0;
        foreach (var c in number)
        {
            var digit = int.Parse(c.ToString());
            remainder = (remainder * 10 + digit) % 97;
        }
        return remainder;
    }

    /// <summary>
    /// Validates URL with optional scheme restriction
    /// </summary>
    /// <param name="ruleBuilder">The rule builder</param>
    /// <param name="requireHttps">Require HTTPS scheme</param>
    /// <returns>Rule builder options</returns>
    public static IRuleBuilderOptionsConditions<T, string> ValidUrl<T>(
        this IRuleBuilder<T, string> ruleBuilder,
        bool requireHttps = false)
    {
        return ruleBuilder.Custom((url, context) =>
        {
            if (string.IsNullOrWhiteSpace(url))
                return; // Let [Required] handle empties

            if (!Uri.TryCreate(url, UriKind.Absolute, out var uri))
            {
                context.AddFailure(context.PropertyPath, "Invalid URL format");
                return;
            }

            if (requireHttps && uri.Scheme != Uri.UriSchemeHttps)
            {
                context.AddFailure(context.PropertyPath, "URL must use HTTPS");
            }
        });
    }
}
