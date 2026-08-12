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
/// How the acting agent came by its authority for a single action — the difference between "a human asked
/// for this", "the agent decided on its own" and "a schedule fired".
/// </summary>
public enum AgentAuthorityKind
{
    /// <summary>A human delegated the authority; <see cref="AgentActionRecord.OnBehalfOfUserId"/> names them.</summary>
    Delegated = 0,

    /// <summary>The agent acted on its own initiative, with no human in the loop for this action.</summary>
    Autonomous = 1,

    /// <summary>A schedule or timer triggered the action (no human and no agent initiative at call time).</summary>
    Scheduled = 2,
}

/// <summary>
/// The authorization verdict that was reached BEFORE the action ran — recorded so a denial leaves a trace
/// just as durable as a success.
/// </summary>
public enum AgentActionDecision
{
    /// <summary>The action was permitted and proceeded.</summary>
    Allowed = 0,

    /// <summary>The action was refused; nothing was carried out.</summary>
    Denied = 1,

    /// <summary>
    /// The action needs human approval before it may proceed. RESERVED for phase 2: recording this value is
    /// supported today, but the framework ships NO approval flow behind it — nothing waits, blocks or notifies.
    /// </summary>
    RequiresApproval = 2,
}

/// <summary>What actually happened once the action ran.</summary>
public enum AgentActionOutcome
{
    /// <summary>The action completed as intended.</summary>
    Success = 0,

    /// <summary>The action failed; see <see cref="AgentActionRecord.ErrorCode"/>.</summary>
    Failure = 1,

    /// <summary>The action completed for some targets and failed for others.</summary>
    Partial = 2,
}

/// <summary>
/// One action taken by an AI agent (or any other non-human actor), as submitted to the tamper-evident agent
/// action ledger through <see cref="IAgentActionAuditor"/>.
///
/// <para><b>What this record is, and is not.</b> The identity fields (<see cref="AgentId"/>,
/// <see cref="OnBehalfOfUserId"/>) are the APPLICATION'S ASSERTION about who acted. The framework makes that
/// assertion tamper-EVIDENT — once recorded it cannot be changed without breaking a hash chain — but it does
/// NOT make it self-proving: no agent presents a cryptographic identity of its own in this version. Do not
/// describe the guarantee more strongly than "application assertion + tamper-evident record".</para>
///
/// <para><b>Content never enters the ledger — only digests do.</b> There is no payload field, by design:
/// supply <see cref="InputDigest"/> / <see cref="OutputDigest"/> (SHA-256 of the content you are binding to
/// the chain) and keep the content itself in your own store. That way a redaction or an erasure request can
/// never break the chain.</para>
///
/// <para><b>Two fields are structurally constrained</b> because they are inside the hash and therefore
/// permanently unredactable: <see cref="TargetId"/> and <see cref="ErrorCode"/> accept opaque/structural
/// values only (see each property). Free-text error detail belongs in <see cref="ErrorMessage"/>, which is
/// stored OUTSIDE the hash and stays redactable.</para>
///
/// <para>The scoping <c>AppId</c> is deliberately absent: the server resolves it from the ingest service
/// token, never from the payload, so an app cannot write into another app's chain.</para>
/// </summary>
public sealed record AgentActionRecord
{
    /// <summary>
    /// Client-generated identity for this event, used for at-least-once ingest idempotency: re-submitting the
    /// same id into the same chain returns the original row instead of appending a second one. Keep it stable
    /// across retries of the SAME action — a fresh id per retry would record the action twice.
    /// </summary>
    public Guid EventId { get; init; } = Guid.NewGuid();

    /// <summary>
    /// The tenant whose chain this record joins (max 64 characters). Each (application, tenant) pair is an
    /// independent, gapless chain. Use <c>"default"</c> for a single-tenant application.
    /// </summary>
    public string TenantId { get; init; } = "default";

    /// <summary>
    /// When the action happened, per the CLIENT's clock — informational only. The authoritative, hashed
    /// timestamp is the server's, assigned when the row is appended. Non-UTC values are converted to UTC.
    /// </summary>
    public DateTime OccurredAtUtc { get; init; } = DateTime.UtcNow;

    /// <summary>The logical identity of the acting agent (max 200 characters), e.g. <c>"asdamir.recon.matcher"</c>.</summary>
    public required string AgentId { get; init; }

    /// <summary>The agent's own version (max 100 characters); null when the agent is unversioned.</summary>
    public string? AgentVersion { get; init; }

    /// <summary>
    /// The model and version behind the action (max 200 characters), e.g. <c>"claude-opus-5-20260501"</c>.
    /// This is the traceability anchor when a model change turns out to explain a change in behaviour —
    /// record it whenever a model was involved.
    /// </summary>
    public string? ModelId { get; init; }

    /// <summary>How the agent came by its authority for this action.</summary>
    public AgentAuthorityKind AuthorityKind { get; init; } = AgentAuthorityKind.Delegated;

