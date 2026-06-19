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



    public async Task WriteAsync(string source, string? correlationId, string payloadJson, string errorText, int attempts)
    {
        var errorMessages = new Dictionary<string, string> {
            { "tr", errorText },
            { "en", errorText },
            { "ru", errorText }
        };
        await WriteAsync(source, correlationId, payloadJson, errorMessages, attempts, null);
    }

    public async Task WriteAsync(string source, string? correlationId, string payloadJson, string errorText, int attempts, Dictionary<string, object>? metadata = null)
    {
        var errorMessages = new Dictionary<string, string> {
            { "tr", errorText },
            { "en", errorText },
            { "ru", errorText }
        };
        await WriteAsync(source, correlationId, payloadJson, errorMessages, attempts, metadata);
    }

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
