// Copyright (C) 2026 Orhan Özşahin — Asdamir.
// Licensed under the GNU Lesser General Public License v3.0. See LICENSE.
// SPDX-License-Identifier: LGPL-3.0-or-later
//
// This file is part of the Asdamir open core. It is free software: you can redistribute it
// and/or modify it under the terms of the GNU Lesser General Public License as published by the
// Free Software Foundation, either version 3 of the License, or (at your option) any later
// version. It is distributed WITHOUT ANY WARRANTY; without even the implied warranty of
// MERCHANTABILITY or FITNESS FOR A PARTICULAR PURPOSE. See the GNU LGPL for more details.

namespace Asdamir.Core.ErrorHandling.Abstractions;

/// <summary>
/// Interface for writing failed messages to Dead Letter Queue
/// </summary>
public interface IDlqWriter
{
    /// <summary>
    /// Writes a failed message to the Dead Letter Queue
    /// </summary>
    /// <param name="source">Source system or service that failed</param>
    /// <param name="correlationId">Correlation ID for tracing</param>
    /// <param name="payloadJson">JSON payload of the failed message</param>
    /// <param name="errorText">Error description</param>
    /// <param name="attempts">Number of retry attempts made</param>
    /// <returns>Task representing the async operation</returns>
    Task WriteAsync(string source, string? correlationId, string payloadJson, string errorText, int attempts);

    /// <summary>
    /// Writes a failed message to the Dead Letter Queue with additional metadata
    /// </summary>
    /// <param name="source">Source system or service that failed</param>
    /// <param name="correlationId">Correlation ID for tracing</param>
    /// <param name="payloadJson">JSON payload of the failed message</param>
    /// <param name="errorText">Error description</param>
    /// <param name="attempts">Number of retry attempts made</param>
    /// <param name="metadata">Additional metadata dictionary</param>
    /// <returns>Task representing the async operation</returns>
    Task WriteAsync(string source, string? correlationId, string payloadJson, string errorText, int attempts, Dictionary<string, object>? metadata = null);
}
