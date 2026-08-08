# Agent Action Ledger — Archive Format Specification v1

**Status: NORMATIVE · `FormatVersion = 1` · FROZEN.**
**Revision 2026-08-08 — CLARIFICATION ONLY. `FormatVersion` STAYS `1`; v1 SEMANTICS DID NOT CHANGE.**
Implementing the format for the first time surfaced questions this page had not answered — the order in
which two failures are reported, whether a verifier tolerates uppercase hex, which bucket a row-count
disagreement falls into. Every answer below is the behaviour the reference implementation *already* has;
nothing about the bytes, the field set or the algorithm changed. **An archive valid before this revision is
valid after it, and vice versa.** The clarifications are here because two conforming implementations that
answered these differently would disagree on a real archive — silently.

When a closed range of the ledger is **folded** (removed from the live database and replaced by a tombstone —
see [Agent Audit](agent-audit.md#retention-folding-not-deleting)), the rows themselves are exported to an
**archive**. This document specifies that archive's bytes: what it contains, how each field is encoded, and
the exact algorithm a verifier must run.

It exists so that **anyone can verify an archive without the product** — no AppManagement, no database, no
commercial licence, no network. If the only thing that can check the archive were the closed component that
produced it, the assurance would collapse to *"trust the vendor"*.

The wire format of the hash chain itself — the canonical byte layout, field order and `RowHash` formula —
is **not** repeated here. It is specified once, in
[Canonicalization v1](agent-audit-canonicalization-v1.md), and this document **references** it. Two copies of
one contract drift; there is exactly one.

The key words **MUST**, **MUST NOT**, **SHOULD** and **MAY** are to be interpreted as in RFC 2119.

---

## 1. The assurance boundary — read this before trusting any result

An archive can attest to **two different things**, and a tool that blurs them is lying to its user.

### A — Internal consistency · **the archive alone CAN prove this**
Every row's `RowHash` is correctly derived from its own columns, each `PrevHash` equals the previous row's
`RowHash`, and `SeqNo` runs without gaps. In other words: *this archive has not been altered since it was
written*.

### B — Anchoring · **the archive alone CANNOT prove this**
That this segment is genuinely the segment that was folded out of a particular live ledger. The proof of
that is the **`FoldSegmentDigest` recorded on the tombstone in the live ledger** — a value the archive cannot
produce for itself. If it could, a forged archive could produce it too.

> **The `SegmentDigest` inside `manifest.json` is NOT the anchor.** It is derived from the very rows it
> accompanies, so it verifies nothing on its own: an attacker who rewrites the rows simply recomputes it. It
> is present for a single purpose — to be **compared against the digest held by the live ledger**. Treat a
> manifest digest that matches the archive as saying *"this manifest describes these rows"*, never as
> *"these rows are authentic"*.

### What a conforming verifier MUST report

| Situation | Result | Meaning |
|---|---|---|
| Internal checks pass **and** an expected digest was supplied **and** it matches | `Verified` | A **and** B |
| Internal checks pass, **no** expected digest supplied | `InternallyConsistent` | A only — **unanchored** |
| Internal checks pass, expected digest supplied, **does not** match | `DigestMismatch` | These rows are not that segment |
| Any internal check fails | `Broken` | The archive is altered or corrupt |

A verifier **MUST NOT** report `Verified` when no expected digest was supplied, and **MUST** state in its
output which of A and B was established. `Verified ✓` on its own is not an acceptable message. (This is the
same discipline as the ledger's three-valued `ChainStatus`, where `Degraded` is never rendered as success:
do not dilute the green.)

---

## 2. Versioning and forward compatibility

`manifest.json` carries **`FormatVersion`** (this document: `1`) and **`HashVersion`** (the canonicalization
contract version of the rows inside; v1 archives carry `1`).

- A verifier **MUST** reject an archive whose `FormatVersion` it does not implement, with a distinct
  "unsupported format version" error. It **MUST NOT** attempt a best-effort parse and it **MUST NOT** report
  any of the four results in §1 for such an archive — an unknown layout means the checks were not performed,
  and silently continuing would present an unchecked archive as checked.
- The same applies to `HashVersion`: a verifier that does not implement the record's hash version **MUST**
  refuse rather than guess. `HashVersion` is field 1 of the canonical body, so a wrong assumption changes
  every hash.
- **One `HashVersion` per archive.** The manifest carries a single value, so every row in the segment
  **MUST** share it, and a producer **MUST** refuse to write a segment whose rows disagree — no manifest
  can honestly describe one. For a verifier the bucket depends on WHY it cannot proceed, per the
  principle in §9:
    - the row's `HashVersion` is one the verifier **implements** but differs from the manifest's →
      **`Broken`**. The manifest made a claim about the rows and a row contradicts it; that is a
      detectable content failure, exactly like a line count that disagrees with `RowCount`.
    - the row's `HashVersion` is one the verifier **does not implement** → **format error**. It cannot
      rebuild that row's canonical body at all, so it must not report a result that claims it checked.
- **v1 is frozen.** Its layout, field names and encodings do not change. A future revision is
  `FormatVersion = 2` with its own specification page; v1 archives remain verifiable forever.

---

## 3. Container

An archive is a **ZIP file** with exactly two entries at the root, named exactly:

```
<name>.zip
├── manifest.json      (§4)
└── segment.ndjson     (§5)
```

- Both entries **MUST** be UTF-8 with **no byte-order mark**.
- Additional entries **MUST** be ignored by a verifier, not rejected — they carry no meaning in v1 and
  reserving them keeps a future revision cheap. A verifier **MUST NOT** derive anything from them.
- Entry names are case-sensitive.
- The archive file name is **not** significant; nothing in it is verified.

## 4. `manifest.json`

A single JSON object. All twelve members are **REQUIRED**; a missing or null member is a format error
(§2 rules apply — refuse, do not proceed).

| Member | Type | Meaning |
|---|---|---|
| `FormatVersion` | integer | This specification's version. `1`. |
| `HashVersion` | integer | The canonicalization version of the rows (§2). |
| `AppId` | string (UUID) | Chain scope, canonical text form (`8-4-4-4-12`). |
| `TenantId` | string | Chain scope. |
| `SeqStart` | integer | `SeqNo` of the first row in the segment. |
| `SeqEnd` | integer | `SeqNo` of the last row. |
| `RowCount` | integer | Number of rows; **MUST** equal `SeqEnd − SeqStart + 1` and the actual line count. |
| `SegmentDigest` | string (hex) | §6. **Not the anchor** — see §1. |
| `PrevHashAtStart` | string (hex) | The `PrevHash` of the row at `SeqStart` — the link back to the row preceding the segment. |
| `RowHashAtEnd` | string (hex) | The `RowHash` of the row at `SeqEnd` — the link forward to the row following the segment. |
| `ExportedAtUtc` | string | When the archive was produced. ISO-8601 round-trip, UTC (`…Z`). **Informational — outside every hash.** |
| `ProducerVersion` | string | Version of the component that produced the archive. **Informational.** |

`PrevHashAtStart` and `RowHashAtEnd` exist so a segment can be re-attached to the live ledger by eye or by
tooling: the tombstone that replaced this range carries the same two values.

**Hex encoding** throughout this document means **lowercase, unseparated, no `0x` prefix** — a 32-byte hash
is 64 characters. A producer **MUST** emit lowercase. A verifier **MUST** compare the DECODED BYTES, not the
text, and therefore **MUST** accept uppercase or mixed case on input: case is a spelling of the same value,
and refusing it would fail an archive that is byte-for-byte sound. (A verifier **MAY** note the deviation;
it **MUST NOT** downgrade the result for it.)

## 5. `segment.ndjson`

Newline-delimited JSON: one **flat** JSON object per line, `\n` (LF) separated, ordered by **strictly
ascending `SeqNo`**, dense (no gaps). A trailing newline after the last line is permitted.

Each line carries every field needed to **rebuild the canonical body from scratch**, plus the two chain
hashes and the stored prefix:

| Member | Type | In the hash? |
|---|---|---|
| `SeqNo` | integer | yes (appended, big-endian, by the server) |
| `RecordedAtTicks` | integer | yes (appended, big-endian, by the server) |
| `HashVersion` | integer | yes — canonical field 1 |
| `AppId` | string (UUID) | yes — field 2 |
| `TenantId` | string | yes — field 3 |
| `RecordKind` | integer | yes — field 4 |
| `EventId` | string (UUID) | yes — field 5 |
| `OccurredAtUtc` | string | yes — field 6 |
| `AgentId` | string | yes — field 7 |
| `AgentVersion` | string \| null | yes — field 8 |
| `ModelId` | string \| null | yes — field 9 |
| `AuthorityKind` | integer | yes — field 10 |
| `OnBehalfOfUserId` | integer \| null | yes — field 11 |
| `SessionId` | string \| null | yes — field 12 |
| `InvocationId` | string \| null | yes — field 13 |
| `CorrelationId` | string \| null | yes — field 14 |
| `ActionType` | string | yes — field 15 |
| `TargetType` | string \| null | yes — field 16 |
| `TargetId` | string \| null | yes — field 17 |
| `Decision` | integer | yes — field 18 |
| `Outcome` | integer | yes — field 19 |
| `InputDigest` | string (hex) \| null | yes — field 20 |
| `OutputDigest` | string (hex) \| null | yes — field 21 |
| `ErrorCode` | string \| null | yes — field 22 |
| `ErrorDigest` | string (hex) \| null | yes — field 23 |
| `PrevHash` | string (hex) | the chain link |
| `RowHash` | string (hex) | the row's hash |
| `CanonicalPrefix` | string (hex) | §7 — stored, **not trusted** |
| `OnBehalfOfUserName` | string \| null | **no** |
| `ErrorMessage` | string \| null | **no** |
| `SignatureAlgo` | string \| null | **no** — reserved, null in v1 |
| `Signature` | string (hex) \| null | **no** — reserved, null in v1 |

The last four are carried because they are part of the record a reader wants to see, but they are **outside
the hash**: editing them does not break anything, and a verifier **MUST NOT** treat them as evidence.
Which columns the chain covers is stated once, in
[Canonicalization v1 §5](agent-audit-canonicalization-v1.md#5-what-the-hash-does-not-cover).

A `null` member and an **absent** member are equivalent for the nullable fields above. For non-nullable
fields, absence is a format error — **except as follows.**

> **`CanonicalPrefix` on a tombstone.** A fold tombstone (`RecordKind = 2`) carries no canonical prefix: its
> `RowHash` is *carried over* from the folded range's last row, not derived from its own content. The
> requirement above therefore binds only for `RecordKind ∈ {0, 1}`. This is not a licence to include a
> tombstone — §8 forbids it outright — but a verifier must be able to READ one in order to REJECT it for the
> right reason, which is why the field is nullable in the model and why §9 fixes the order of the two checks.

> **Encoding note — hex, not base64.** Every binary value in this format is lowercase hex, matching the
> ledger's API surface, the canonicalization specification and its published golden vectors. Encoding one
> field differently from the rest would be a needless second convention to get wrong.

## 6. `SegmentDigest`

```
SegmentDigest = SHA-256( RowHash(SeqStart) ‖ RowHash(SeqStart+1) ‖ … ‖ RowHash(SeqEnd) )
```

Each `RowHash` contributes its **raw 32 bytes** (not its hex text), concatenated in ascending `SeqNo` order
with no separator. The input is therefore exactly `32 × RowCount` bytes.

A verifier **MUST** recompute this from the rows it read and compare it to `manifest.SegmentDigest`; a
mismatch is `Broken` (the manifest does not describe these rows). Comparing it to an **expected** digest
supplied by the caller is a separate step — that is the anchoring check of §1.

## 7. `CanonicalPrefix` — stored, but never trusted

Each line carries the canonical prefix that was hashed when the row was written. A conforming verifier:

1. **MUST** rebuild the canonical prefix **from the row's own columns**, following
   [Canonicalization v1 §2–§3](agent-audit-canonicalization-v1.md#2-canonical-prefix--field-order-frozen-for-v1);
2. **MUST** compare the rebuilt bytes to the stored `CanonicalPrefix` — a difference is `Broken`;
3. **MUST** compute `RowHash` from the **rebuilt** bytes, never from the stored ones.

**Why this rule exists.** Hashing the stored prefix as-is would make the check circular: an attacker who
edits a projection column (`AgentId`, `ActionType`, `TargetId`, …) and leaves the prefix and hash untouched
would pass. That is exactly why the ledger's in-database verification is **partial** by construction — SQL
cannot rebuild the prefix from the columns — and why the canonical verifier is the authoritative layer. This
format must not import that weakness: **the archive is verified by re-derivation, not by re-hashing.**

Storing the prefix therefore costs roughly double the payload and repeats the hashed text (`TargetId`,
`ErrorCode`) inside the archive; it is kept because it turns "the columns disagree with what was hashed"
from an undetectable state into an explicit, localised failure.

## 8. Segment constraints

A v1 segment **MUST NOT** contain a row with `RecordKind = 2` (a fold tombstone). A verifier that encounters
one **MUST** fail with a distinct **format error** — not `Broken` — naming the offending `SeqNo`.

**This check is ORDERED, and the order is normative: the tombstone check runs BEFORE the required-member
check** (§9 step 4). A tombstone legitimately has no `CanonicalPrefix` (§5), so a verifier that checked
required members first would reject a tombstone archive for a *missing field* rather than for *containing a
tombstone* — the same archive, two different reasons. Two implementations that chose differently would hand a
user two different explanations for one file, which is exactly the kind of silent divergence a normative
specification exists to prevent.

The live ledger cannot fold a range that already contains a tombstone (re-folding is refused), so no archive
produced by the fold flow can contain one. An archive that does was not produced by that flow.

> **Known limit — the producer is only PARTLY constrained.** The prohibition is enforced by this format and by
> the verifier, and *partly* by the reference producer:
> - The export **procedure** accepts an arbitrary `SeqNo` range and applies no tombstone check at all.
> - The export **endpoint** rejects a range whose interior contains an earlier fold — those rows are gone, so
>   the range is not dense, and the density guard refuses it. That covers the ordinary case.
> - It does **not** reject a range consisting of the tombstone **alone** (`[a..a]` is trivially dense), so a
>   caller invoking the API directly can still produce a one-row archive that this format forbids.
>
> So the guarantee is: the operator flow cannot produce such an archive, the common direct-API mistake is
> caught, and the narrow single-tombstone case is caught only by the verifier. Closing it fully needs a change
> to the export procedure and is tracked separately.

## 9. Verification algorithm (normative)

Given an archive and an optional `expectedDigest`:

1. **Open the container.** Read `manifest.json` and `segment.ndjson`. A missing entry is a format error.
2. **Check versions.** Unsupported `FormatVersion` or `HashVersion` → refuse (§2). Do not continue.
3. **Check the manifest's shape.** All twelve members present (§4); `RowCount == SeqEnd − SeqStart + 1`.
4. **Read the rows**, in this order — **the order is normative** (§8):
   a. parse each line and read its `RecordKind`; **if any row is `RecordKind = 2`, stop with the §8 format
      error naming that `SeqNo`** — before checking required members, because a tombstone legitimately lacks
      `CanonicalPrefix` and would otherwise be rejected for the wrong reason;
   b. **if any row's `HashVersion` differs from the manifest's**, stop with a format error (§2) — a mixed
      segment is not a v1 archive;
   c. assert every required member of every row is present (§5) — format error if not;
   d. assert the line count equals `RowCount`, and that `SeqNo` runs strictly ascending from `SeqStart` to
      `SeqEnd` with no gap or repeat.
5. **Per row, in order:**
   a. rebuild the canonical prefix from the columns (§7.1) and compare to `CanonicalPrefix` (§7.2);
   b. recompute `RowHash = SHA-256(PrevHash ‖ rebuiltPrefix ‖ SeqNo ‖ RecordedAtTicks)` per
      [Canonicalization v1 §1](agent-audit-canonicalization-v1.md#1-the-chain) and compare to `RowHash`;
   c. for every row after the first, assert `PrevHash == RowHash(previous row)`.
6. **Check the boundaries.** `PrevHash` of the first row equals `manifest.PrevHashAtStart`; `RowHash` of the
   last equals `manifest.RowHashAtEnd`.
7. **Recompute the segment digest** (§6) and compare to `manifest.SegmentDigest`.
8. **Anchor, if asked.** If `expectedDigest` was supplied, compare it to the recomputed digest:
   equal → `Verified`; different → `DigestMismatch`. If it was **not** supplied → `InternallyConsistent`,
   and say so.

**Which bucket a failure lands in.** Steps 1–3 and 4a–4c are **format errors**: the archive is not a v1
archive at all, so the integrity checks were never performed, and reporting one of §1's four results would
claim a check that did not happen. Step 4d onward is **`Broken`**: the archive parsed as v1 and then failed a
check — that is a finding about its content, not about its shape.

That is why a **line count that disagrees with `RowCount` is `Broken`, not a format error** (a deleted or
added row is precisely tampering, and the manifest is well-formed), while a **missing `RowCount` member is a
format error** (nothing can be compared). Same field, different failure, different bucket — deliberately.

The verifier **SHOULD** report the first offending `SeqNo` and which check failed — "the archive is broken"
without a position is not actionable.

## 10. Writing your own verifier

Everything above is implementable from this page plus
[Canonicalization v1](agent-audit-canonicalization-v1.md); no Asdamir code is required and none of it is
consulted at runtime. To check an independent implementation before pointing it at real data, run it against
the published [golden vectors](agent-audit-golden-vectors-v1.json) — they pin the canonical prefix bytes and
the resulting `RowHash` for a fixed `PrevHash`, `SeqNo` and `RecordedAtTicks`, which is the part most
implementations get wrong first (`NULL` versus empty string, GUID byte order, the little-endian length
prefix).

An implementation is conforming when, for every archive, it reaches the same one of the four results in §1
as the reference implementation, and refuses the same archives under §2 and §8.

**A conforming verifier also ships, in the open core, and you do not have to use it.** `Asdamir.Core` exposes
`AgentLedgerArchiveVerifier` (LGPL), and the CLI wraps it as
[`asdamir audit verify-archive`](../cli.md#audit-verify-archive--verify-a-folded-agent-audit-segment-offline) —
runnable with no AppManagement, no database, no network and no commercial licence, and reporting A and B
separately per §1. It is offered as a convenience, not as the definition: **this page is normative and the
shipped verifier is not privileged over yours.** If the two disagree, that is a finding worth raising — which
is the entire reason the format is specified rather than merely implemented.

## See also

- [Canonicalization v1](agent-audit-canonicalization-v1.md) — the frozen wire format of the hash chain.
- [Agent Audit](agent-audit.md) — what the ledger is for, what the guarantee is and is not, how folding works.
