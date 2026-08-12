// Copyright (C) 2026 Orhan Özşahin — Asdamir.
// Licensed under the GNU Lesser General Public License v3.0. See LICENSE.
// SPDX-License-Identifier: LGPL-3.0-or-later
//
// This file is part of the Asdamir open core. It is free software: you can redistribute it
// and/or modify it under the terms of the GNU Lesser General Public License as published by the
// Free Software Foundation, either version 3 of the License, or (at your option) any later
// version. It is distributed WITHOUT ANY WARRANTY; without even the implied warranty of
// MERCHANTABILITY or FITNESS FOR A PARTICULAR PURPOSE. See the GNU LGPL for more details.

namespace Asdamir.Core.AgentAudit;

/// <summary>
/// Finds the public key a signature names.
/// </summary>
/// <remarks>
/// An interface rather than a concrete lookup because the key registry lives in the control plane's database,
/// which the open core does not and must not reference. Core verifies; something else knows where keys live.
/// </remarks>
public interface IAgentSigningKeyResolver
{
    /// <summary>Resolves one key.</summary>
    /// <param name="appId">The application the agent belongs to.</param>
    /// <param name="agentId">The agent named on the record.</param>
    /// <param name="keyId">The key id named on the signature.</param>
    /// <param name="ct">Cancellation token.</param>
    /// <returns>The key, or <see langword="null"/> when it is unknown — which is NOT the same as invalid.</returns>
    /// <remarks>
    /// Asynchronous because every real registry is a database. A synchronous shape would have forced the one
    /// implementation that matters to block on an async connection factory, which is how a deadlock gets
    /// introduced to save a keyword.
    /// </remarks>
    Task<AgentSigningKey?> FindAsync(Guid appId, string agentId, string keyId, CancellationToken ct = default);
}
