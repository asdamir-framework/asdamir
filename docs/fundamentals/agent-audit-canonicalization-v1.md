# Agent Action Ledger — Canonicalization Specification v1

**Status: NORMATIVE · Version: 1 (`HashVersion = 1`) · FROZEN.**

This document specifies, completely and without reference to any Asdamir source code, how a record in the
agent action ledger is serialized into bytes and chained by hash. It exists so that **anyone can write an
independent verifier** — in any language, offline, with no Asdamir library, no AppManagement instance and no
database — and reach the same verdict as the vendor's own tooling.

That independence is the point. **If the only thing that can verify the ledger is the closed component being
audited, the assurance collapses to "trust the vendor": a system's own clean report about itself is not audit
evidence.** This specification, plus the [golden vectors](agent-audit-golden-vectors-v1.json), is what makes
the guarantee checkable by someone who does not trust us.

The key words **MUST**, **MUST NOT**, **SHOULD** and **MAY** are to be interpreted as in RFC 2119.

For the concepts — what the ledger is for, what the guarantee is and is not, how retention works — see
[Agent Audit](agent-audit.md). This page is the wire format only.

---

## 1. The chain

A ledger is partitioned into independent chains, one per **scope** — the pair `(AppId, TenantId)`. Records
within a scope carry a gapless `SeqNo` starting at 1. Each record binds itself to its predecessor:

```
RowHash = SHA-256( PrevHash ‖ CanonicalPrefix ‖ SeqNo ‖ RecordedAtTicks )
```

| Part | Bytes | Produced by |
|---|---|---|
| `PrevHash` | 32, raw | the previous record's `RowHash`; **32 zero bytes (`0x00 × 32`)** for `SeqNo = 1` |
| `CanonicalPrefix` | variable | the client (§2, §3) |
| `SeqNo` | 8, **big-endian** signed | the server, under the chain lock |
| `RecordedAtTicks` | 8, **big-endian** signed | the server, under the chain lock (§4) |

`‖` is plain concatenation: **no separators, no padding, no length field around the whole**.

The split is deliberate. Everything a client can know is canonicalized by the client, which makes the encoding
testable in isolation; the two fields only the server can assign — the sequence number and the authoritative
receipt time — are appended inside the same transaction that holds the per-scope lock, so two concurrent
writers cannot be assigned the same `SeqNo` or race the chain head.

## 2. Canonical prefix — field order (FROZEN for v1)

Exactly these 23 fields, in exactly this order. A verifier MUST reject any other order.

| # | Field | Encoding | Nullable |
|---:|---|---|:--:|
| 1 | `HashVersion` | 1 byte, **unprefixed** | no |
| 2 | `AppId` | 16 bytes, **unprefixed**, RFC 4122 **big-endian** | no |
| 3 | `TenantId` | length-prefixed UTF-8 | no |
| 4 | `RecordKind` | 1 byte, unprefixed (0 = Event, 1 = Seal, 2 = FoldTombstone) | no |
| 5 | `EventId` | 16 bytes, unprefixed, RFC 4122 **big-endian** | no |
| 6 | `OccurredAtUtc` | length-prefixed UTF-8 of the round-trip form (§3.4) | no |
| 7 | `AgentId` | length-prefixed UTF-8 | no |
| 8 | `AgentVersion` | length-prefixed UTF-8 | yes |
| 9 | `ModelId` | length-prefixed UTF-8 | yes |
| 10 | `AuthorityKind` | 1 byte, unprefixed (0 = Delegated, 1 = Autonomous, 2 = Scheduled) | no |
| 11 | `OnBehalfOfUserId` | nullable int32 (§3.5) | yes |
| 12 | `SessionId` | length-prefixed UTF-8 | yes |
| 13 | `InvocationId` | length-prefixed UTF-8 | yes |
| 14 | `CorrelationId` | length-prefixed UTF-8 | yes |
| 15 | `ActionType` | length-prefixed UTF-8 | no |
| 16 | `TargetType` | length-prefixed UTF-8 | yes |
| 17 | `TargetId` | length-prefixed UTF-8 — structurally constrained (§6) | yes |
| 18 | `Decision` | 1 byte, unprefixed (0 = Allowed, 1 = Denied, 2 = RequiresApproval) | no |
| 19 | `Outcome` | 1 byte, unprefixed (0 = Success, 1 = Failure, 2 = Partial) | no |
| 20 | `InputDigest` | length-prefixed raw bytes (32, or NULL) | yes |
| 21 | `OutputDigest` | length-prefixed raw bytes (32, or NULL) | yes |
| 22 | `ErrorCode` | length-prefixed UTF-8 — structurally constrained (§6) | yes |
| 23 | `ErrorDigest` | length-prefixed raw bytes (32, or NULL) | yes |

