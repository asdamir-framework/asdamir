// Copyright (C) 2026 Orhan Özşahin — Asdamir.
// Licensed under the GNU Lesser General Public License v3.0. See LICENSE.
// SPDX-License-Identifier: LGPL-3.0-or-later
//
// This file is part of the Asdamir open core. It is free software: you can redistribute it
// and/or modify it under the terms of the GNU Lesser General Public License as published by the
// Free Software Foundation, either version 3 of the License, or (at your option) any later
// version. It is distributed WITHOUT ANY WARRANTY; without even the implied warranty of
// MERCHANTABILITY or FITNESS FOR A PARTICULAR PURPOSE. See the GNU LGPL for more details.

namespace Asdamir.Core.Dtos;

public class OutboxDto
{
    public Guid Id { get; set; }
    public byte MessageType { get; set; } // 1=SMS, 2=Email
    public string Destination { get; set; } = string.Empty;
    public string? Cc { get; set; }
    public string? Subject { get; set; }
    public string? Body { get; set; }
    public string? SourceApp { get; set; }
    public byte Status { get; set; } // 0=Pending, 1=Sending, 2=Succeeded, 3=Failed, 4=Dead
    public string? CorrelationKey { get; set; }
    public Guid? CorrelationId { get; set; }
    public int TryCount { get; set; }
    public int MaxTryCount { get; set; }
    public string? LastError { get; set; }
    public DateTime CreatedAtUtc { get; set; }
    public DateTime UpdatedAtUtc { get; set; }
}
