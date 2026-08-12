// Copyright (C) 2026 Orhan Özşahin — Asdamir.
// Licensed under the GNU Lesser General Public License v3.0. See LICENSE.
// SPDX-License-Identifier: LGPL-3.0-or-later

using System.Security.Cryptography;
using Asdamir.Core.AgentAudit;
using FluentAssertions;
using Xunit;

namespace Asdamir.Core.Tests;

/// <summary>
/// The signing contract (Faz 3a): what a signature proves, what it does not, and the four states a verifier
/// may report.
///
/// <para><b>Why signatures are verified rather than compared.</b> ECDSA is randomized — the same body signed
/// twice yields different bytes — so a golden vector here cannot pin an expected signature string. That is a
/// consequence of choosing an in-box algorithm over Ed25519, and it is bounded: the signature sits OUTSIDE the
/// canonical body, so it affects neither the chain nor an archive's digest. Only this file changes shape.</para>
/// </summary>
public sealed class AgentSignatureTests
{
    private static AgentActionRecord Record(string action = "orders.ship") => new()
    {
        EventId = Guid.Parse("11111111-2222-3333-4444-555555555555"),
        TenantId = "default",
        OccurredAtUtc = new DateTime(2026, 8, 12, 9, 30, 0, DateTimeKind.Utc),
        AgentId = "agent.shipping",
        ActionType = action,
        Decision = AgentActionDecision.Allowed,
        Outcome = AgentActionOutcome.Success,
    };

    private static (EcdsaP256AgentActionSigner Signer, AgentSigningKey Key) NewKeyPair(
        string keyId = "k1",
        string agentId = "agent.shipping",
        DateTime? validFrom = null,
        DateTime? validTo = null,
        DateTime? revokedAt = null)
    {
        var ecdsa = ECDsa.Create(ECCurve.NamedCurves.nistP256);
        var key = new AgentSigningKey(
            keyId, agentId, AgentSignatureAlgorithms.EcdsaP256Sha256,
            ecdsa.ExportSubjectPublicKeyInfo(),
            validFrom ?? new DateTime(2020, 1, 1, 0, 0, 0, DateTimeKind.Utc),
            validTo, revokedAt);

        return (new EcdsaP256AgentActionSigner(keyId, ecdsa), key);
    }

    /// <summary>The signed body is built with a ZERO AppId — on BOTH sides, or nothing ever verifies.</summary>
    /// <remarks>
    /// This is the contract that has no shared constant to enforce it: the signer lives in the sink and the
    /// verifier's caller lives in the control plane, and neither can reference the other. So it is pinned
    /// here instead. Canonical field 2 is resolved from the ingest token, which an agent cannot know — signing
    /// it would mean shipping the id into every application's configuration, where one wrong entry makes every
    /// record report INVALID: a configuration typo wearing the costume of tampering.
    /// </remarks>
    [Fact]
    public void TheSignedBodyIsTheCanonicalPrefixWithAZeroAppId()
    {
        var record = Record();

        var zero = AgentActionCanonicalizer.BuildPrefix(Guid.Empty, record);
        var real = AgentActionCanonicalizer.BuildPrefix(Guid.Parse("2826118b-646e-4824-833b-6df8f903ded7"), record);

        zero.Should().NotEqual(real, "the AppId is genuinely part of the canonical body — which is exactly why "
            + "signing it would require the agent to know a value only the control plane assigns");

        var (signer, key) = NewKeyPair();
        using var _ = signer;
        var signature = signer.Sign(zero);

        new EcdsaP256AgentActionSignatureVerifier()
            .Verify(zero, signature, key, record.OccurredAtUtc)
            .Should().Be(AgentSignatureState.Valid);

        new EcdsaP256AgentActionSignatureVerifier()
            .Verify(real, signature, key, record.OccurredAtUtc)
            .Should().Be(
                AgentSignatureState.Invalid,
                "if the two sides ever disagree about the AppId they use, every signature fails — this is the "
                + "assertion that catches such a drift instead of a support ticket");
    }

    /// <summary>An honestly signed record verifies. Without this every refusal below would be vacuous.</summary>
    [Fact]
    public void AnHonestlySignedRecordVerifies()
    {
        var record = Record();
        var (signer, key) = NewKeyPair();
        using var _ = signer;

        var body = AgentActionCanonicalizer.BuildPrefix(Guid.Empty, record);

        new EcdsaP256AgentActionSignatureVerifier()
            .Verify(body, signer.Sign(body), key, record.OccurredAtUtc)
            .Should().Be(AgentSignatureState.Valid);
    }

