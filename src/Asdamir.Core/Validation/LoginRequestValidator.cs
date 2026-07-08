// Copyright (C) 2026 Orhan Özşahin — Asdamir.
// Licensed under the GNU Lesser General Public License v3.0. See LICENSE.
// SPDX-License-Identifier: LGPL-3.0-or-later
//
// This file is part of the Asdamir open core. It is free software: you can redistribute it
// and/or modify it under the terms of the GNU Lesser General Public License as published by the
// Free Software Foundation, either version 3 of the License, or (at your option) any later
// version. It is distributed WITHOUT ANY WARRANTY; without even the implied warranty of
// MERCHANTABILITY or FITNESS FOR A PARTICULAR PURPOSE. See the GNU LGPL for more details.

using Asdamir.Core.Dtos;
using FluentValidation;

namespace Asdamir.Core.Validation;

/// <summary>
/// FluentValidation rules for the login request — requires a well-formed email and a
/// non-empty password of at least six characters. Shape checks only; the authentication
/// decision lives elsewhere.
/// </summary>
public class LoginRequestValidator : AbstractValidator<LoginRequestDto>
{
    /// <summary>
    /// Registers the email and password rules for <see cref="LoginRequestDto"/>.
    /// </summary>
    public LoginRequestValidator()
    {
        RuleFor(x => x.Email).NotEmpty().EmailAddress();
        RuleFor(x => x.Password).NotEmpty().MinimumLength(6);
    }
}


