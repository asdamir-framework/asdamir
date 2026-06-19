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

// Audit fix: these are demonstration validators left in the framework for documentation
// purposes. They were `public class` and accidentally formed part of the NuGet contract —
// any caller could `new UserInputValidator()` and depend on the rules. Switched to
// `internal sealed` so they remain available for the documentation pipeline (XML doc + samples)
// without being a stable contract surface.

/// <summary>
/// Example validator for user input. Demo only — not part of the framework's public contract.
/// </summary>
internal sealed class UserInputValidator : BaseValidator<string>
{
    protected override void ConfigureRules()
    {
        RuleFor(x => x)
            .NotEmpty().WithMessage("Input cannot be empty")
            .MaximumLength(100).WithMessage("Input cannot exceed 100 characters");
    }
}

/// <summary>
/// Example validator for email addresses. Demo only — not part of the framework's public contract.
/// </summary>
internal sealed class EmailValidator : BaseValidator<string>
{
    protected override void ConfigureRules()
    {
        RuleFor(x => x)
            .NotEmpty().WithMessage("Email is required")
            .ValidEmail(blockRoleAddresses: true);
    }
}

/// <summary>
/// Example validator for numeric input. Demo only — not part of the framework's public contract.
/// </summary>
internal sealed class NumericValidator : BaseValidator<int>
{
    protected override void ConfigureRules()
    {
        RuleFor(x => x)
            .GreaterThan(0).WithMessage("Value must be greater than 0")
            .LessThanOrEqualTo(1000).WithMessage("Value must be less than or equal to 1000");
    }
}