    /// <summary>The user who delegated the authority; null for autonomous or scheduled actions.</summary>
    public int? OnBehalfOfUserId { get; init; }

    /// <summary>
    /// Display name of the delegating user (max 256 characters). Stored but deliberately NOT hashed, so it
    /// stays redactable; <see cref="OnBehalfOfUserId"/> is the hashed identity.
    /// </summary>
    public string? OnBehalfOfUserName { get; init; }

    /// <summary>The agent session this action belongs to (max 200 characters); null when not applicable.</summary>
    public string? SessionId { get; init; }

    /// <summary>The single agent invocation this action belongs to (max 200 characters); null when not applicable.</summary>
    public string? InvocationId { get; init; }

    /// <summary>
    /// The request correlation id (max 64 characters), matching the framework's correlation-id convention, so a
    /// ledger row can be joined to the operational logs of the same request.
    /// </summary>
    public string? CorrelationId { get; init; }

    /// <summary>
    /// What the agent did, as <c>&lt;area&gt;.&lt;action&gt;</c> (max 200 characters), e.g. <c>"orders.approve"</c>
    /// — the same shape as the framework's permission names.
    /// </summary>
    public required string ActionType { get; init; }

    /// <summary>The type of entity acted upon (max 128 characters), e.g. <c>"Order"</c>; null when not applicable.</summary>
    public string? TargetType { get; init; }

    /// <summary>
    /// The entity acted upon, as an OPAQUE or SURROGATE identifier: at most 128 characters drawn from
    /// <c>[0-9 A-Z a-z . _ : / -]</c>. A value outside that set is rejected at write time.
    ///
    /// <para>The restriction makes the field structurally incapable of holding free text — no spaces, no
    /// <c>@</c>, no commas — because the field is inside the hash and can therefore never be redacted. It does
    /// NOT and cannot detect personal data: a national id or a phone number fits the character set perfectly
    /// well. Choosing a surrogate key over a natural one remains the caller's responsibility.</para>
    /// </summary>
    public string? TargetId { get; init; }

    /// <summary>The authorization verdict reached before the action ran.</summary>
    public AgentActionDecision Decision { get; init; } = AgentActionDecision.Allowed;

    /// <summary>What happened when the action ran.</summary>
    public AgentActionOutcome Outcome { get; init; } = AgentActionOutcome.Success;

    /// <summary>
    /// SHA-256 (exactly 32 bytes) of the action's input, binding the input to the chain without storing it;
    /// null when there is nothing to bind.
    /// </summary>
    public byte[]? InputDigest { get; init; }

    /// <summary>SHA-256 (exactly 32 bytes) of the action's output; null when there is nothing to bind.</summary>
    public byte[]? OutputDigest { get; init; }

    /// <summary>
    /// A machine-readable error code (max 64 characters from <c>[0-9 A-Z a-z . _ -]</c>), e.g.
    /// <c>"orders.insufficient_stock"</c> or <c>"HTTP_502"</c>. Rejected at write time if it contains anything
    /// else — the readable sentence belongs in <see cref="ErrorMessage"/>, whose SHA-256 goes in
    /// <see cref="ErrorDigest"/>.
    /// </summary>
    public string? ErrorCode { get; init; }

    /// <summary>
    /// SHA-256 (exactly 32 bytes) of the FULL error message. Binds the error's content to the chain while the
    /// text itself stays outside it and remains redactable.
    /// </summary>
    public byte[]? ErrorDigest { get; init; }

    /// <summary>
    /// The human-readable error text. Stored but NOT hashed: it can be redacted later without breaking the
    /// chain, at the cost of <see cref="ErrorDigest"/> no longer matching the (now removed) text — which is
    /// the intended trade.
    /// </summary>
    public string? ErrorMessage { get; init; }

    // ─── Signature (Faz 3a) — all three are OUTSIDE the canonical body ──────────────────────────────
    //
    // Canonicalization v1 is frozen and these fields are not part of it, so signing changes no hash and needs
    // no new HashVersion. The consequence is worth stating rather than discovering: because the chain does
    // not cover them, a stripped signature does not break the chain. Seeing that is the verifier's job — it
    // reports a record left unsigned while its agent held a usable key.

    /// <summary>
    /// The algorithm the signature was produced with — see <see cref="AgentSignatureAlgorithms"/>. Null on an
    /// unsigned record, which is valid: signing is optional.
    /// </summary>
    public string? SignatureAlgo { get; init; }

    /// <summary>
    /// The signature over this record's canonical prefix, or null when unsigned. Stored but NOT hashed.
    /// </summary>
    public byte[]? Signature { get; init; }

    /// <summary>
    /// Which registered key produced <see cref="Signature"/>. Recorded so a verifier checks against the key
    /// that actually signed, instead of trying every key the agent has ever held — which would leave a
    /// revoked key still verifying, and make revocation a formality.
    /// </summary>
    public string? SigningKeyId { get; init; }
}