## 3. Encoding rules

### 3.1 Length prefix
Every "length-prefixed" field is written as a **4-byte little-endian signed integer** giving the number of
**bytes** that follow, then those bytes.

- `NULL` → prefix **`-1`** (`FF FF FF FF`), no bytes follow.
- Empty string → prefix **`0`**, no bytes follow.

`NULL` and `""` therefore hash **differently**. A verifier MUST preserve that distinction; conflating them is
the most common implementation error, which is why the golden vectors pin both cases separately.

### 3.2 Unprefixed fields
`HashVersion`, `RecordKind`, `AuthorityKind`, `Decision` and `Outcome` are single bytes with **no length
prefix**. `AppId` and `EventId` are 16 raw bytes with **no length prefix**.

### 3.3 GUIDs
GUIDs are encoded in **RFC 4122 byte order (big-endian)** — the order in which the textual form reads. In
.NET this is `Guid.ToByteArray(bigEndian: true)`; the default `Guid.ToByteArray()` is **mixed-endian** and
produces a different, non-conforming prefix.

### 3.4 Timestamps in the prefix
`OccurredAtUtc` is the UTF-8 text of the ISO-8601 round-trip form with **exactly 7 fractional digits** and a
`Z` suffix, e.g. `2026-07-30T12:34:56.1234567Z` (.NET: `ToString("O")` on a `DateTimeKind.Utc` value). It is
client-asserted information; the authoritative time is `RecordedAtUtc` (§4).

### 3.5 Nullable 32-bit integer
`OnBehalfOfUserId` is written as a length-prefixed value: `NULL` → prefix `-1`; otherwise prefix `4` followed
by the value as **4 bytes big-endian, two's complement** (so negative values are well defined — see the
`negative_user_id` vector).

### 3.6 Digests
`InputDigest`, `OutputDigest` and `ErrorDigest` are length-prefixed **raw bytes**, never hex text: prefix `32`
plus the 32 bytes, or prefix `-1` when absent. A digest of any other length MUST be rejected at write time.

## 4. `RecordedAtTicks`

`RecordedAtTicks` is the number of **100-nanosecond ticks since `1970-01-01T00:00:00Z`** — a documented
integer, never a cast of a database timestamp type (whose on-disk representation is an implementation detail
and MUST NOT be relied on).

```
-- T-SQL
DATEDIFF_BIG(SECOND, '1970-01-01T00:00:00', RecordedAtUtc) * 10000000
  + DATEPART(NANOSECOND, RecordedAtUtc) / 100

// .NET
recordedAtUtc.Ticks - DateTime.UnixEpoch.Ticks
```

Both forms yield `17854148961234567` for `2026-07-30T12:34:56.1234567Z`. (`DATEDIFF_BIG(NANOSECOND,
'0001-01-01', …)` overflows a 64-bit integer and MUST NOT be used; the second + nanosecond form above is
overflow-safe well past the year 29000.)

## 5. What the hash does NOT cover

The canonical body is immutable by construction, so anything inside it can never be redacted. These columns
are therefore stored **outside** the hash and are **not** protected by the chain:

