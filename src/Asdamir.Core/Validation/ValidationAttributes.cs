// Copyright (C) 2026 Orhan Özşahin — Asdamir.
// Licensed under the GNU Lesser General Public License v3.0. See LICENSE.
// SPDX-License-Identifier: LGPL-3.0-or-later
//
// This file is part of the Asdamir open core. It is free software: you can redistribute it
// and/or modify it under the terms of the GNU Lesser General Public License as published by the
// Free Software Foundation, either version 3 of the License, or (at your option) any later
// version. It is distributed WITHOUT ANY WARRANTY; without even the implied warranty of
// MERCHANTABILITY or FITNESS FOR A PARTICULAR PURPOSE. See the GNU LGPL for more details.

using System.ComponentModel.DataAnnotations;

namespace Asdamir.Core.Validation;

/// <summary>
/// Validates that a string member is a well-formed email address, with optional domain allow/block
/// lists and role-address blocking. A null/empty/whitespace value passes (compose with <c>[Required]</c>
/// to forbid empties). Apply to string properties.
/// </summary>
/// <example><code>
/// [Email] public string Email { get; set; }
/// [Email("corp.com", "partner.org")] public string WorkEmail { get; set; } // only these domains allowed
/// </code></example>
public class EmailAttribute : ValidationAttribute
{
    private readonly string[] _allowedDomains;
    private readonly string[] _blockedDomains;

    /// <summary>
    /// When <c>true</c>, rejects role-based mailboxes (e.g. <c>info@</c>, <c>admin@</c>, <c>support@</c>);
    /// when <c>false</c> (default) they are accepted.
    /// </summary>
    public bool BlockRoleAddresses { get; set; } = false;

    /// <summary>
    /// Creates the attribute, optionally restricting the address to a whitelist of domains.
    /// </summary>
    /// <param name="allowedDomains">Domains the address must belong to; pass none to accept any domain.</param>
    public EmailAttribute(params string[] allowedDomains)
    {
        _allowedDomains = allowedDomains;
        _blockedDomains = Array.Empty<string>();
    }

    /// <summary>
    /// Creates the attribute with both an allow-list and a block-list of domains.
    /// </summary>
    /// <param name="allowedDomains">Domains the address must belong to; empty accepts any domain not blocked.</param>
    /// <param name="blockedDomains">Domains that are rejected even if otherwise allowed (block wins).</param>
    public EmailAttribute(string[] allowedDomains, string[] blockedDomains)
    {
        _allowedDomains = allowedDomains ?? Array.Empty<string>();
        _blockedDomains = blockedDomains ?? Array.Empty<string>();
    }

    /// <inheritdoc/>
    protected override ValidationResult? IsValid(object? value, System.ComponentModel.DataAnnotations.ValidationContext validationContext)
    {
        var options = new ValidationUtils.EmailOptions(_allowedDomains, _blockedDomains, BlockRoleAddresses);
        var email = value?.ToString();
        if (ValidationUtils.TryValidateEmail(email, options, out var error))
            return ValidationResult.Success;

        return new ValidationResult(error ?? "Invalid email");
    }
}

/// <summary>
/// Validates that a string member meets a configurable password-strength policy — length bounds plus
/// required character classes (upper/lower/digit/special). A null/empty/whitespace value passes (pair
/// with <c>[Required]</c> to forbid empties). All failing rules are reported together. Apply to string
/// properties.
/// </summary>
/// <example><code>[StrongPassword(MinLength = 12, RequireSpecialCharacter = false)] public string Password { get; set; }</code></example>
public class StrongPasswordAttribute : ValidationAttribute
{
    /// <summary>Minimum number of characters the password must contain (default 8).</summary>
    public int MinLength { get; set; } = 8;

    /// <summary>Maximum number of characters the password may contain (default 128).</summary>
    public int MaxLength { get; set; } = 128;

    /// <summary>When <c>true</c> (default), at least one uppercase letter (A–Z) is required.</summary>
    public bool RequireUppercase { get; set; } = true;

    /// <summary>When <c>true</c> (default), at least one lowercase letter (a–z) is required.</summary>
    public bool RequireLowercase { get; set; } = true;

    /// <summary>When <c>true</c> (default), at least one digit (0–9) is required.</summary>
    public bool RequireDigit { get; set; } = true;

    /// <summary>
    /// When <c>true</c> (default), at least one character from <see cref="SpecialCharacters"/> is required.
    /// </summary>
    public bool RequireSpecialCharacter { get; set; } = true;

    /// <summary>
    /// The set of characters counted as "special" for <see cref="RequireSpecialCharacter"/>. Override to
    /// widen or narrow which punctuation satisfies the rule.
    /// </summary>
    public string SpecialCharacters { get; set; } = "!@#$%^&*()_+-=[]{}|;:,.<>?";

