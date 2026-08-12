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
/// Signs a record's canonical body on the AGENT's side.
/// </summary>
/// <remarks>
/// The private key never reaches the control plane. A design in which the server could sign would empty the
/// claim it is meant to support: "the agent signed this" means nothing if the party storing the record can
/// produce the signature itself.
/// </remarks>
public interface IAgentActionSigner
{
    /// <summary>Signs one canonical prefix.</summary>
    /// <param name="canonicalPrefix">The canonical body as produced by the canonicalizer.</param>
    /// <returns>The signature and the key that produced it.</returns>
    AgentActionSignature Sign(ReadOnlySpan<byte> canonicalPrefix);
}
