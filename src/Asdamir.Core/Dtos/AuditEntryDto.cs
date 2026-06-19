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

public record AuditEntryDto
{
    public DateTime Timestamp { get; init; }
    public string Action { get; init; } = string.Empty;
    public string Entity { get; init; } = string.Empty;
    public string? EntityId { get; init; }
    public string? UserId { get; init; }
    public string? UserName { get; init; }
    public string? TenantId { get; init; }
    public string? Ip { get; init; }
    public string? UserAgent { get; init; }
    public string? OldValuesJson { get; init; }
    public string? NewValuesJson { get; init; }
    public string? ExtraJson { get; init; }
    public string? Description { get; init; }
}
