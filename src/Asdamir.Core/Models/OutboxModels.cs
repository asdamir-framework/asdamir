// Copyright (C) 2026 Orhan Özşahin — Asdamir.
// Licensed under the GNU Lesser General Public License v3.0. See LICENSE.
// SPDX-License-Identifier: LGPL-3.0-or-later
//
// This file is part of the Asdamir open core. It is free software: you can redistribute it
// and/or modify it under the terms of the GNU Lesser General Public License as published by the
// Free Software Foundation, either version 3 of the License, or (at your option) any later
// version. It is distributed WITHOUT ANY WARRANTY; without even the implied warranty of
// MERCHANTABILITY or FITNESS FOR A PARTICULAR PURPOSE. See the GNU LGPL for more details.


namespace Asdamir.Core.Models;

public enum OutboxMessageType : byte { Sms = 1, Email = 2 }
public enum OutboxStatus : byte { Pending = 0, Sending = 1, Succeeded = 2, Failed = 3, Dead = 4 }

public sealed class OutboxOptions
{
    public string ConnectionString { get; set; } = string.Empty;
    public string WorkerName { get; set; } = "outbox-worker";
    public int BatchSize { get; set; } = 25;
}

public sealed class OutboxItem
{
    public Guid Id { get; set; }
    public OutboxMessageType MessageType { get; set; }

    /// <summary>To recipients. One or more addresses separated by ';' or ','.</summary>
    public string Destination { get; set; } = string.Empty;

    /// <summary>Optional CC recipients. One or more addresses separated by ';' or ','.</summary>
    public string? Cc { get; set; }

    public string? Subject { get; set; }
    public string Body { get; set; } = string.Empty;
}

public sealed class OutboxMessage
{
    public Guid? Id { get; set; }
    public OutboxMessageType MessageType { get; set; }

    /// <summary>To recipients. One or more addresses separated by ';' or ','.</summary>
    public string Destination { get; set; } = string.Empty;

    /// <summary>Optional CC recipients. One or more addresses separated by ';' or ','.</summary>
    public string? Cc { get; set; }

    public string? Subject { get; set; }
    public string Body { get; set; } = string.Empty;
    public string? SourceApp { get; set; }
    public string? CorrelationKey { get; set; }
    public Guid? CorrelationId { get; set; }
    public string? DedupKey { get; set; }
}