    /// <inheritdoc/>
    protected override ValidationResult? IsValid(object? value, System.ComponentModel.DataAnnotations.ValidationContext validationContext)
    {
        if (value == null || string.IsNullOrWhiteSpace(value.ToString()))
        {
            return ValidationResult.Success;
        }

        var password = value.ToString()!;
        var errors = new List<string>();

        if (password.Length < MinLength)
        {
            errors.Add($"Password must be at least {MinLength} characters long");
        }

        if (password.Length > MaxLength)
        {
            errors.Add($"Password must not exceed {MaxLength} characters");
        }

        if (RequireUppercase && !password.Any(char.IsUpper))
        {
            errors.Add("Password must contain at least one uppercase letter");
        }

        if (RequireLowercase && !password.Any(char.IsLower))
        {
            errors.Add("Password must contain at least one lowercase letter");
        }

        if (RequireDigit && !password.Any(char.IsDigit))
        {
            errors.Add("Password must contain at least one digit");
        }

        if (RequireSpecialCharacter && !password.Any(c => SpecialCharacters.Contains(c)))
        {
            errors.Add($"Password must contain at least one special character ({SpecialCharacters})");
        }

        if (errors.Any())
        {
            return new ValidationResult(string.Join("; ", errors));
        }

        return ValidationResult.Success;
    }
}

/// <summary>
/// Validates a country-specific business identifier (tax ID / VAT number). Hyphens and spaces are
/// stripped before checking. Supported countries are US (9-digit EIN), GB (9-digit VAT, optional
/// <c>GB</c> prefix) and DE (10–11 digit tax number); any other <see cref="CountryCode"/> passes
/// unchecked. A null/empty/whitespace value passes (compose with <c>[Required]</c>). Apply to string
/// properties.
/// </summary>
/// <example><code>[BusinessIdentifier(CountryCode = "GB")] public string VatNumber { get; set; }</code></example>
public class BusinessIdentifierAttribute : ValidationAttribute
{
    /// <summary>
    /// ISO country code selecting the validation ruleset (<c>US</c>, <c>GB</c>, <c>DE</c>); case-insensitive.
    /// An unrecognized code skips validation. Default <c>US</c>.
    /// </summary>
    public string CountryCode { get; set; } = "US";

    /// <summary>
    /// Advisory label for the kind of identifier expected (e.g. <c>TaxId</c>, <c>VatNumber</c>); does not
    /// change the check, which is driven by <see cref="CountryCode"/>. Default <c>TaxId</c>.
    /// </summary>
    public string IdentifierType { get; set; } = "TaxId";

    /// <inheritdoc/>
    protected override ValidationResult? IsValid(object? value, System.ComponentModel.DataAnnotations.ValidationContext validationContext)
    {
        if (value == null || string.IsNullOrWhiteSpace(value.ToString()))
        {
            return ValidationResult.Success;
        }

        var identifier = value.ToString()!.Replace("-", "").Replace(" ", "");

        return CountryCode.ToUpperInvariant() switch
        {
            "US" => ValidateUSTaxId(identifier),
            "GB" => ValidateUKVATNumber(identifier),
            "DE" => ValidateGermanTaxNumber(identifier),
            _ => ValidationResult.Success // Default to success for unknown countries
        };
    }

    private ValidationResult? ValidateUSTaxId(string taxId)
    {
        // US EIN format: XX-XXXXXXX (9 digits)
        if (taxId.Length != 9 || !taxId.All(char.IsDigit))
        {
            return new ValidationResult("US Tax ID must be 9 digits");
        }

        return ValidationResult.Success;
    }

    private ValidationResult? ValidateUKVATNumber(string vatNumber)
    {
        // UK VAT format: GB999999999 or 999999999
        var cleanVat = vatNumber.StartsWith("GB") ? vatNumber[2..] : vatNumber;
        
        if (cleanVat.Length != 9 || !cleanVat.All(char.IsDigit))
        {
            return new ValidationResult("UK VAT number must be 9 digits");
        }

        return ValidationResult.Success;
    }

    private ValidationResult? ValidateGermanTaxNumber(string taxNumber)
    {
        // German tax number format varies by region, basic validation
        if (taxNumber.Length < 10 || taxNumber.Length > 11 || !taxNumber.All(char.IsDigit))
        {
            return new ValidationResult("German tax number must be 10-11 digits");
        }

        return ValidationResult.Success;
    }
}

/// <summary>
/// Validates that a string member is a parseable phone number (via libphonenumber), interpreting
/// national-format numbers against <see cref="CountryCode"/>. A null/empty/whitespace value passes
/// (compose with <c>[Required]</c>). Apply to string properties.
/// </summary>
/// <example><code>[Phone(CountryCode = "TR", RequireTurkishMobile = true)] public string Mobile { get; set; }</code></example>
public class PhoneAttribute : ValidationAttribute
{
    /// <summary>
    /// ISO country code used as the default region when parsing numbers written in national format
    /// (default <c>TR</c>).
    /// </summary>
    public string CountryCode { get; set; } = "TR";

    /// <summary>
    /// When <c>true</c> (default), numbers in international format (E.164, leading <c>+</c>) are accepted;
    /// when <c>false</c>, only numbers valid for <see cref="CountryCode"/> pass.
    /// </summary>
    public bool AllowInternational { get; set; } = true;

