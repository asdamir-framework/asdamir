// Copyright (C) 2026 Orhan Özşahin — Asdamir.
// Licensed under the GNU Lesser General Public License v3.0. See LICENSE.
// SPDX-License-Identifier: LGPL-3.0-or-later
//
// This file is part of the Asdamir open core. It is free software: you can redistribute it
// and/or modify it under the terms of the GNU Lesser General Public License as published by the
// Free Software Foundation, either version 3 of the License, or (at your option) any later
// version. It is distributed WITHOUT ANY WARRANTY; without even the implied warranty of
// MERCHANTABILITY or FITNESS FOR A PARTICULAR PURPOSE. See the GNU LGPL for more details.

using Microsoft.AspNetCore.Mvc;

namespace Asdamir.Core.ErrorHandling.Abstractions;

/// <summary>
/// Maps exceptions to ProblemDetails responses.
///
/// Audit fix (CRITICAL): v1 exposed a synchronous <c>Map</c> that, in turn, used
/// <c>Task.WaitAll</c> over three localizer calls — a sync-over-async deadlock pit on
/// the request thread. The mapper is now async; downstream middleware awaits it.
/// </summary>
public interface IProblemDetailsMapper
{
    /// <summary>
    /// Maps an exception to HTTP status code, error code, and ProblemDetails.
    /// </summary>
    Task<(int StatusCode, string ErrorCode, ProblemDetails ProblemDetails)> MapAsync(
        Exception exception,
        string traceId,
        CancellationToken cancellationToken = default);
}
