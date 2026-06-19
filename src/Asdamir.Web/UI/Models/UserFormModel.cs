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

namespace Asdamir.Web.UI.Models;

public class UserFormModel : IValidatableObject
{
    public int Id { get; set; }

    [Required(ErrorMessage = "Email adresi zorunludur")]
    [EmailAddress(ErrorMessage = "Geçerli bir email adresi giriniz")]
    public string Email { get; set; } = string.Empty;

    [Required(ErrorMessage = "İsim alanı zorunludur")]
    [MinLength(2, ErrorMessage = "İsim en az 2 karakter olmalıdır")]
    public string Name { get; set; } = string.Empty;

    [Required(ErrorMessage = "Rol seçimi zorunludur")]
    public string Role { get; set; } = "User";

    public bool IsActive { get; set; } = true;

    [Phone(ErrorMessage = "Geçerli bir telefon numarası giriniz")]
    [RegularExpression(@"^\+?[1-9]\d{1,14}$", ErrorMessage = "Uluslararası telefon formatı: +905XXXXXXXXX")]
    public string? PhoneNumber { get; set; }

    public bool PhoneNumberVerified { get; set; }

    public Guid? CompanyId { get; set; }
    
    public string? CompanyName { get; set; }

    public List<Guid> ServicePointIds { get; set; } = new();

    public bool IsTwoFactorEnabled { get; set; }

    [RequiredIf(nameof(Id), 0, ErrorMessage = "Yeni kullanıcı için şifre zorunludur")]
    [MinLength(6, ErrorMessage = "Şifre en az 6 karakter olmalıdır")]
    public string? Password { get; set; }

    public string? Notes { get; set; }
    public List<string> Permissions { get; set; } = new();

    public IEnumerable<ValidationResult> Validate(ValidationContext validationContext)
    {
        if ((Role == "Admin" || Role == "SuperAdmin") && CompanyId.HasValue)
        {
            yield return new ValidationResult(
                "Admin ve SuperAdmin kullanıcılarına firma atanamaz.",
                new[] { nameof(CompanyId) });
        }
    }
}

/// <summary>
/// Conditional <c>[Required]</c>: marks the property as required only when another
/// property on the same instance equals the supplied comparison value.
/// </summary>
/// <remarks>
/// Audit fix: was <c>public class</c> but is only consumed by <see cref="UserFormModel"/>
/// within the same file. Marked <c>internal sealed</c> to keep the framework's NuGet
/// contract narrow.
/// </remarks>
internal sealed class RequiredIfAttribute : ValidationAttribute
{
    private readonly string _comparisonProperty;
    private readonly object _comparisonValue;

    public RequiredIfAttribute(string comparisonProperty, object comparisonValue)
    {
        _comparisonProperty = comparisonProperty;
        _comparisonValue = comparisonValue;
    }

    protected override ValidationResult? IsValid(object? value, ValidationContext validationContext)
    {
        var property = validationContext.ObjectType.GetProperty(_comparisonProperty);
        if (property == null)
            throw new ArgumentException($"Property {_comparisonProperty} not found");

        var comparisonValue = property.GetValue(validationContext.ObjectInstance);

        if (Equals(comparisonValue, _comparisonValue))
        {
            if (value == null || string.IsNullOrWhiteSpace(value.ToString()))
            {
                return new ValidationResult(ErrorMessage ?? $"{validationContext.DisplayName} is required");
            }
        }

        return ValidationResult.Success;
    }
}
