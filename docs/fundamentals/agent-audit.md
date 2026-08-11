# Agent Audit

**Packages:** `Asdamir.Core` (contracts, `AgentActionRecord`), `Asdamir.Data` (the client sink, `AddAgentAudit`)

## Introduction

Agent audit records what an AI agent — or any other non-human actor — actually did, in a form that can be
**checked later by a machine**. Ordinary audit logging answers "what happened?". This answers a different and
harder question: **"can you show that this record has not been changed since it was written?"**

That distinction is the entire point. A log you can quietly edit is a log you cannot produce as evidence. Every
record here joins a hash chain, so changing, deleting, reordering or inserting any row breaks the chain from
that point on, and verification reports the exact position where it broke.

## What the guarantee is — and what it is not

Read this section before you rely on the feature for anything.

**What it gives you.** A tamper-EVIDENT record. Rows are append-only, chained by SHA-256, and enforced by three
independent layers: a permission-level `DENY`, `INSTEAD OF UPDATE`/`DELETE` triggers, and the hash chain itself.

**What it does not give you.** It is evidence, not prevention. A principal with `db_owner` or `sysadmin` rights
can disable a trigger and edit a row — nothing inside a single database can stop that. What they cannot do is
make the chain agree afterwards. Never state the guarantee more strongly than that.

**It does not prove WHO acted.** `AgentId` and `OnBehalfOfUserId` are the **application's assertion**, recorded
tamper-evidently. No agent presents a cryptographic identity of its own in this version. The assurance level is
therefore:

> **application assertion + tamper-evident record** — *not* "the agent cryptographically proved its own
> identity".

Per-agent cryptographic identity, signatures and PKI are a later phase. The `HashVersion` column is live and in
use; `SignatureAlgo` and `Signature` are reserved and always `NULL` today.

## Recording an action

```csharp
builder.Services.AddAgentAudit(builder.Configuration, controlPlaneBaseAddress);
```

```csharp
await auditor.RecordAsync(new AgentActionRecord
{
    AgentId       = "asdamir.recon.matcher",
    AgentVersion  = "3.11.2",
    ModelId       = "claude-opus-5-20260501",   // the traceability anchor when behaviour changes
    AuthorityKind = AgentAuthorityKind.Delegated,
    OnBehalfOfUserId = currentUserId,
    ActionType    = "orders.approve",
    TargetType    = "Order",
    TargetId      = "ORD-2026-000913",          // opaque identifier ONLY
    Decision      = AgentActionDecision.Allowed,
    Outcome       = AgentActionOutcome.Success,
    InputDigest   = SHA256.HashData(inputBytes),
    OutputDigest  = SHA256.HashData(outputBytes),
});
```

