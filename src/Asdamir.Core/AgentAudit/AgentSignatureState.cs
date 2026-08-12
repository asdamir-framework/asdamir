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
/// What a verifier could establish about one record's signature.
/// </summary>
/// <remarks>
/// <para><b>Four states, not three, and the fourth is the point.</b> "I could not check this" and "I checked
/// it and it failed" are different facts about the world, and merging them is the failure mode this codebase
/// has a standing rule against — a skip is not a pass. <see cref="KeyUnavailable"/> means the record carries a
/// signature but the key it names could not be resolved (never registered, or registered and since revoked);
/// <see cref="Invalid"/> means the key WAS resolved and the signature did not verify against it.</para>
/// <para><see cref="Invalid"/> is the loud one. A record whose signature does not verify is <b>worse than an
/// unsigned record</b>: an unsigned record claims nothing, while a broken signature is a claim that failed.
/// No surface may present the two the same way.</para>
/// </remarks>
public enum AgentSignatureState
{
    /// <summary>The record carries no signature. Valid — signing is optional, for backward compatibility.</summary>
    Unsigned = 0,

    /// <summary>A key was resolved and the signature verified against it.</summary>
    Valid = 1,

    /// <summary>A key was resolved and the signature did NOT verify. Reported loudly and never as "unsigned".</summary>
    Invalid = 2,

    /// <summary>
    /// The record is signed but its key could not be resolved — unknown id, or a key that is revoked or
    /// outside its validity window. The signature was NOT checked; this is not a pass and not a failure.
    /// </summary>
    KeyUnavailable = 3,
}
