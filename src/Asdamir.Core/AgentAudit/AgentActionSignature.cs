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
/// An agent's signature over a record's canonical body, plus the identity of the key that produced it.
/// </summary>
/// <remarks>
/// <para><b>What a signature here proves, stated exactly.</b> It proves that the holder of the named private
/// key signed this canonical body — that the agent MEANT TO SEND this record. It does <b>not</b> prove that
/// the key was not stolen, that the agent's work was correct, or that the record occupies any particular
/// position in the chain. A compromised key produces signatures that verify perfectly.</para>
/// <para>The signature covers the canonical PREFIX, which excludes <c>SeqNo</c> and the server-assigned
/// <c>RecordedAtUtc</c> — the two fields only the control plane can know. Signing the row hash instead would
/// prove "the agent endorsed this chain position", but it would require a round trip AND a second write to a
/// table whose immutability trigger has no bypass.</para>
/// </remarks>
/// <param name="Algorithm">The algorithm identifier — see <see cref="AgentSignatureAlgorithms"/>.</param>
/// <param name="KeyId">Which key signed. Recorded so rotation does not force a verifier to try every key
/// an agent has ever held, which would make revocation meaningless.</param>
/// <param name="Value">The raw signature bytes.</param>
public sealed record AgentActionSignature(string Algorithm, string KeyId, byte[] Value);