| Column | Why it is outside |
|---|---|
| `ErrorMessage` | free-text error prose — must stay redactable (§6) |
| `OnBehalfOfUserName` | a display name; the *identity* is bound via `OnBehalfOfUserId` (field 11) |
| `FoldSeqStart`, `FoldSeqEnd`, `FoldRowCount`, `FoldSegmentDigest`, `FoldArchiveRef` | fold bookkeeping (§7) — an archive must remain relocatable |
| `SignatureAlgo`, `Signature` | reserved for a future version; **always NULL in v1** |

State this plainly to anyone relying on the ledger: editing one of these columns **will not** break the chain.
They are protected by the database's immutability controls, not by cryptography.

## 6. Structural constraints — free text never enters the hashed body

Two fields inside the hash are constrained to structural values, enforced **at write time**:

- **`TargetId`** — length ≤ 128, characters from `[0-9 A-Z a-z . _ : / -]` only. It MUST be an **opaque or
  surrogate identifier**, never a natural key.
- **`ErrorCode`** — length ≤ 64, characters from `[0-9 A-Z a-z . _ -]` only. A machine-readable code such as
  `orders.insufficient_stock`; the readable text goes to `ErrorMessage`, bound to the chain only by
  `ErrorDigest`.

The character-set comparison MUST be **ordinal/binary** (SQL Server: `Latin1_General_BIN2`). Under a
case-insensitive accent-insensitive collation an `A-Z` range also matches accented and dotted letters — for
example the Turkish `İ` and `ş` — which silently voids the constraint.

**What this does and does not achieve, stated honestly:** the constraint makes it impossible to store a
sentence, an e-mail address or a stack trace in these fields. It does **not** detect personal data — a
national id number or a phone number passes the character set. Choosing a surrogate identifier remains the
caller's responsibility. The constraint removes easy accidents, not the risk.

## 7. Fold tombstones — the one exception to the invariant

Retention is implemented by **folding**, not deleting: a closed range `[a..b]` is exported to an archive and
replaced by a single tombstone record (`RecordKind = 2`) that carries `PrevHash(a)` and `RowHash(b)`
**verbatim**, so the record at `b+1` still links correctly and the chain verifies end to end.

Consequently:

> The invariant `RowHash = SHA-256(PrevHash ‖ prefix ‖ SeqNo ‖ ticks)` holds **only for `RecordKind ∈ {0, 1}`**.

A conforming verifier MUST special-case `RecordKind = 2`: do not recompute its `RowHash`; treat the folded
range as **verifiable against the archive, not against the database**, and report it as such. A verifier that
reports a folded chain as fully "valid" from the database alone is **wrong** — see §8.

Each fold also writes an ordinary Event record (`ActionType = agentaudit.fold`) whose hashed body carries the
folded range and the segment digest, so a tombstone has an accounting entry inside the chain. A tombstone with
no such entry MUST be reported (it is the `Degraded` case below). A range containing a fold accounting record
cannot itself be folded.

## 8. Verification — the algorithm, and the limits of each layer

A conforming verifier, given the records of one scope, MUST check:

1. **Sequence continuity** — `SeqNo` increases by exactly 1, with no gaps (a fold tombstone accounts for its
   whole `[FoldSeqStart..FoldSeqEnd]` range).
2. **Linkage** — every record's `PrevHash` equals the previous record's `RowHash`; the first record's
   `PrevHash` is 32 zero bytes.
3. **Re-derivation** — for `RecordKind ∈ {0, 1}`, re-canonicalize the record **from its columns** per §2–§4 and
   recompute `RowHash`. Comparing against a stored copy of the prefix is **not** re-derivation (§8.1).
4. **Head** — the last record's `RowHash` equals the chain head.
5. **Tombstone accounting** — every tombstone has a matching fold accounting record.

