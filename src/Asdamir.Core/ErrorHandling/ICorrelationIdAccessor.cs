// Copyright (C) 2026 Orhan Özşahin — Asdamir.
// Licensed under the GNU Lesser General Public License v3.0. See LICENSE.
// SPDX-License-Identifier: LGPL-3.0-or-later
//
// This file is part of the Asdamir open core. It is free software: you can redistribute it
// and/or modify it under the terms of the GNU Lesser General Public License as published by the
// Free Software Foundation, either version 3 of the License, or (at your option) any later
// version. It is distributed WITHOUT ANY WARRANTY; without even the implied warranty of
// MERCHANTABILITY or FITNESS FOR A PARTICULAR PURPOSE. See the GNU LGPL for more details.

namespace Asdamir.Core.ErrorHandling.Http;

/// <summary>
/// DI-resolvable accessor for the current request's correlation id.
///
/// Set by <see cref="CorrelationIdMiddleware"/> at request entry, propagated to
/// downstream HTTP calls by <see cref="CorrelationIdForwardingHandler"/>.
///
/// Implementation is scoped — the same instance is shared by every service resolved
/// inside one HTTP request, so reads from anywhere in the request pipeline return
/// the same value.
/// </summary>
public interface ICorrelationIdAccessor
{
    /// <summary>The correlation id for the current request, or null before the middleware ran.</summary>
    string? CurrentId { get; }
}

internal interface ICorrelationIdMutator : ICorrelationIdAccessor
{
    new string? CurrentId { get; set; }
}

internal sealed class ScopedCorrelationIdAccessor : ICorrelationIdMutator
{
    public string? CurrentId { get; set; }
}
