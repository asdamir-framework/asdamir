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

/// <summary>Credentials posted to the login endpoint.</summary>
/// <param name="Email">Account email used as the login identifier.</param>
/// <param name="Password">Plaintext password (sent over TLS); verified against the stored PBKDF2 hash.</param>
public record LoginRequestDto(string Email, string Password);

/// <summary>Token pair returned on successful login or refresh.</summary>
/// <param name="AccessToken">Short-lived signed JWT presented as the bearer credential on API calls.</param>
/// <param name="RefreshToken">Long-lived rotating token exchanged for a new access token when it expires.</param>
/// <param name="ExpiresAtUtc">UTC instant at which the access token stops being valid.</param>
/// <param name="RefreshExpiresAtUtc">UTC instant at which the refresh token stops being valid.</param>
public record TokenResponseDto(string AccessToken, string RefreshToken, DateTime ExpiresAtUtc, DateTime RefreshExpiresAtUtc);

/// <summary>Identity and effective permissions of the currently authenticated user (the "who am I" response).</summary>
/// <param name="UserId">Identifier of the authenticated user.</param>
/// <param name="Email">User's email address.</param>
/// <param name="Name">User's display name.</param>
/// <param name="Permissions">Flattened permission keys the user holds, used for client-side authorization checks.</param>
/// <param name="Role">Primary role name, when a single role applies; null if unset.</param>
public record MeResponseDto(int UserId, string Email, string Name, List<string> Permissions, string? Role = null);