`RecordAsync` hands the record to a bounded queue and returns, so a slow control plane never slows down the
action being audited. Completion of the call is **not** a guarantee that the row is durable yet — see
[When delivery fails](#when-delivery-fails).

The record carries **no AppId**. The server resolves it from the ingest service token, so an application can
only ever append to its own chain.

> **What "AppId-scoped" does and does not mean.** `AppId` is the **scoping field** — it partitions the ledger
> into one chain per `(AppId, TenantId)` and the server, not the caller, decides its value. It is **not
> enforced by a foreign key**: `dbo.AgentActionLedger.AppId` has no FK to `dbo.Apps`, so an id that does not
> resolve is not rejected at write time; the read path (`AgentActionLedger_List`) `LEFT JOIN`s `dbo.Apps`, so
> such a row simply shows a blank application name instead of failing. The scoping guarantee comes from the
> token, not from referential integrity. Adding the FK is being evaluated for a later phase.

## What is hashed, and what is not

This table is the honest boundary of the guarantee. Everything in the left column is covered by the chain;
everything in the right column is stored but **not** protected by it, and can be changed or removed without
breaking anything.

| Hashed (immutable, unredactable) | NOT hashed (stored, redactable) |
|---|---|
| `HashVersion`, `AppId`, `TenantId`, `SeqNo`, `RecordKind`, `EventId` | `OnBehalfOfUserName` — mutable display data |
| `OccurredAtUtc`, `RecordedAtUtc` | `ErrorMessage` — free-text error detail |
| `AgentId`, `AgentVersion`, `ModelId` | `FoldSeqStart/End/RowCount/SegmentDigest/ArchiveRef` |
| `AuthorityKind`, `OnBehalfOfUserId` | `SignatureAlgo`, `Signature` (reserved, always `NULL`) |
| `SessionId`, `InvocationId`, `CorrelationId` | |
| `ActionType`, `TargetType`, `TargetId` | |
| `Decision`, `Outcome` | |
| `InputDigest`, `OutputDigest`, `ErrorCode`, `ErrorDigest` | |

The unhashed columns are protected by the immutability triggers only. Saying otherwise would be a false
assurance claim.

### There is no payload column

**Raw content is never stored.** There is no `PayloadJson` field and there never was one — only
`InputDigest` / `OutputDigest`, the SHA-256 of content you keep in your own store.

This is deliberate. Anything inside the hash can never be redacted, so putting a payload there would mean a
lawful erasure request could only be honoured by breaking the chain. Digests bind the content to the record
without holding it.

The same reasoning explains `ErrorMessage`: the readable sentence is stored **outside** the hash so it can be
redacted, while `ErrorCode` (a structural code) and `ErrorDigest` (the SHA-256 of the full text) are inside it.
Redacting the message leaves the digest attesting to text that is no longer present — that is the intended
trade, made explicit rather than hidden.

### Two fields are structurally constrained

Because they are hashed and therefore permanently unredactable, two fields reject anything that looks like free
text, at write time:

- **`TargetId`** — at most 128 characters from `[0-9 A-Z a-z . _ : / -]`
- **`ErrorCode`** — at most 64 characters from `[0-9 A-Z a-z . _ -]`

No spaces, no `@`, no commas — so a name, a sentence or an e-mail address cannot be stored there.

**Be clear about what that buys.** It removes the easy accident. It **cannot detect personal data**: a national
identity number, a phone number or a customer's e-mail localpart all fit the character set perfectly well.
Choosing a surrogate key over a natural one remains **your** responsibility.

## Verification is two-layer, and only one layer is the authority

Verification runs in two places, and the difference matters:

| Layer | Where | Covers | Authoritative |
|---|---|---|---|
| **1 — database** | T-SQL, no application needed | Linkage, ordering, SeqNo gaps, head agreement, and each row's hash against the **stored** canonical body | **No — PARTIAL** |
| **2 — canonical** | C# | Re-canonicalizes every row **from its own columns**, compares byte-for-byte, then re-hashes | **Yes** |

Canonicalization happens in C#, so the database can only re-hash the canonical body it already has stored. If
someone edits a projection column — `AgentId`, `ActionType`, `Outcome`, `TargetId` — and leaves the canonical
body and row hash untouched, **the in-database check still passes**. Nothing it can see disagrees. Only
re-canonicalizing from the row's own columns catches it.

So: **a "valid" answer from the database layer alone is not a proof of integrity, and must never be presented
as one** — not in an API response, not in the console, not in a report. `POST /api/agent-audit/verify` runs both
layers and reports the first divergence together with the layer that found it; the combined verdict is always
the worse of the two, so a green database layer can never mask a broken canonical one.

### The verdict is three-valued

| Status | Meaning | Render as |
|---|---|---|
| `Valid` | Everything the database holds verifies, with nothing folded away | Good |
| `Degraded` | Every link and hash agrees, **but the database cannot prove the whole history** — a range was folded into the archive, or a tombstone is unaccounted for | **Not good** |
| `Broken` | A hash mismatch, a broken link or a SeqNo gap. Evidence of tampering | Bad |

**`Degraded` is never rendered green.** It is not a warning decoration on a good result: it means the
in-database proof is incomplete and the archive must be checked. The `IsValid` flag is `true` for `Valid` and
for nothing else — it is `false` for both `Degraded` and `Broken`.

## Who writes to the ledger

> **Single appender for events.** In total there are exactly TWO writers: `_Append` and `_Fold`. Both are stored
> procedures, both self-audit. No other path INSERTs.

`_Fold` exists as the second writer because a fold tombstone must occupy the folded range's first sequence
number, mid-chain, which an appender that only ever writes `LastSeqNo + 1` can never produce. It audits itself
by appending a normal, hash-covered `agentaudit.fold` event at the chain tail in the same transaction.

The ledger lives **only** in the control plane's database. An application never writes to it directly: it POSTs
to the ingest endpoint with a service token, and the server resolves the application's identity from that token.

## Retention: folding, not deleting

Append-only and "delete old rows" are contradictory, so old data is **folded** rather than deleted:

1. The range is exported to an archive in the normative
   **[Archive Format v1](agent-audit-archive-format-v1.md)** — a ZIP holding `manifest.json` and
   `segment.ndjson` — which yields a **segment digest**. The digest is written into the manifest as well as
   returned to the caller, so the archive carries the value that will later be compared against the
   tombstone's `FoldSegmentDigest`. (That comparison, not the manifest's own copy, is the anchoring proof.)
2. The rows are removed and replaced by a single **tombstone** at the range's first sequence number, carrying
   the range's boundary hashes verbatim — so the following row's link stays valid and the chain still verifies.
3. The fold appends its own hash-covered accounting record, so it audits itself.