### 8.1 Why a database-only check is partial
An in-database verifier can rehash a **stored** canonical prefix, but it cannot rebuild that prefix from the
columns. Therefore a database-only check **cannot detect an edit to a projection column** (`AgentId`,
`ActionType`, `TargetId`, …) that leaves the stored prefix and `RowHash` untouched. Only re-derivation from
the columns catches it. Any implementation MUST NOT present a database-only result as proof of integrity.

### 8.2 The verdict is three-valued
A boolean verdict is not sufficient, because "the links are intact" and "the history is provable" are
different claims.

| Verdict | Meaning |
|---|---|
| `Broken` | a hash mismatch, a link break, a gap, or a head mismatch — report the exact `SeqNo` and which check failed |
| `Degraded` | the links verify, but the database alone cannot prove the whole history: a folded range (verifiable only against its archive) or a tombstone with no accounting record |
| `Valid` | every check above passes in full |

`Degraded` **MUST NOT** be rendered as success (no green, no check mark). Only `Valid` may be presented as
good.

## 9. Golden vectors

[`agent-audit-golden-vectors-v1.json`](agent-audit-golden-vectors-v1.json) contains frozen test vectors for
this specification: for each case, the full `CanonicalPrefix` in hex, its length, and the resulting `RowHash`
for a fixed `PrevHash`, `SeqNo = 4242` and `RecordedAtTicks = 17854148961234567`.

The cases deliberately cover the encoding's failure modes: all-nulls versus all-empty-strings (§3.1),
Turkish multi-byte text, every field at its maximum length, a negative `OnBehalfOfUserId` (§3.5), maximum enum
values, and a `HashVersion = 2` prefix proving the version byte changes the hash.

An independent implementation is conforming when it reproduces every `prefixHex` and every `rowHashHex`
byte-for-byte.

**This file is the only copy.** The vectors published here are not a snapshot of some internal fixture — the
reference implementation's own test suite asserts against *this* file, so the bytes you verify against are
byte-for-byte the bytes the implementation is proven correct against. That is deliberate: a private duplicate
could drift, and a drifted duplicate would leave this specification quietly lying while every internal test
stayed green — at which point your verifier would disagree with a chain that is in fact intact, and you would
have no way to tell which side was wrong.

> **Editing an existing v1 expectation is a bug, not a fix.** If your implementation disagrees with a vector,
> your implementation is wrong — or the format changed, in which case it is a new `HashVersion` (§10), not an
> edit to v1.

## 10. Versioning

`HashVersion` is field 1 **and is inside the hash**, so a version change necessarily changes every resulting
`RowHash` — a v2 record can never be mistaken for a v1 record.

- **v1 is frozen.** Its field order, encodings and vectors do not change, ever. Records already written under
  v1 must remain verifiable forever.
- A new version MUST be introduced as `HashVersion = 2` with its own specification page and its own golden
  vectors, and verifiers MUST dispatch on the stored `HashVersion` rather than assuming the newest.

## 11. Assurance statement

Read this before quoting the ledger in a compliance context.

- What this specification gives you: **a tamper-evident record** — any modification to a hashed field, any
  deleted row, and any reordering is detectable, and the detection is reproducible by a third party.
- What it does **not** give you: **it is not proof that a particular agent produced the record.** `AgentId`,
  `ModelId` and `OnBehalfOfUserId` are the **application's assertion**, recorded immutably. There is no
  per-agent key and no signature in v1 (`SignatureAlgo`/`Signature` are reserved and always NULL).

So the honest formulation is: *application assertion, recorded in a tamper-evident chain* — **not** *"the
agent cryptographically proved its own identity"*. Do not describe it more strongly than that.

## See also

- [Archive Format v1](agent-audit-archive-format-v1.md) — the normative container a folded segment is
  exported to, and the algorithm that verifies one offline. It references this page for the byte layout
  rather than repeating it.
- [Agent Audit](agent-audit.md) — concepts, recording actions, retention, permissions, delivery guarantees.
