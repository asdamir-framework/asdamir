// Copyright (C) 2026 Orhan Özşahin — Asdamir.
// Licensed under the GNU Lesser General Public License v3.0. See LICENSE.
// SPDX-License-Identifier: LGPL-3.0-or-later
//
// This file is part of the Asdamir open core. It is free software: you can redistribute it
// and/or modify it under the terms of the GNU Lesser General Public License as published by the
// Free Software Foundation, either version 3 of the License, or (at your option) any later
// version. It is distributed WITHOUT ANY WARRANTY; without even the implied warranty of
// MERCHANTABILITY or FITNESS FOR A PARTICULAR PURPOSE. See the GNU LGPL for more details.

using Asdamir.Core.ErrorHandling.Abstractions;
using Microsoft.Extensions.Logging;
using System.Text.Json;

namespace Asdamir.Core.ErrorHandling.Logging;

/// <summary>
/// File-based implementation of IDlqWriter
/// </summary>
public class FileDlqWriter : IDlqWriter
{
    private readonly string _dlqDirectory;
    private readonly ILogger<FileDlqWriter> _logger;

    /// <summary>
    /// Creates the writer against a required, explicit DLQ directory, creating that directory on disk
    /// if it does not yet exist. Throws when no directory is configured (the process cwd is not a safe
    /// default on IIS / Windows services).
    /// </summary>
    /// <param name="logger">Logger used to record each successful DLQ write and any write failure.</param>
    /// <param name="dlqDirectory">Absolute path of the directory where dead-letter JSON files are persisted; must be non-empty.</param>
    public FileDlqWriter(ILogger<FileDlqWriter> logger, string dlqDirectory)
    {
        // Audit fix: previously the parameter was optional and defaulted to
        // Path.Combine(Directory.GetCurrentDirectory(), "dlq"). On IIS / Windows services the
        // process cwd is often unexpected (System32, app pool root, etc.), so DLQ entries
        // could end up in surprising locations and silently miss in operations. We now require
        // an explicit absolute path from configuration.
        if (string.IsNullOrWhiteSpace(dlqDirectory))
            throw new ArgumentException("DLQ directory must be configured (non-empty).", nameof(dlqDirectory));

        _logger = logger;
        _dlqDirectory = dlqDirectory;

        Directory.CreateDirectory(_dlqDirectory);
    }



    /// <summary>
    /// Persists a failed message to the DLQ using a single error text, which is stored under every
    /// supported culture (tr/en/ru), with no extra metadata.
    /// </summary>
    /// <param name="source">Logical origin of the failed message (queue/worker/dispatcher name).</param>
    /// <param name="correlationId">Correlation id of the originating request, if any, for tracing.</param>
    /// <param name="payloadJson">Serialized payload of the message that could not be processed.</param>
    /// <param name="errorText">Human-readable failure reason, replicated across all cultures.</param>
    /// <param name="attempts">Number of processing attempts made before the message was dead-lettered.</param>
    /// <returns>A task that completes once the DLQ entry has been written to disk.</returns>
    public async Task WriteAsync(string source, string? correlationId, string payloadJson, string errorText, int attempts)
    {
        var errorMessages = new Dictionary<string, string> {
            { "tr", errorText },
            { "en", errorText },
            { "ru", errorText }
        };
        await WriteAsync(source, correlationId, payloadJson, errorMessages, attempts, null);
    }

    /// <summary>
    /// Persists a failed message to the DLQ using a single error text (replicated across all cultures),
    /// together with optional structured metadata attached to the entry.
    /// </summary>
    /// <param name="source">Logical origin of the failed message (queue/worker/dispatcher name).</param>
    /// <param name="correlationId">Correlation id of the originating request, if any, for tracing.</param>
    /// <param name="payloadJson">Serialized payload of the message that could not be processed.</param>
    /// <param name="errorText">Human-readable failure reason, replicated across all cultures.</param>
    /// <param name="attempts">Number of processing attempts made before the message was dead-lettered.</param>
    /// <param name="metadata">Optional extra key/value context stored alongside the entry.</param>
    /// <returns>A task that completes once the DLQ entry has been written to disk.</returns>
    public async Task WriteAsync(string source, string? correlationId, string payloadJson, string errorText, int attempts, Dictionary<string, object>? metadata = null)
    {
        var errorMessages = new Dictionary<string, string> {
            { "tr", errorText },
            { "en", errorText },
            { "ru", errorText }
        };
        await WriteAsync(source, correlationId, payloadJson, errorMessages, attempts, metadata);
    }

    /// <summary>
    /// Core write: serializes the failure as a single timestamped, uniquely named JSON file in the DLQ
    /// directory, carrying per-culture error messages and optional metadata. Any write failure is logged
    /// and rethrown so the caller can react (rather than silently losing the entry).
    /// </summary>
    /// <param name="source">Logical origin of the failed message (queue/worker/dispatcher name).</param>
    /// <param name="correlationId">Correlation id of the originating request, if any, for tracing.</param>
    /// <param name="payloadJson">Serialized payload of the message that could not be processed.</param>
    /// <param name="errorMessages">Failure reason keyed by culture (e.g. tr/en/ru).</param>
    /// <param name="attempts">Number of processing attempts made before the message was dead-lettered.</param>
    /// <param name="metadata">Optional extra key/value context stored alongside the entry.</param>
    /// <returns>A task that completes once the DLQ entry has been written to disk.</returns>
    public async Task WriteAsync(string source, string? correlationId, string payloadJson, Dictionary<string, string> errorMessages, int attempts, Dictionary<string, object>? metadata = null)
    {
        try
        {
            var dlqEntry = new DlqEntry
            {
                Id = Guid.NewGuid().ToString("N"),
                Source = source,
                CorrelationId = correlationId,
                PayloadJson = payloadJson,
                ErrorMessages = errorMessages,
                Attempts = attempts,
                Timestamp = DateTime.UtcNow,
                Metadata = metadata ?? new Dictionary<string, object>()
            };

            var fileName = $"dlq_{DateTime.UtcNow:yyyyMMdd}_{dlqEntry.Id}.json";
            var filePath = Path.Combine(_dlqDirectory, fileName);

            var json = JsonSerializer.Serialize(dlqEntry, new JsonSerializerOptions
            {
                WriteIndented = true,
                PropertyNamingPolicy = JsonNamingPolicy.CamelCase
            });

            await File.WriteAllTextAsync(filePath, json);

            _logger.LogWarning("Message written to DLQ. File: {FileName}, Source: {Source}, CorrelationId: {CorrelationId}", 
                fileName, source, correlationId);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Failed to write message to DLQ. Source: {Source}, CorrelationId: {CorrelationId}", 
                source, correlationId);
            throw;
        }
    }

    // Eski overload kaldırıldı. Sadece çok dilli dictionary ile kullanılacak.

    private class DlqEntry
    {
        public string Id { get; set; } = string.Empty;
        public string Source { get; set; } = string.Empty;
        public string? CorrelationId { get; set; }
        public string PayloadJson { get; set; } = string.Empty;
        public Dictionary<string, string> ErrorMessages { get; set; } = new();
        public int Attempts { get; set; }
        public DateTime Timestamp { get; set; }
        public Dictionary<string, object> Metadata { get; set; } = new();
    }
}
