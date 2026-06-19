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
/// Email validation attribute
/// </summary>
public class EmailAttribute : ValidationAttribute
{
    private readonly string[] _allowedDomains;
    private readonly string[] _blockedDomains;
    public bool BlockRoleAddresses { get; set; } = false;

    public EmailAttribute(params string[] allowedDomains)
    {
        _allowedDomains = allowedDomains;
        _blockedDomains = Array.Empty<string>();
    }

    public EmailAttribute(string[] allowedDomains, string[] blockedDomains)
    {
        _allowedDomains = allowedDomains ?? Array.Empty<string>();
        _blockedDomains = blockedDomains ?? Array.Empty<string>();
    }

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
/// Strong password validation attribute
/// </summary>
public class StrongPasswordAttribute : ValidationAttribute
{
    public int MinLength { get; set; } = 8;
    public int MaxLength { get; set; } = 128;
    public bool RequireUppercase { get; set; } = true;
    public bool RequireLowercase { get; set; } = true;
    public bool RequireDigit { get; set; } = true;
    public bool RequireSpecialCharacter { get; set; } = true;
    public string SpecialCharacters { get; set; } = "!@#$%^&*()_+-=[]{}|;:,.<>?";

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
/// Business identifier validation attribute (for tax numbers, VAT numbers, etc.)
/// </summary>
public class BusinessIdentifierAttribute : ValidationAttribute
{
    public string CountryCode { get; set; } = "US";
    public string IdentifierType { get; set; } = "TaxId";

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
/// Enterprise phone number validation attribute
/// </summary>
public class PhoneAttribute : ValidationAttribute
{
    public string CountryCode { get; set; } = "TR";
    public bool AllowInternational { get; set; } = true;
    public bool RequireTurkishMobile { get; set; } = false;

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
/// Date range validation attribute
/// </summary>
public class DateRangeAttribute : ValidationAttribute
{
    public string MinDate { get; set; } = "";
    public string MaxDate { get; set; } = "";
    public bool AllowFutureDates { get; set; } = true;
    public bool AllowPastDates { get; set; } = true;

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
/// Decimal precision validation attribute
/// </summary>
public class DecimalPrecisionAttribute : ValidationAttribute
{
    public int Precision { get; set; }
    public int Scale { get; set; }

    public DecimalPrecisionAttribute(int precision, int scale)
    {
        Precision = precision;
        Scale = scale;
    }

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