Retention defaults to **730 days**, with an **enforced minimum of 180 days**: a shorter window is refused by the
database, not merely discouraged. Folding is **operator-triggered only** — nothing schedules it — and requires
both an archive reference and the segment digest, so "export before you fold" is a mechanism rather than a
matter of discipline. A range containing a fold accounting record can never itself be folded, because that
would retroactively orphan an earlier tombstone.

### Folding takes two people

Since `AsdamirVault_134` a fold is not a single action. It is three:

1. **Propose** — an operator holding `ent.agentaudit.fold.propose` exports the range, records where the archive
   was stored, and states **why**. The justification is mandatory: a checker asked to approve an irreversible
   action with no stated reason can only rubber-stamp it. Proposing removes nothing.
2. **Approve or reject** — a **different** operator, holding `ent.agentaudit.fold.approve`, decides. The
   proposer cannot decide their own proposal, and that is enforced by a database `CHECK` constraint on the two
   identities — not only by the stored procedure and not only by the API. If the rule lived solely in the
   application, "four eyes" would mean "four eyes as long as everyone uses the front door".
3. **Execute** — the approved proposal is carried out, again not by its proposer.

Two things stand between an approval and a deletion:

- **Only the proposal id crosses the wire.** The range, the archive reference and the retention window are read
  back from the stored proposal. If they could be supplied at execution time, a caller could have range A
  approved and fold range B, and the approval would attest to something that never happened.
- **The segment digest is re-derived and compared.** The digest recorded when the proposal was made is
  recomputed from the live rows at execution. Any difference refuses the fold and asks for a fresh export and a
  fresh proposal — what was approved is what gets folded, or nothing does.

**An approval expires after seven days.** Seven spans a working week, which is the natural unit for *"the same
two people are still here and still authorized"*. A shorter window forces haste across timezones and shifts,
which turns the second signature into a formality; a longer one lets an approval outlive an access review or an
operator's notice period, and it would then attest to authority that no longer exists. Expiry **refuses and
says so** rather than lapsing quietly — an approval that simply stopped working, with no explanation, reads as
a broken feature and gets worked around.

After a fold the chain still verifies, but the verdict becomes **`Degraded`**: the database no longer holds the
evidence for the folded span, and only the archive can complete the proof.

## When delivery fails

An audit record has no second copy, so the client never drops one quietly. Every path ends somewhere
accountable: delivered, spooled for retry, or dead-lettered with a standing alarm.

`AgentAuditOptions.OnSinkFailure` chooses the policy — a compliance decision, not a performance one:

| Mode | Behaviour |
|---|---|
| `Spool` *(default)* | Undelivered records go to a bounded local NDJSON spool and are replayed at startup |
| `Throw` | Fail-closed: once delivery is failing, subsequent calls throw rather than let an unauditable action proceed |
| `DropAndWarn` | The gap is accepted and merely logged. Only for an advisory ledger — never for a record you may have to produce |

Because delivery is asynchronous, `Throw` cannot fail the very record that could not be sent; it fails the
**next** call. That is a real limitation of an asynchronous sink, and it is why `Spool` is the default.

### Permanent versus transient — and why an unknown status is transient

Each submitted record comes back with one of a **closed, versioned** set of statuses:

| Status | Meaning | Client action |
|---|---|---|
| `appended` | Written to the ledger | Done |
| `duplicate` | Already present — the expected result of an at-least-once replay | Done, successfully |
| `rejected` | **Permanent.** These exact bytes can never be written | Dead-letter it |
| `failed` | **Transient.** The same record may succeed later | Retry to the cap |

The split is what makes dead-lettering possible at all. Retry a permanent rejection for ever and the record
merely looks busy while the gap goes unnoticed; dead-letter a transient failure and you discard a record that
would have succeeded. Both are silent audit gaps.

**A status this client version does not recognise is always treated as TRANSIENT.** The response carries a
contract version precisely so new statuses can be added; assuming an unknown one is permanent would silently
discard a record that a newer server considered retryable. The retry cap
(`AgentAuditOptions.MaxDeliveryAttempts`) is what stops such a record circulating for ever.

There are therefore exactly **two routes into the dead letter**: a status known to be permanent, and an
unknown or transient status that exhausted the retry cap.

### The dead-letter alarm is persistent by design

A dead-lettered record is a **hole in the audit trail**. It is written to a separate `dead-letter.ndjson` file,
counted, and reported at **error level** — the level the framework forwards to central error monitoring — both
when it happens and **repeatedly thereafter**, every `DeadLetterWarningInterval`, for as long as anything
remains dead-lettered.

The alarm is deliberately **not** warn-once, and the count is re-derived from the file at startup, so **a
restart does not silence it**. A warning that a restart clears is a warning that will be cleared by a restart.
It stops only when someone deals with the file.

## Permissions

Four separate permissions, because reading, verifying, asking to fold and allowing a fold are genuinely
different powers — folding removes rows and is the only one that can destroy evidence:

- `ent.agentaudit.read` — view records and chain state
- `ent.agentaudit.verify` — run a chain verification
- `ent.agentaudit.fold.propose` — export a range and open a fold proposal
- `ent.agentaudit.fold.approve` — decide on a proposal, and execute an approved one

**Why the fold permission was split rather than kept as one.** A single code cannot express *may ask* versus
*may allow*: whoever holds it holds both halves, and the second pair of eyes becomes a step the same person
completes alone. `AsdamirVault_134` therefore replaced `ent.agentaudit.fold` with the pair above and
**deleted** the old code rather than deprecating it — a permission left seeded keeps satisfying any policy that
still requires it.

### Roles: who holds them

| Role | `read` | `verify` | `fold.propose` | `fold.approve` |
|---|:--:|:--:|:--:|:--:|
| **SuperAdmin** | ✅ | ✅ | ✅ | ✅ |
| **Auditor** | ✅ | ✅ | **no** | **no** |

**Why an Auditor exists at all.** Until `AsdamirVault_132` all three permissions belonged to SuperAdmin
alone — the widest authority on the platform, and therefore *the actor most in need of auditing*. An audit
trail that only its own subject can read is the classic failure shape. The argument is the same one
[Archive Format v1](agent-audit-archive-format-v1.md) makes outward ("if verification depends on the closed
component, assurance collapses to trusting the vendor"), turned inward.

**Why the Auditor cannot fold.** Folding is the one irreversible operation here: it removes rows. The party
auditing must not be able to destroy what it audits. The omission is deliberate and is asserted at the API,
not merely hidden in the console — the fold panel is not offered to a non-holder, but that is a courtesy;
the policy refuses the request whether or not the page was ever opened.

**SuperAdmin is unchanged, deliberately.** The aim is to ADD a second pair of eyes, not to remove the first.

### Migration note: this enables a second operator, it does not create one

`AsdamirVault_134` seeds both fold permissions and grants them to `SuperAdmin`. It creates **no** additional
operator and assigns **no** role to anyone. Who proposes and who approves is a human decision about your
organization, and a migration that invented a second account would be answering it on your behalf.

The consequence is worth stating plainly rather than discovering: **on a fresh install, or on any deployment
with exactly one console operator, folding cannot complete.** The proposal step works; the decision step
refuses, because the only available decider is the proposer. That is the rule working, not a defect — the
console says so on screen rather than leaving a dead button.

To enable folding, give a second operator `ent.agentaudit.fold.approve`. Note that `SuperAdmin` and `AppAdmin`
cannot be hand-assigned for this purpose (the API refuses it): they derive their permissions from the built-in
catalogue, so granting one to obtain an approver would hand over the entire platform. Create a role carrying
just the approve permission, or use `Auditor` plus that one grant.

**Scope — the role is app-scoped, its READ is not.** Roles in this schema carry an `AppId`, and `Auditor` is
no exception. But an auditor who can see only one application audits nothing, so the ledger endpoints are
cross-app by construction and an Auditor reads **every** app's chain. That is a deliberate deviation from
"a role is scoped to one app", and it is contained: it applies to the ledger endpoints and to nothing else.
The negative test set asserts that reach does not extend to the app registry, the user directory, role
administration, app configuration or localization — **one surface at a time**, because a single "cannot do
admin things" assertion would pass while one specific endpoint stood open.

### The honesty boundary — what a role does NOT establish

**This is access control. It is not evidence.**

The Auditor role answers *"who may look?"*. Whether what they see is TRUE is a different question, answered
by the hash chain (this page) and by [independent verification](agent-audit-archive-format-v1.md) — never by
the role. A narrative that blends the two claims a stronger guarantee than exists: granting a read
permission to a second person adds a witness, not integrity. Integrity was already there, or it was not, and
no role changes which.

Concretely: an Auditor reading a chain that reports `Valid` has learned that *this database's rows hash
consistently*. That is exactly what a SuperAdmin reading the same screen learns. What separation of duties
adds is that the reading is no longer performed solely by the party with the most to hide — a governance
property, not a cryptographic one.

## See also

- **[Canonicalization Specification v1](agent-audit-canonicalization-v1.md)** — the normative wire format:
  field order, byte encodings, the hash chain, the tombstone exception, and the verification algorithm.
  Enough to write an **independent verifier** in any language, with frozen
  [golden vectors](agent-audit-golden-vectors-v1.json) to check it against. Read it if you need to prove the
  ledger's integrity without trusting our tooling.
- **[Archive Format v1](agent-audit-archive-format-v1.md)** — what a folded segment is exported to, and
  how to verify one **without the product**: no database, no control plane, no network. Read its
  "assurance boundary" section before quoting any verification result.
- [Audit Logging](audit-logging.md) · [Authorization](authorization.md) · [Observability](observability.md)
