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
#nullable enable

namespace Asdamir.Core.Validation;

/// <summary>
/// Enriches ValidationResult with additional metadata (e.g., error codes, property mapping)
/// </summary>
public interface IValidationResultEnricher
{
    /// <summary>
    /// Post-processes a validation result — e.g. logs the failures or attaches error codes /
    /// localized-message keys — after the validator has run.
    /// </summary>
    /// <typeparam name="T">The validated model type, used for context in the enrichment.</typeparam>
    /// <param name="result">The result to enrich; typically only acted on when invalid.</param>
    void Enrich<T>(ValidationResult result);
}