    /// <summary>
    /// When <c>true</c>, requires the value to be a Turkish mobile number specifically; when <c>false</c>
    /// (default), any valid number type is accepted.
    /// </summary>
    public bool RequireTurkishMobile { get; set; } = false;

    /// <inheritdoc/>
    protected override ValidationResult? IsValid(object? value, System.ComponentModel.DataAnnotations.ValidationContext validationContext)
    {
        var options = new ValidationUtils.PhoneOptions(CountryCode, AllowInternational, RequireTurkishMobile);
        var phone = value?.ToString();
        if (ValidationUtils.TryValidatePhone(phone, options, out var error))
            return ValidationResult.Success;

        return new ValidationResult(error ?? "Invalid phone number");
    }
}

/// <summary>
/// Validates that a <see cref="DateTime"/> member falls within configured bounds — absolute min/max
/// dates and/or past/future restrictions (compared against today's UTC date). A null value passes; a
/// non-<see cref="DateTime"/> value fails. Apply to <see cref="DateTime"/> properties.
/// </summary>
/// <example><code>[DateRange(AllowFutureDates = false)] public DateTime BirthDate { get; set; }</code></example>
public class DateRangeAttribute : ValidationAttribute
{
    /// <summary>
    /// Inclusive lower bound as a parseable date string (e.g. <c>2000-01-01</c>); empty (default) disables
    /// the lower bound.
    /// </summary>
    public string MinDate { get; set; } = "";

    /// <summary>
    /// Inclusive upper bound as a parseable date string (e.g. <c>2030-12-31</c>); empty (default) disables
    /// the upper bound.
    /// </summary>
    public string MaxDate { get; set; } = "";

    /// <summary>When <c>false</c>, rejects dates after today (UTC); <c>true</c> (default) permits them.</summary>
    public bool AllowFutureDates { get; set; } = true;

    /// <summary>When <c>false</c>, rejects dates before today (UTC); <c>true</c> (default) permits them.</summary>
    public bool AllowPastDates { get; set; } = true;

    /// <inheritdoc/>
    protected override ValidationResult? IsValid(object? value, System.ComponentModel.DataAnnotations.ValidationContext validationContext)
    {
        if (value == null)
        {
            return ValidationResult.Success;
        }

        if (value is not DateTime date)
        {
            return new ValidationResult("Value must be a valid date");
        }

        var now = DateTime.UtcNow.Date;

        if (!AllowFutureDates && date > now)
        {
            return new ValidationResult("Future dates are not allowed");
        }

        if (!AllowPastDates && date < now)
        {
            return new ValidationResult("Past dates are not allowed");
        }

        if (!string.IsNullOrEmpty(MinDate) && DateTime.TryParse(MinDate, out var minDate) && date < minDate)
        {
            return new ValidationResult($"Date must be after {minDate:yyyy-MM-dd}");
        }

        if (!string.IsNullOrEmpty(MaxDate) && DateTime.TryParse(MaxDate, out var maxDate) && date > maxDate)
        {
            return new ValidationResult($"Date must be before {maxDate:yyyy-MM-dd}");
        }

        return ValidationResult.Success;
    }
}

/// <summary>
/// Validates that a <see cref="decimal"/> member fits a fixed-point size — total significant digits
/// (precision) and digits after the point (scale), mirroring a SQL <c>decimal(p,s)</c> column. A null
/// value passes; a non-<see cref="decimal"/> value fails. Apply to <see cref="decimal"/> properties.
/// </summary>
/// <example><code>[DecimalPrecision(18, 2)] public decimal Amount { get; set; } // e.g. money</code></example>
public class DecimalPrecisionAttribute : ValidationAttribute
{
    /// <summary>Maximum total number of significant digits allowed (integer part + fractional part).</summary>
    public int Precision { get; set; }

    /// <summary>Maximum number of digits allowed after the decimal point.</summary>
    public int Scale { get; set; }

    /// <summary>
    /// Creates the attribute with the required fixed-point size.
    /// </summary>
    /// <param name="precision">Maximum total significant digits (see <see cref="Precision"/>).</param>
    /// <param name="scale">Maximum fractional digits (see <see cref="Scale"/>).</param>
    public DecimalPrecisionAttribute(int precision, int scale)
    {
        Precision = precision;
        Scale = scale;
    }

    /// <inheritdoc/>
    protected override ValidationResult? IsValid(object? value, System.ComponentModel.DataAnnotations.ValidationContext validationContext)
    {
        if (value == null)
        {
            return ValidationResult.Success;
        }

        if (value is not decimal decimalValue)
        {
            return new ValidationResult("Value must be a decimal number");
        }

        var parts = decimalValue.ToString().Split('.');
        var integerPart = parts[0].Replace("-", "");
        var decimalPart = parts.Length > 1 ? parts[1] : "";

        if (integerPart.Length + decimalPart.Length > Precision)
        {
            return new ValidationResult($"Number exceeds maximum precision of {Precision} digits");
        }

        if (decimalPart.Length > Scale)
        {
            return new ValidationResult($"Number exceeds maximum scale of {Scale} decimal places");
        }

        return ValidationResult.Success;
    }
}
