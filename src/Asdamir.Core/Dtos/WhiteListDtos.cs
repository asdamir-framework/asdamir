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

/// <summary>
/// Request model for inserting a phone number to SMS WhiteList
/// </summary>
public class SendInsertWhiteListRequest
{
    /// <summary>
    /// Phone number to add to whitelist (required)
    /// </summary>
    public string Phone { get; set; } = string.Empty;

    /// <summary>
    /// Email address (optional)
    /// </summary>
    public string? Email { get; set; }

    /// <summary>
    /// User who created the whitelist entry
    /// </summary>
    public string? CreateUser { get; set; }
}

/// <summary>
/// Response model for WhiteList insert operation
/// </summary>
public class InsertWhiteListResponse
{
    /// <summary>
    /// Result status of the insert operation
    /// </summary>
    public InsertWhiteListResult Result { get; set; }

    /// <summary>
    /// Message describing the result
    /// </summary>
    public string Message { get; set; } = string.Empty;
}

/// <summary>
/// Result codes for WhiteList insert operation
/// </summary>
public enum InsertWhiteListResult
{
    /// <summary>
    /// Successfully created in whitelist
    /// </summary>
    Created = 0,

    /// <summary>
    /// Phone number already exists in whitelist
    /// </summary>
    AlreadyExists = 1,

    /// <summary>
    /// Operation failed
    /// </summary>
    Failed = 2
}

/// <summary>
/// Request model for deactivating a phone number from SMS WhiteList
/// </summary>
public class DeactivateWhiteListRequest
{
    /// <summary>
    /// Phone number to deactivate from whitelist (required)
    /// </summary>
    public string Phone { get; set; } = string.Empty;

    /// <summary>
    /// User who updated/deactivated the whitelist entry
    /// </summary>
    public string? UpdateUser { get; set; }
}
