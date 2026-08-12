// Copyright (C) 2026 Orhan Özşahin — Asdamir.
// Licensed under the GNU Lesser General Public License v3.0. See LICENSE.
// SPDX-License-Identifier: LGPL-3.0-or-later

using System.Security.Cryptography;
using System.Text.Json;
using Asdamir.Core.AgentAudit;
using FluentAssertions;
using Xunit;

namespace Asdamir.Data.Tests;

/// <summary>
/// A signature survives the spool: written to disk when delivery fails, replayed later, byte-identical.
/// </summary>
/// <remarks>
/// <para>This is why the sink signs at RECORD time rather than at send time. The spool exists precisely
/// because delivery fails, and a record can wait on disk for minutes or days. Signing on the way out would
/// mean signing during REPLAY — with whatever key is in force then, which may have been rotated since the
/// action happened, attaching a signature whose key was not the one the agent held when it acted.</para>
/// <para>Because the signature is part of the record, the spool carries it with no code of its own. This test
/// pins that: a future change to the on-disk shape that silently dropped the field would leave every replayed
/// record unsigned, and nothing else would notice — an unsigned record is valid.</para>
/// </remarks>
public sealed class AgentAuditSignatureSpoolTests
{
    [Fact]
    public void ASignedRecordSurvivesTheSpoolRoundTripUnchanged()
    {
        using var ecdsa = ECDsa.Create(ECCurve.NamedCurves.nistP256);
        using var signer = new EcdsaP256AgentActionSigner("k-spool", ecdsa);

        var record = new AgentActionRecord
        {
            TenantId = "default",
            AgentId = "agent.spool",
            ActionType = "orders.ship",
            OccurredAtUtc = new DateTime(2026, 8, 12, 9, 0, 0, DateTimeKind.Utc),
        };

        var body = AgentActionCanonicalizer.BuildPrefix(Guid.Empty, record);
        var signature = signer.Sign(body);

        var signed = record with
        {
            SignatureAlgo = signature.Algorithm,
            Signature = signature.Value,
            SigningKeyId = signature.KeyId,
        };

        // The spool writes NDJSON through System.Text.Json; this is that round trip.
        var replayed = JsonSerializer.Deserialize<AgentActionRecord>(JsonSerializer.Serialize(signed))!;

        replayed.SignatureAlgo.Should().Be(AgentSignatureAlgorithms.EcdsaP256Sha256);
        replayed.SigningKeyId.Should().Be("k-spool");
        replayed.Signature.Should().Equal(signature.Value, "a replayed record must carry the ORIGINAL "
            + "signature — re-signing on replay would attest with a key that may have rotated since");

        var key = new AgentSigningKey(
            "k-spool", "agent.spool", AgentSignatureAlgorithms.EcdsaP256Sha256,
            ecdsa.ExportSubjectPublicKeyInfo(),
            new DateTime(2020, 1, 1, 0, 0, 0, DateTimeKind.Utc), null, null);

        new EcdsaP256AgentActionSignatureVerifier()
            .Verify(
                AgentActionCanonicalizer.BuildPrefix(Guid.Empty, replayed),
                new AgentActionSignature(replayed.SignatureAlgo!, replayed.SigningKeyId!, replayed.Signature!),
                key,
                replayed.OccurredAtUtc)
            .Should().Be(AgentSignatureState.Valid, "the signature must still verify after the round trip");
    }
}
