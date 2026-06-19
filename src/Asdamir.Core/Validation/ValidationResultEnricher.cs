// Copyright (C) 2026 Orhan Özşahin — Asdamir.
// Licensed under the GNU Lesser General Public License v3.0. See LICENSE.
// SPDX-License-Identifier: LGPL-3.0-or-later
//
// This file is part of the Asdamir open core. It is free software: you can redistribute it
// and/or modify it under the terms of the GNU Lesser General Public License as published by the
// Free Software Foundation, either version 3 of the License, or (at your option) any later
// version. It is distributed WITHOUT ANY WARRANTY; without even the implied warranty of
// MERCHANTABILITY or FITNESS FOR A PARTICULAR PURPOSE. See the GNU LGPL for more details.

using FluentValidation.Results;
using Microsoft.Extensions.Logging;

namespace Asdamir.Core.Validation;

/// <summary>
/// Default enricher that logs property names and error codes; extend as needed.
/// </summary>
public class ValidationResultEnricher : IValidationResultEnricher
{
    private readonly ILogger<ValidationResultEnricher> _logger;

    public ValidationResultEnricher(ILogger<ValidationResultEnricher> logger)
    {
        _logger = logger;
    }

    public void Enrich<T>(ValidationResult result)
    {
        if (result.IsValid) return;

        var messages = result.Errors.Select(e =>
            string.IsNullOrWhiteSpace(e.ErrorCode)
                ? $"{e.PropertyName}:{e.ErrorMessage}"
                : $"{e.PropertyName}:{e.ErrorCode}:{e.ErrorMessage}");

        _logger.LogWarning("Validation failed for {Type}. Details: {Details}",
            typeof(T).Name,
            string.Join("; ", messages));
    }
}