    /// <summary>An unsigned record is valid — signing is optional, so earlier ledgers stay correct.</summary>
    [Fact]
    public void AnUnsignedRecordIsUnsignedAndNotInvalid()
    {
        var body = AgentActionCanonicalizer.BuildPrefix(Guid.Empty, Record());

        new EcdsaP256AgentActionSignatureVerifier()
            .Verify(body, null, null, DateTime.UtcNow)
            .Should().Be(
                AgentSignatureState.Unsigned,
                "backward compatibility is the point: a ledger written before Faz 3a is not retroactively "
                + "deficient, and 'no claim' must never be reported as 'a claim that failed'");
    }

    /// <summary>A body altered after signing is caught, even though the chain would catch it too.</summary>
    /// <remarks>
    /// Two independent detections on purpose. The hash chain covers the body and would notice; the signature
    /// covers it as well, from a different key. Either alone would be enough — together they mean an attacker
    /// needs both the database and the agent's private key.
    /// </remarks>
    [Fact]
    public void ABodyChangedAfterSigningIsInvalid()
    {
        var (signer, key) = NewKeyPair();
        using var _ = signer;

        var signed = AgentActionCanonicalizer.BuildPrefix(Guid.Empty, Record("orders.ship"));
        var signature = signer.Sign(signed);
        var tampered = AgentActionCanonicalizer.BuildPrefix(Guid.Empty, Record("orders.cancel"));

        new EcdsaP256AgentActionSignatureVerifier()
            .Verify(tampered, signature, key, DateTime.UtcNow)
            .Should().Be(AgentSignatureState.Invalid);
    }

    /// <summary>Editing only the signature column is caught — the chain cannot see it, so this must.</summary>
    /// <remarks>
    /// The signature columns are OUTSIDE the canonical body, so swapping them breaks no hash and the chain
    /// still reports Valid. This is the one detection that exists for that case, which is why it is asserted
    /// separately from the body-tampering test above rather than folded into it.
    /// </remarks>
    [Fact]
    public void AnEditedSignatureColumnIsInvalidEvenThoughTheChainCannotSeeIt()
    {
        var (signer, key) = NewKeyPair();
        using var _ = signer;

        var body = AgentActionCanonicalizer.BuildPrefix(Guid.Empty, Record());
        var real = signer.Sign(body);

        var flipped = (byte[])real.Value.Clone();
        flipped[0] ^= 0xFF;

        new EcdsaP256AgentActionSignatureVerifier()
            .Verify(body, real with { Value = flipped }, key, DateTime.UtcNow)
            .Should().Be(AgentSignatureState.Invalid);
    }

    /// <summary>A signature made by a different key does not verify.</summary>
    [Fact]
    public void ASignatureFromAnotherKeyIsInvalid()
    {
        var (signer, _) = NewKeyPair("k1");
        var (otherSigner, otherKey) = NewKeyPair("k2");
        using var a = signer;
        using var b = otherSigner;

        var body = AgentActionCanonicalizer.BuildPrefix(Guid.Empty, Record());

        new EcdsaP256AgentActionSignatureVerifier()
            .Verify(body, signer.Sign(body), otherKey, DateTime.UtcNow)
            .Should().Be(AgentSignatureState.Invalid);
    }

    /// <summary>A signed record whose key cannot be resolved is NOT reported as invalid.</summary>
    /// <remarks>
    /// The distinction this file exists to protect: "I could not check this" and "I checked it and it failed"
    /// are different facts. Collapsing them would make an unregistered key look like tampering — or, far
    /// worse, let real tampering hide behind a plausible registration gap.
    /// </remarks>
    [Fact]
    public void ASignedRecordWithNoResolvableKeyIsKeyUnavailable()
    {
        var (signer, _) = NewKeyPair();
        using var _d = signer;
        var body = AgentActionCanonicalizer.BuildPrefix(Guid.Empty, Record());

        new EcdsaP256AgentActionSignatureVerifier()
            .Verify(body, signer.Sign(body), null, DateTime.UtcNow)
            .Should().Be(AgentSignatureState.KeyUnavailable);
    }

