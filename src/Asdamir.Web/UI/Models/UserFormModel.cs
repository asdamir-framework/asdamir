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

/// <summary>
/// Edit/create form model for the user-management screen: the bound fields plus their
/// validation intent. Backs both "new user" and "edit user" flows (the two are told apart
/// by <see cref="Id"/> being <c>0</c>), and carries a cross-field rule via
/// <see cref="IValidatableObject"/>.
/// </summary>
public class UserFormModel : IValidatableObject
{
    /// <summary>Persisted user identifier; <c>0</c> marks a not-yet-saved (new) user and drives the "password required" rule.</summary>
    public int Id { get; set; }

    /// <summary>Login e-mail; required and must be a syntactically valid address.</summary>
    [Required(ErrorMessage = "Email adresi zorunludur")]
    [EmailAddress(ErrorMessage = "Geçerli bir email adresi giriniz")]
    public string Email { get; set; } = string.Empty;

    /// <summary>Display name; required, minimum two characters.</summary>
    [Required(ErrorMessage = "İsim alanı zorunludur")]
    [MinLength(2, ErrorMessage = "İsim en az 2 karakter olmalıdır")]
    public string Name { get; set; } = string.Empty;

    /// <summary>Assigned RBAC role (e.g. <c>User</c>, <c>Admin</c>, <c>SuperAdmin</c>); required, defaults to <c>User</c>.</summary>
    [Required(ErrorMessage = "Rol seçimi zorunludur")]
    public string Role { get; set; } = "User";

    /// <summary>Whether the account is enabled; disabled users cannot sign in. Defaults to active.</summary>
    public bool IsActive { get; set; } = true;

    /// <summary>Optional phone number; when present must be E.164-formatted (e.g. <c>+905XXXXXXXXX</c>).</summary>
    [Phone(ErrorMessage = "Geçerli bir telefon numarası giriniz")]
    [RegularExpression(@"^\+?[1-9]\d{1,14}$", ErrorMessage = "Uluslararası telefon formatı: +905XXXXXXXXX")]
    public string? PhoneNumber { get; set; }

    /// <summary>Whether the phone number has been confirmed (e.g. via an SMS code).</summary>
    public bool PhoneNumberVerified { get; set; }

    /// <summary>Company (firma) the user belongs to; must be empty for <c>Admin</c>/<c>SuperAdmin</c> (see <see cref="Validate"/>).</summary>
    public Guid? CompanyId { get; set; }

    /// <summary>Display name of the selected company; presentation-only companion to <see cref="CompanyId"/>.</summary>
    public string? CompanyName { get; set; }

    /// <summary>Service points the user is scoped to; empty means no restriction.</summary>
    public List<Guid> ServicePointIds { get; set; } = new();

    /// <summary>Whether two-factor authentication is enabled for the account.</summary>
    public bool IsTwoFactorEnabled { get; set; }

    /// <summary>Password; required only when creating a new user (<see cref="Id"/> == 0) and then at least six characters.</summary>
    [RequiredIf(nameof(Id), 0, ErrorMessage = "Yeni kullanıcı için şifre zorunludur")]
    [MinLength(6, ErrorMessage = "Şifre en az 6 karakter olmalıdır")]
    public string? Password { get; set; }

    /// <summary>Free-text administrative notes about the user.</summary>
    public string? Notes { get; set; }

    /// <summary>Explicit per-user permission grants layered on top of the role.</summary>
    public List<string> Permissions { get; set; } = new();

    /// <summary>
    /// Cross-field validation: an <c>Admin</c>/<c>SuperAdmin</c> user may not be assigned to a company.
    /// </summary>
    /// <param name="validationContext">The context describing the object being validated.</param>
    /// <returns>Any validation failures; empty when the model is valid.</returns>
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