    /// <summary>
    /// A record signed before its key was revoked stays verifiable; one signed after does not.
    /// </summary>
    /// <remarks>
    /// Revocation is judged against the SIGNING time. Treating it as retroactive would invalidate history that
    /// was honest when written, and would make a key compromise look as though the agent had been lying all
    /// along — which is both false and unusable for an investigation.
    /// </remarks>
    [Fact]
    public void RevocationIsJudgedAgainstTheSigningTimeNotAgainstNow()
    {
        var revokedAt = new DateTime(2026, 8, 12, 12, 0, 0, DateTimeKind.Utc);
        var (signer, key) = NewKeyPair(revokedAt: revokedAt);
        using var _ = signer;

        var body = AgentActionCanonicalizer.BuildPrefix(Guid.Empty, Record());
        var signature = signer.Sign(body);
        var verifier = new EcdsaP256AgentActionSignatureVerifier();

        verifier.Verify(body, signature, key, revokedAt.AddHours(-1))
            .Should().Be(AgentSignatureState.Valid, "it was signed while the key was still trusted");

        verifier.Verify(body, signature, key, revokedAt.AddHours(1))
            .Should().Be(
                AgentSignatureState.KeyUnavailable,
                "signed after revocation the key may not be used — and 'may not be used' is not the same as "
                + "'the signature is wrong', so it must not report Invalid");
    }

    /// <summary>Rotation: each record verifies against the key that actually signed it.</summary>
    [Fact]
    public void AfterRotationEachRecordVerifiesAgainstItsOwnKey()
    {
        var cutover = new DateTime(2026, 8, 12, 10, 0, 0, DateTimeKind.Utc);
        var (oldSigner, oldKey) = NewKeyPair("k-old", validTo: cutover);
        var (newSigner, newKey) = NewKeyPair("k-new", validFrom: cutover);
        using var a = oldSigner;
        using var b = newSigner;

        var body = AgentActionCanonicalizer.BuildPrefix(Guid.Empty, Record());
        var before = oldSigner.Sign(body);
        var after = newSigner.Sign(body);
        var verifier = new EcdsaP256AgentActionSignatureVerifier();

        before.KeyId.Should().Be("k-old", "the key id is recorded so the verifier never has to guess");
        after.KeyId.Should().Be("k-new");

        verifier.Verify(body, before, oldKey, cutover.AddHours(-1)).Should().Be(AgentSignatureState.Valid);
        verifier.Verify(body, after, newKey, cutover.AddHours(1)).Should().Be(AgentSignatureState.Valid);

        verifier.Verify(body, before, newKey, cutover.AddHours(-1)).Should().Be(
            AgentSignatureState.KeyUnavailable,
            "the new key is not yet valid at that instant — and being outside its window is not a failed check");
    }

    /// <summary>
    /// MULTI-VIOLATION: the record is signed with an unresolvable key AND the body has been altered.
    /// </summary>
    /// <remarks>
    /// A matrix of single violations cannot see a rule ORDER — whichever rule is broken is the one reported,
    /// whatever the sequence. Here both are broken at once, so the order becomes observable, and it is
    /// normative: key availability is decided BEFORE the signature is examined, because a verifier that has no
    /// key has performed no check and must not report a verdict on one.
    /// </remarks>
    [Fact]
    public void WhenTheKeyIsUnavailableAndTheBodyIsAlteredTheKeyIsReportedFirst()
    {
        var (signer, _) = NewKeyPair();
        using var _d = signer;

        var signature = signer.Sign(AgentActionCanonicalizer.BuildPrefix(Guid.Empty, Record("orders.ship")));
        var altered = AgentActionCanonicalizer.BuildPrefix(Guid.Empty, Record("orders.cancel"));

        new EcdsaP256AgentActionSignatureVerifier()
            .Verify(altered, signature, null, DateTime.UtcNow)
            .Should().Be(
                AgentSignatureState.KeyUnavailable,
                "reporting Invalid here would claim a check that never happened — the body may well be "
                + "altered, but nothing verified it, and the chain is what catches that case");
    }

    /// <summary>An algorithm this verifier does not implement is an unperformed check, not a failed one.</summary>
    [Fact]
    public void AnUnknownAlgorithmIsKeyUnavailableNotInvalid()
    {
        var (signer, key) = NewKeyPair();
        using var _ = signer;
        var body = AgentActionCanonicalizer.BuildPrefix(Guid.Empty, Record());
        var signature = signer.Sign(body) with { Algorithm = "ed25519" };

        new EcdsaP256AgentActionSignatureVerifier()
            .Verify(body, signature, key, DateTime.UtcNow)
            .Should().Be(AgentSignatureState.KeyUnavailable);
    }
}
