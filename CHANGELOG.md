# Changelog — Asdamir

All notable changes to this repo. Format: [Keep a Changelog](https://keepachangelog.com/en/1.1.0/).
Versioning: [SemVer](https://semver.org/spec/v2.0.0.html).

The open-core packages (`Asdamir.Core`, `Asdamir.Data`, `Asdamir.Web`) share one version via
`Directory.Build.props`; `Asdamir.Payments` is cohort-aligned; the CLI (`Asdamir.Tools`) versions
independently.

**Currently published on nuget.org** — the table below is GENERATED from the manifest that also drives the
generated-app pins, so it cannot drift from what the feed actually serves. It replaces a hand-written
sentence that was, when this was written, three releases stale.

<!-- published-versions:begin — GENERATED from src/Asdamir.Tools/published-versions.json by
     packaging/sync-published-versions.sh. Do NOT hand-edit: edit the manifest and re-run. -->
| Package | Published on nuget.org | Next (in this repo) |
| --- | --- | --- |
| `Asdamir.Core` | `1.8.0` | — |
| `Asdamir.Data` | `1.5.0` | — |
| `Asdamir.Payments` | `1.2.0` | `1.3.0` built, pending publish |
| `Asdamir.Tools` | `1.7.0` | — |
| `Asdamir.Web` | `2.1.0` | — |

*A "Next" ahead of the published column is the normal pre-publish state — the version is built here but
not pushed yet. The published column is what a fresh `asdamir new app` pins.*
<!-- published-versions:end -->

**What the recent releases contain** — history, not a statement of the current version: (Web `2.1.0`: the FluentUI-**isolation facades, batch 2** — 11 more components (`AsdamirSelect`/`Stack`/`TextArea`/`Switch`/`Checkbox`/`DatePicker`/`RadioGroup`+`Radio`/`MessageBar`/`Label`/`Spacer`/`Anchor`) so caller-side raw `<Fluent*>` component usage is now 0. Web `2.0.2`: additive `Required`+`Class` on the input components. Web `2.0.0`: the FluentUI-**isolation facades** — callers no longer reference `Microsoft.FluentUI.*`; **BREAKING**, the notification service was renamed — see the entries below. Web `1.6.0` added the `AsdamirTextInput` + `AsdamirNumberInput<T>` input components. Earlier: central error visibility for generated apps — the `AppLogForwardSink`. Earlier: the shared INSPINIA theme as a static web asset + chrome components in Web `1.4.0`–`1.5.3`, the `audit permissions` / AUD016 gate — see the Tools 1.4.1 entry below; the `IBackgroundJobHandler` run-context — see the 1.5.0 entry below; the Gateway background-run primitive + the localization-completeness gate landed in 1.4.0). Earlier: **Tools `1.3.15`** (generated SQL bracket-quotes every table/column identifier so reserved-word field names stay valid, and the generated `run-tests.sh` keeps a Docker-free default run; generated apps enforce a nonce-based CSP + ship an audit trail; `new entity`/`new page`/`new feature`/`add field` run from the app root + auto-apply the generated migration, with `--no-db` to skip, and print a restart reminder after applying; generated apps bind the auth cookie to a server-side session registry so a restart / re-create ends the session; `rollback app` reads the DB connection from the Gateway user-secret + hides the vault line when the mode is undetermined; generated `restart-<app>.sh` frees the port; `new app` is generate → run: writes the
Gateway dev user-secrets + creates the DB + applies migrations; a profile menu + self-service change-password page in BOTH modes, and the forced first-login change-password flow removed). Data `1.2.1`'s FeatureManager value-type fallback fix shipped **inside Data `1.3.0`** (never published separately).

AppManagement (the commercial control plane) is not packed to NuGet — it ships as a compiled release for
commercial customers.

## [Unreleased] — 2026-08-09 — no package version moves

### Fixed: Archive Format v1 — the two places two conforming verifiers could still disagree

The format is published so third parties can implement it. Two questions could make two *conforming*
verifiers answer differently on the same bytes. **No archive's verdict changes**, and **no package version
moves** — the shipped C# verifier was already correct on both; what changed is the specification, the
independent Python fixture, and the tests.

- **§9.1 — a member that is PRESENT but not well-formed** (`"RowHash": "zzzz…"`, a 30-byte hash,
  `AuthorityKind: 256`) is now normatively a **format error**: the check that would have used the value never
  ran, so no verdict may be reported. The class is **counted** rather than gestured at — per row 7 hex,
  2 UUID and 8 integer members; in the manifest 3 hex and 1 UUID.
  **The honest part:** unlike the 2026-08-08 revision, this is not writing down settled behaviour — the page
  had left this **undefined**. Both existing implementations happened to agree, so agreement was luck rather
  than conformance, and a third could legitimately have read the silence the other way. The clarification
  banner says so in those words.
- **§9 step 4d — where the line-count check sits.** The page already said *last*; the Python fixture had it
  *first*. That is a real, measured divergence: on an archive breaking **both** the tombstone rule (4a) and
  the line count (4d), the two answered `BROKEN (3)` versus `FORMAT_ERROR (4)` — different bucket, different
  exit code, different reason, same bytes. No single-violation case could reveal it, which is why it survived.
  The fixture is corrected and the spec now states *why* the position matters.

**The shared cross-implementation matrix now collects EVERY divergence instead of stopping at the first** —
a matrix that aborts on case 3 hides cases 4–15, and the useful question is always *which inputs*, never
*whether any*. On a synthetic break it reports **3 of 15 cases disagreed**, by name.

### Fixed: the last three hand-written version claims — two generated, one deleted

The sweep the previous slice reported is now closed, and the decisions differ by file because the files do.

- **The public `README.md` was wrong in public.** Its first screen said `Asdamir.Core 1.6.0`,
  `Data 1.4.0`, `Web 2.0.1`, `Tools 1.4.5` while nuget served **`1.8.0`, `1.5.0`, `2.1.0`, `1.7.0`**. It is
  now a generated region like the CHANGELOG's — but rendered **inline**, because a README opens with a
  sentence and dropping a markdown table there would be worse than the drift it fixes. One source, two
  presentations; never two sources.
- **`CLAUDE.md`'s version sentence is DELETED, not corrected.** It had drifted six releases. A rule document
  that restates the fact it points at is one more copy to get wrong, so it now points and stops.
- **The dead `Asdamir.*` pins are DELETED from entframework and the public tree.** Measured: entframework
  resolves the open core through the `Exists()` `ProjectReference` branch, and the public tree has no
  `Asdamir.*` `PackageReference` at all — so those four `PackageVersion` entries were read by nobody and
  wrong by four releases. **Deleting is better than syncing**: under Central Package Management a
  `PackageReference` with no `PackageVersion` fails the build with `NU1010`, so a silent wrong number becomes
  a loud error. The commercial repo keeps its pins — it genuinely restores from them — and `verify-mirror`
  now compares them to the same manifest.

**A hole surfaced while proving the new gate could go red**, which is the entire reason for doing that: with
the README's region *edited* the gate failed correctly, but with the region *deleted* it PASSED — the
"no region" case was skipped so entframework's own README (which legitimately makes no version claim) would
not error. Closed: absence is a hard failure whenever `--root` names a mirror.

### Fixed: the last hand-written version claim is generated

The public repo's `CHANGELOG.md` opened with a hand-typed *"Current published state (nuget.org): …"* — after
`published-versions.json` and the `PublishedVersions` gate, the one version claim left in the project that
derived from nothing and was compared to nothing. **It was three releases stale.**

`packaging/sync-published-versions.sh` gained **`--root <dir>`**, so it generates into a mirror clone from
this repo's manifest (the mirror never carries its own copy of the truth), and `packaging/verify-mirror.sh`
runs `--check` for the public target. A stale block, or a deleted one, fails before the push — both proven.

**The sweep it asked for found three more**, reported and deliberately **not** fixed: the public `README.md`
states versions three to four releases stale on its first screen; `CLAUDE.md` states a set six releases
stale; and `Directory.Packages.props` disagrees across all three repos — though measurement shows only the
commercial copy is ever read, and that one is current. See the roadmap.

## [Tools 1.7.0] — 2026-08-09 — *pending publish*

### Fixed: `new app` with no name wrote a whole application and exited `0`

The name prompt fell back to the placeholder `GeneratedApp` when no console was attached. So
`asdamir new app` in a script, a CI step or a pipeline **scaffolded a complete 67-file, 364 KB application
into the current directory and exited `0`**, as though that had been asked for. `--yes` did the same, which
is worse: it is documented as *"accept every default"*, and a placeholder is not a default.

A missing app name is now a **usage error (`64`) and nothing is written**. `--yes` still accepts every real
default — the projects derive from the name, the database from the name, the SQL host from `localhost` — but
the name derives from nothing, so there is nothing to accept on the user's behalf.

**A mistyped option in the name position is now reported as one.** `asdamir new app --bogus` hands `--bogus`
to the command as the *name*. The exit code was already correct (`-` is not an uppercase letter, so the
PascalCase check rejected it), but the message blamed the user's capitalisation for what was a typo'd flag —
the harder of the two mistakes to spot, and the reader was sent to inspect a name that was never the problem.
One implementation now serves all seven commands that take a name; there were seven copies of the check.

**The last eight `Environment.Exit` calls are gone.** They killed the process from inside a handler, skipping
the rest of the invocation pipeline — which is what had kept these commands outside the contract test: its
first run *aborted the whole test session* instead of failing. They return exit codes now.

**Measured, then fixed, in that order.** The other file-writing commands — `new entity`, `new page`,
`new feature`, `add field`, `new mobile`, `new module`, `rollback` — were probed in a sandbox with a
before/after directory comparison for every usage error. All were already correct on both exit code and side
effects; only their message was improved. **One earlier claim of mine was wrong and is corrected here:**
`asdamir rollback Invoice --yse` does *not* fall through to the confirmation prompt of a destructive command.
It exits `64` and does nothing — `1.6.0`'s contract already covered it. It had been reported as a risk
without being measured.

**Enforcement — `ScaffoldSideEffectTests`**: 16 usage errors across 8 file-writing commands, each in its own
temporary directory, asserting the exit code **and** that the directory is unchanged, plus a positive case
(a valid invocation still scaffolds) and a pin that the placeholder never becomes a real app name. Reverting
the required-name fix turns **3** red; reverting the flag-detection **message** turns **3** red — that second
one is a message-only guarantee with its own test precisely because it changes no exit code, and an
improvement with no gate silently disappears. `CliExitCodeContractTests` now covers **17 commands × 7 shapes**.

`3` is documented as a **result**, not a usage error: a scaffolding command exits `3` when it refuses to
write into a non-empty target. The invocation was fine; the command examined the target and declined.

## [Tools 1.6.0] — 2026-08-09

### Fixed: a mistyped flag was indistinguishable from a failing gate — CLI-wide this time

`1.5.1` fixed this leak in `verify-archive` and left the other eight commands **unmeasured**. Measuring them
found the identical defect in **every one**, plus two crashes. "Unmeasured" is not "fine" — that assumption
was made three times in this phase and was wrong all three times.

**What was actually happening.** Usage errors that `System.CommandLine` answers *before* a handler runs
leaked into each command's RESULT band:

| Invocation | before | which the docs define as | now |
|---|---:|---|---:|
| `audit lint --pth src` (one transposed letter) | `1` | **"findings — fails the build"** | `64` |
| any gate: required option omitted / no value / stray argument / unknown alias | `1` | **"findings"** | `64` |
| `--version` on any subcommand | `1` | **"findings"** | `64` |
| `--help` on any command | `0` | **"clean"** (for `verify-archive`, **"VERIFIED"**) | `64` |
| `audit seeds --path ""`, `audit permissions --path ""` | `1` + **stack trace** | crash | `64` + one line |
| `db apply` from the wrong directory | + **stack trace** | crash | `64` + one line |
| every handler-raised bad argument (52 sites) | `2` | bad args | `64` |

So a typo in a CI script reported a **failing lint**, and `--help` reported a **clean gate**.

**One usage code for the whole CLI: `64` (`EX_USAGE`).** `2` — the previous house convention — could not be
it: in `verify-archive` `2` is a verdict (`DIGEST_MISMATCH`), so the choice was `64` everywhere or
"`2` except one command", and a rule with an exception is the kind that gets misremembered. A new `70`
(`EX_SOFTWARE`) marks an unexpected internal failure, so "the tool broke" is never reported as "you typed it
wrong". **Stack traces are gone from user-facing failures** — in an audit tool they leak absolute paths and
internals nobody asked for, and they read as a broken product when the cause was a bad argument.

**The rule is positive, not a blacklist:** a result code is possible only when a command handler produced it;
everything else is a usage error by construction. An enumerated list of today's parser errors goes blind the
moment the parser grows a new pre-handler outcome — which is exactly how the previous version went blind.

**`docs/cli.md` states the contract normatively, in one place**, with a before/after table for anyone pinned
to an older version. `audit lint` — the most-run gate in the repository — had **no documented exit codes at
all**; it has them now, and they were written as what the command *should* do, then the code was made to
match. Documenting the old behaviour would have made the defect official.

**Enforcement — `CliExitCodeContractTests`**, driving the real parse-and-invoke path over
**9 commands × 8 invocation shapes**. Its scope comes from a list, so covering a new command is one row.
Reverting the normalization turns **70 of 76** cases red; reverting only the crash handling turns **5** red,
naming both commands. The pre-existing per-command suites could not have caught any of this: they call
handlers, and these invocations never reach one.

**Also fixed, because it made the contract unenforceable:** four gates called `Environment.Exit` inside their
handlers. That skips the rest of the invocation pipeline and kills the process — the first run of the new
test *aborted the entire test session* instead of failing. They now set the invocation's exit code. Four
file-writing commands still use `Environment.Exit`; they are the next slice's scope and are listed in the
roadmap rather than left implicit.

## [Tools 1.5.1] — 2026-08-08

### Fixed: a mistyped command line exited `1`, and `1` means "the archive is intact"

`verify-archive` promises that exit codes `0`–`4` are claims about an archive and that a bad invocation stays
outside that band, on `64` — *"so 'invoked wrongly' can never be read as 'judged'"*. In `1.5.0` that held only
for the usage errors the **handler** raises. Everything `System.CommandLine` answers **before** the handler
runs leaked into the band:

| Invocation | `1.5.0` | means | `1.5.1` |
|---|---:|---|---:|
| `--pth ./segment.zip` (a typo) | `1` | `INTERNALLY_CONSISTENT` | `64` |
| `--path ./segment.zip --bogus` | `1` | `INTERNALLY_CONSISTENT` | `64` |
| `--path` omitted, or given with no value | `1` | `INTERNALLY_CONSISTENT` | `64` |
| a stray positional argument | `1` | `INTERNALLY_CONSISTENT` | `64` |
| `--help`, `-h`, `--version` | `0` | **`VERIFIED`** | `64` |

So `asdamir audit verify-archive --pth ./segment.zip` — one transposed letter — made a third party's audit
script log *"the archive has not been altered"* for a command that never opened it. `--help` returning `0` was
worse: `0` is the strongest claim the tool can make.

**Help and version are deliberately included**, against the usual convention that `--help` exits `0`. On this
command `0` does not mean *the program ran*; it means *this archive is unaltered and anchored to the ledger it
claims to come from*. Help cannot borrow that code. Nothing else in the CLI changes — the rule is scoped to
`verify-archive`, where the exit code is evidence.

**The check is positive, not a blacklist**: the handler is the only thing that may hand out a `0`–`4`, so
anything that did not reach it is a usage error by construction. A future parser that invents a new
pre-handler outcome is covered already — enumerating today's cases is precisely how the original went blind.

**Why the tests were green.** `VerifyArchiveCommandTests` asserts `64` for every usage error *the handler
produces*, and it passed on every one. It could not reach the others: they are answered before any handler
runs, so a test that calls the handler cannot see them. The new `VerifyArchiveExitCodeBandTests` drives
`Program.RunAsync` — the real parse-and-invoke path — over twelve measured invocation shapes, asserts the band
invariant directly, and pins both that a real archive still gets a real verdict and that a sibling command
keeps its own codes. Reverting the fix turns **11 of its 16** cases red.

**One near-miss worth recording**, because it was caught by measurement and not by review: the first attempt
replaced `root.InvokeAsync(args)` with `root.Parse(args)` + `parseResult.InvokeAsync()`. Those look equivalent
and are not — the second runs **without the default middleware**, so parse errors stop short-circuiting. The
handler then ran on malformed input (printing a genuine verdict for a mistyped line), crashed with an
unhandled exception on a valueless option (exit `134`), and — the serious one — turned `audit lint --bogus`
from an error into *"0 files scanned"*, exit `0`: **a build gate silently green**. The shipped fix keeps
`CommandLineBuilder(root).UseDefaults()` and normalizes afterwards; a test now pins the sibling command.

**Documentation**: `docs/cli.md` states the contract **normatively and exhaustively** in one place — these
values and no others — including the `1.5.0` caveat for anyone pinned to it. The archive format, its
semantics and the four verdicts are **unchanged**; nothing published in `1.5.0` reports a wrong verdict, it is
only more forgiving of a typo than it claims to be.

## [Core 1.8.0 · Tools 1.5.0] — 2026-08-08

### Added — you can now verify a folded archive yourself, offline

`Core 1.7.0` published the *specification* so that anyone could write an independent verifier. This
release ships one — **in the open core**, so running it costs nothing and requires nothing.

Until now the authoritative canonical verifier was `internal` to the commercial control plane. In an audit
product that is a contradiction: **if the only thing able to check the archive is the closed component that
produced it, the assurance collapses to "trust the vendor"** — a system's own clean report about itself is
not audit evidence.

- **[Archive Format v1](docs/fundamentals/agent-audit-archive-format-v1.md) is frozen and published.** A
  folded segment is a **ZIP** holding `manifest.json` + `segment.ndjson`: twelve manifest members, flat
  NDJSON lines, lowercase hex throughout, dense ascending `SeqNo`. Normative and complete — the format, not
  our implementation of it, is what a third party has to agree with.
- **New public API in `Asdamir.Core`** (additive; nothing removed or renamed): `IAgentActionCanonicalizer`,
  `AgentActionCanonicalizer` (was `internal`), `AgentLedgerArchiveVerifier`, `ArchiveVerificationResult`,
  `ArchiveVerificationStatus`, `AgentLedgerArchiveManifest`, `AgentLedgerArchiveRow`. **Core `1.7.0 → 1.8.0`.**
- **New CLI command** — `asdamir audit verify-archive --path <dir|zip> [--expected-digest <hex>] [--json]`.
  **Tools `1.4.6 → 1.5.0`.** No network, no database, no AppManagement, no licence; a test proves it by
  running the real tool from an empty directory with every proxy variable pointed at a dead port.
- **One implementation, not two.** The commercial tier's verifier is now a thin DTO adapter over this public
  Core type. Two copies of a frozen hash contract would not fail a build if they drifted — they would report
  an intact archive as broken, in the very tool a third party runs to check us.

**The honesty rule is enforced in code, not documented and hoped for.** An archive can attest to two
different things, and one tick for both would be a lie:

| Result | Exit | What it proves |
|---|---:|---|
| `VERIFIED` | 0 | not altered **and** anchored to the ledger it claims to come from |
| `INTERNALLY_CONSISTENT` | 1 | not altered — **UNANCHORED**, and it says so instead of showing green |
| `DIGEST_MISMATCH` | 2 | internally sound, but not that segment |
| `BROKEN` | 3 | a check failed, naming the offending `SeqNo` |
| *(format error)* | 4 | not a v1 archive — **no check ran**, so no verdict is reported |

Anchoring needs the `FoldSegmentDigest` recorded on the tombstone in the live ledger, passed as
`--expected-digest`. The `SegmentDigest` **inside** the manifest is not an anchor: it is derived from the
rows it accompanies, so whoever rewrites the rows recomputes it. The specification says so in its own
assurance-boundary section, and the tool refuses to blur it.

The verifier **re-derives**; it never re-hashes what it was handed. Each row carries the canonical prefix
that was hashed when it was written, and the verifier rebuilds that prefix **from the row's own columns**
before comparing — hashing the stored prefix would be circular, and someone who edited a projection column
while leaving prefix and hash alone would pass.

**Conformance is proven against an implementation that shares no code with ours.** A Python fixture written
from the published specification checks the real export, and a ten-case tamper matrix proves each check can
go RED, every case naming its position. Exit codes `0`–`4` are identical in both, so a script wired to one
behaves the same against the other.

Two places where two *conforming* implementations could still disagree are known and written down rather
than quietly left: a required member that is present but **not decodable** (a malformed hex hash, an
out-of-range enum), and the order in which the line-count and tombstone rules are applied. Both are
tracked, and both will land with the conformance cases that pin them — a clarification without a test is
how a specification drifts.

## [Core 1.7.0 · Data 1.5.0] — 2026-08-02

### Added — agent-audit primitives: recording what an AI agent did, in a checkable form

Ordinary audit logging answers *"what happened?"*. This answers *"can you show that this record has not
changed since it was written?"* — every record joins a SHA-256 hash chain, so changing, deleting, reordering
or inserting a row breaks the chain from that point on and verification reports the exact position.

- **`Asdamir.Core.AgentAudit`** (Core `1.6.0 → 1.7.0`, additive): `IAgentActionAuditor`, `AgentActionRecord`,
  `AgentAuthorityKind`, `AgentActionDecision`, `AgentActionOutcome`, `AgentAuditOptions` (with a nested
  `SinkFailureMode`) and `AgentAuditServiceToken`. The canonicalizer and the record validator are deliberately
  **internal** — the byte layout they produce is a frozen hash contract, not an API to program against.
- **`Asdamir.Data.AgentAudit.AgentAuditServiceCollectionExtensions.AddAgentAudit(...)`** (Data
  `1.4.0 → 1.5.0`, additive): the client sink — bounded queue, batched delivery, exponential backoff, and a
  **spool** so a control plane that is down does not silently lose records. A **permanent** rejection is moved
  to a separate dead-letter file with a persistent (not warn-once) alarm rather than retried forever; an
  **unknown** delivery status is treated as transient and only dead-lettered after the retry cap, because
  discarding what a newer server considered retryable is a silent audit gap.
- **The specification is published, not just the code**:
  [Canonicalization v1](docs/fundamentals/agent-audit-canonicalization-v1.md) — the frozen wire format (field
  order, byte encodings, the hash chain, the tombstone exception, the verification algorithm) plus
  [golden vectors](docs/fundamentals/agent-audit-golden-vectors-v1.json), so **anyone can write an independent
  verifier** in any language, offline. That is deliberate: if the only thing that can verify the ledger is the
  closed component being audited, the assurance collapses to *"trust the vendor"*.
- **Be precise about the guarantee.** Tamper-**evident**, not tamper-proof: a database owner can disable a
  trigger and edit a row — what they cannot do is make the chain agree afterwards. And it does **not** prove
  *who* acted: `AgentId` / `OnBehalfOfUserId` are the application's assertion, recorded immutably. The assurance
  level is *"application assertion + tamper-evident record"*, not *"the agent cryptographically proved its own
  identity"*. Per-agent cryptographic identity is a later phase; the signature columns are reserved and stay
  `NULL`.
- Verification is **two-layer and only one layer is authoritative**: the in-database check is PARTIAL by
  construction (it can only re-hash the body it stored), and the canonical verifier — which re-derives each
  record from its own columns — is the authority. The verdict is three-valued: `Valid` / `Degraded` / `Broken`,
  and **`Degraded` is never rendered as success**.
- The ledger itself, its verification/fold procedures and the operator screen are part of the **commercial
  control plane**, not of the open core; the open core is the contract, the client sink and the published spec.

## [Tools 1.4.6] — 2026-08-01

### Fixed — three ways a gate could be GREEN while the thing it guards was broken

The audit gates read SQL as **text**, so each new spelling blinded them. Two real blind spots were found and
fixed, and a third rule was added to stop the pattern repeating by constraining the input instead of forever
teaching the scanner.

- **AUD016 could be silently green.** Seeded permission codes were extracted with a plain string-literal regex
  over the raw file, so **one unpaired apostrophe in a comment** (`catalogue's`) shifted literal pairing for the
  rest of the file. The loud half is a false positive; the dangerous half is that comment prose could parse as a
  seeded permission, letting a policy that no seed satisfies pass the gate — a guaranteed 403 on a green build.
  Now lexed properly (comments stripped, `''` treated as an escape).
- **AUD015 was blind to every seed written through an upsert procedure** — 276 seeded keys across 20 migrations
  were read as "never seeded". All three procedure spellings are now recognised.
- **AUD019 (new)** — *an in-memory mirror does not substitute for a SQL seed*: a key mirrored into the
  in-memory seed but missing from the SQL migration (or short a culture) used to pass while production rendered
  the raw key. Its honest scope: **statically resolved keys only**.
- **AUD018 (new)** — a localization seed has exactly **one** approved spelling; **AUD017 (new)** — a permission
  grant must be an explicit name list, never a `LIKE` pattern (a wildcard grant is unlistable in principle: it
  confers whatever the catalogue happens to hold, and grants nothing to codes that do not match). Both are
  enforced by `asdamir audit seeds`, with a reviewed, commented grandfather allowlist instead of an inline
  suppression.

## [Web 2.1.0] — 2026-07-28

### FluentUI-isolation facades, batch 2 — caller-side component isolation is closed (11 new components)

Follows the batch-1 facades (theme/providers/spinner/button/notification/search + the `AsdamirTextInput` /
`AsdamirNumberInput<T>` inputs). This batch puts **every remaining FluentUI component the calling code used**
behind an Asdamir facade, so a future FluentUI major is a handful of facade files, not a whole-tree edit.
All additive (no removals); pure refactor on the v4 tree — the rendered behaviour is byte-for-byte the former
FluentUI behaviour. New components (native HTML + the shared `.asd-*` design tokens, **zero** FluentUI
dependency in the calling code):

- **`AsdamirSelect<TOption>`** — native `<select>` (replaces `FluentSelect`); generic item type but a **fixed
  `string` `Value` surface** so v5's now-generic Select API can't leak; `OptionText`/`OptionValue`, `@bind-Value`.
- **`AsdamirTextArea`** — native `<textarea>` (`InputBase<string?>`, `Rows`, `MaxLength`).
- **`AsdamirSwitch`** — accessible `role="switch"` toggle; **`AsdamirCheckbox`** — native checkbox (`Indeterminate`).
- **`AsdamirDatePicker`** — native `<input type="date">`; `DateTime?` `@bind-Value`, fixed ISO on the wire.
- **`AsdamirRadioGroup` + `AsdamirRadio`** — native radios sharing one group `name` via a cascading context.
- **`AsdamirStack`** — flex layout with its own orientation/alignment enums (no FluentUI enum leak);
  **`AsdamirMessageBar`** (`Intent`), **`AsdamirLabel`** (`Typo` → semantic `h1..h6`/`p`/`span`),
  **`AsdamirSpacer`**, **`AsdamirAnchor`** (`Appearance` → `.asd-btn` variants).

The framework's own security dialogs (forgot/reset-password, session-warning, access-denied) moved to
`AsdamirModal` + inline-SVG icons, and the consuming pages shed their now-dead `@using Microsoft.FluentUI`.
**Caller-side raw `<Fluent*>` component usage is now 0** (100% component isolation); the only remaining
FluentUI reference is the required `AddFluentUIComponents()` DI + package the facade toast/dialog services wrap.

## [Web 2.0.2] — 2026-07-27

### Additive — `Required` + `Class` on `AsdamirTextInput` / `AsdamirNumberInput<T>`

Two additive parameters bringing the isolation inputs to full parity with the `FluentTextField` /
`FluentNumberField` fields they replace: **`Required`** (renders the label asterisk + native
`required`/`aria-required`) and **`Class`** (extra CSS class on the field root, for width/layout). No breaking
change — existing bindings are unaffected.

## [Web 2.0.1] — 2026-07-27

### Fix — restore the input components that 2.0.0 accidentally dropped

`2.0.0` shipped **without** `AsdamirTextInput` / `AsdamirNumberInput<T>` (it branched before the `1.6.0`
input-components work had merged) — a regression against `1.6.0`. `2.0.1` brings them back, so Web `2.0.1`
has **both** the FluentUI-isolation facades (2.0.0) **and** the input components (1.6.0). Consumers on
`2.0.0` should move to `2.0.1`; no API change beyond the components reappearing.

## [Web 2.0.0] — 2026-07-27 — ⚠️ DEFECTIVE, superseded by 2.0.1

> **Do not use `2.0.0`.** It shipped **without** the `AsdamirTextInput` / `AsdamirNumberInput<T>` input
> components that `1.6.0` had added, so a consumer moving `1.6.0 → 2.0.0` silently **lost** them (the DLL
> of the published `2.0.0` has the facades but not the input types; `2.0.1` has both). Use **`2.0.1`** or
> later. `2.0.0` is being unlisted on nuget.org.

### FluentUI isolation facades — callers no longer reference `Microsoft.FluentUI.*`

New `Asdamir.Web/UI/Components` facades wrap FluentUI so calling code (AppManagement pages/layouts and
every generated app) stays off `Microsoft.FluentUI.*`. When FluentUI ships a breaking major, the change
is a handful of facade files, not the whole tree.

- **`AsdamirThemeProvider`** — wraps `FluentDesignTheme` (skin→accent map + light/dark). `AppTheme.razor`
  + `ServerAppTheme.sbn` now just render `<AsdamirThemeProvider />`.
- **`AsdamirAppProviders`** — the four overlay providers (toast/dialog/tooltip/message-bar) behind one
  opt-out-per-provider component.
- **`IAsdamirToastService` / `IAsdamirDialogService`** — Asdamir-owned toast/dialog services over FluentUI's
  `IToastService` / `IDialogService` (registered by `AddUIServices()`).
- **`AsdamirSpinner`** — wraps `FluentProgressRing`; all 25 loading placeholders migrated.
- **`AsdamirButton`** is now the only button — the last 19 raw `<FluentButton>` usages migrated; it gained a
  `Loading` parameter mirroring `FluentButton.Loading`.
- **`FluentSearch`** (removed in v5) — the single generated-CRUD-page search box (`Page.sbn`) is now a native
  `<input type="search" class="asd-input">` (a facade would be overkill for one call site); the generated
  CRUD page is now FluentUI-free (its `@using Microsoft.FluentUI` dropped).

### ⚠️ BREAKING — notification service renamed

`Asdamir.Web.UI.Services.INotificationService` / `NotificationService` are renamed to
**`IAsdamirNotificationService`** / **`AsdamirNotificationService`**. This resolves the `CS0104` ambiguity
with FluentUI v5's same-named `INotificationService`, and is why this is a **MAJOR** bump (Web `1.5.3` → `2.0.0`).

**Migration:** rename every `@inject` / DI registration / type reference:

```diff
- @inject Asdamir.Web.UI.Services.INotificationService Notify
+ @inject Asdamir.Web.UI.Services.IAsdamirNotificationService Notify

- services.AddScoped<INotificationService, NotificationService>();   // (AddUIServices already does this)
+ services.AddScoped<IAsdamirNotificationService, AsdamirNotificationService>();
```

The API surface (methods, events, `NotificationOptions`, the `Notification*Request` records, the enums) is
otherwise unchanged — only the interface + implementation-class names changed. `<AsdamirNotificationHost />`
and `AddUIServices()` are updated internally; apps that only use the host + `@inject` need just the rename.

## [Web 1.6.0] — 2026-07-26

### FluentUI-isolation input components — `AsdamirTextInput` + `AsdamirNumberInput<T>`

Two new framework UI components in `Asdamir.Web/UI/Components`, the first of the FluentUI-**isolation**
front: a native `<input>` styled with the shared `--asd-*` / Fluent-2 design tokens, with **zero**
`Microsoft.FluentUI.*` dependency. Consumer code (generated apps, AppManagement) can bind to a framework
field without taking a dependency on the FluentUI component API, so the FluentUI surface can evolve behind
the framework boundary. Both derive from `InputBase<TValue>`, so `Value`/`ValueChanged`/`ValueExpression`
flow through the ambient `EditContext` and DataAnnotations validation works with no extra wiring. No JS
interop.

- **`AsdamirTextInput`** — `MaxLength` (native + component clamp), a caller-supplied `AllowedCharacter`
  predicate applied to **typing AND paste/IME**, `InputType` (text/password/email/tel/search/url; a non-text
  type throws), `Disabled`/`ReadOnly`, `Immediate`, `@attributes` splat.
- **`AsdamirNumberInput<TValue>`** — `TValue` constrained to `decimal`/`int`/`long` (`double`/`float`
  rejected at runtime, since binary floating point can't represent decimal money exactly). Uses
  `type="text"` + `inputmode="decimal"` (NOT `type="number"`, which reinterprets the separator per browser
  locale, ignores `maxlength`, and lets the wheel change the value) with a **culture-aware** parse/format
  (decimal separator from `CultureInfo.CurrentCulture`, group separator tolerated on parse),
  `MidpointRounding.ToEven`, `Decimals`/`Min`/`Max`/`AllowNegative`/`MaxIntegerDigits`, wheel neutralized via
  `@onwheel:preventDefault`, caret stable while typing (re-format only on commit).

Accessibility: `<label for>`, `aria-invalid` + `aria-describedby` → a `role="alert"` message. User-facing
text (`Label`/`Placeholder`) is an English-defaulted parameter — the caller passes `@L["…"]` in. 29 new
bUnit tests (value binding, EditContext/DataAnnotations, typing + paste filters, tr-TR↔en-US culture
round-trip, ToEven rounding, bounds, wheel-safety, `double`/`float` rejection). No new business rule; existing
`FluentTextField`/`FluentNumberField` usages are not migrated (a later increment).

## [Core 1.6.0 · Data 1.4.0 · Tools 1.4.5] — 2026-07-26

### Added — central error visibility for generated apps (the AppLog forward sink)

A scaffolded Gateway now wires **three Serilog sinks** — Console + File (local) + a new **`AppLogForwardSink`**
(`Asdamir.Data.Logging`) that forwards **Warning+** events to the control plane's authenticated ingest
endpoint (`POST api/admin/applogs/ingest`). Before this, a generated Gateway had **no Serilog at all** — no
local file log and nothing central, so every Gateway `500` was invisible in central error monitoring.

- The ingest endpoint resolves the app's id **from the Gateway's `app-log` service token — never from the
  request body**, so one app can only write its own log slice (a forged app id is rejected).
- The sink is **fail-safe** (a down/slow ingest never blocks or crashes the app — it keeps Console+File),
  **no-loop** (never forwards its own HTTP-transport logs), and **batched + bounded** (async `Channel`,
  drop-oldest when full).
- **Opt-out** via config `AppLog:ForwardToCentral` (default `true`). **Free mode** has no control plane, so
  only Console + File are wired.
- New public API: `Asdamir.Core.ErrorHandling.Logging.AppLogServiceToken` (**Core `1.6.0`**);
  `Asdamir.Data.Logging.AppLogForwardSink` + `AppLogForwardOptions` (**Data `1.4.0`**);
  `GatewayProgram.sbn` emits the 3-sink wiring (**Tools `1.4.6`**). `SerilogBootstrap.UseWithDatabase` is now
  `[Obsolete]` (direct-DB sink — use the forward sink in a generated app).

**Existing generated apps:** an app from an earlier CLI has no Serilog — add the 3-sink
`builder.Host.UseSerilog(…)` block to your Gateway `Program.cs` (or regenerate), and bump
`Asdamir.Core`/`Data` to `1.6.0`/`1.4.0`.

## [Tools 1.4.1] — 2026-07-22

### Added — (CLI, `Asdamir.Tools`) `asdamir audit permissions` (AUD016) — permission/policy-completeness gate

A static gate that cross-checks every `perm` value a Gateway authorization policy **requires**
(`RequireClaim("perm","X")` / `HasClaim("perm","X")`, incl. inside `RequireAssertion(ctx => …)`) against the
codes the tree's SQL seeds **supply** — the `Name`s in `dbo.Permissions` plus the role codes in
`dbo.Roles` / `dbo.UserAppRoles`. A policy requiring a `perm` that is neither a seeded permission nor a role
code can never be satisfied (the app-login JWT carries role codes + granted permission codes as `perm`
claims) → every request silently `403`s, and claim-injecting tests miss it. `--path` is repeatable (point one
at `src`, one at `db`); **any finding fails the build (exit 1)**. Sibling of the AUD015 localization gate;
suppress a policy line with `// audit-lint:ignore AUD016`.

## [1.5.0 — Core 1.5.0 · Data 1.3.1] — 2026-07-21

### Changed — BREAKING (`Asdamir.Core 1.5.0`): background-run handler takes a run context

`IBackgroundJobHandler.ExecuteAsync` now takes a single **`BackgroundRunContext`** instead of the loose
`(string? payload, IProgressReporter progress, CancellationToken ct)`:

```csharp
Task<string?> ExecuteAsync(BackgroundRunContext context);   // was (payload, progress, ct)
```

`BackgroundRunContext` (`Asdamir.Core.BackgroundRuns`, a `sealed record`) carries everything the runner
already knows about the run in ONE object — `RunId`, `TenantId`, `Payload`, `Progress`, `CancellationToken` —
so future additions extend the context without another signature break (no loose-params + context mix).

- **Why:** the old signature gave the handler **no RunId**, so a job body could not write a back-link from
  its own business record to the framework's `BackgroundRuns` row — two-way audit traceability was
  impossible. `context.RunId` is now the same value `EnqueueAsync` returned and `GetStatusAsync(runId).RunId`
  reports.
- **Migration:** replace `ExecuteAsync(payload, progress, ct)` with `ExecuteAsync(context)` and read
  `context.Payload` / `context.Progress` / `context.CancellationToken`; use `context.RunId` for the back-link.
  The API is fresh (published `1.4.0`), so there is **no `[Obsolete]` bridge** — a coordinated bump.
- **`Asdamir.Data 1.3.1`** (patch): the hosted runner builds the context and calls `ExecuteAsync(context)`;
  its own public surface is unchanged. Web / Payments / Tools unchanged (the `new app` template emits no
  handler body, so generated apps are unaffected).

## [1.4.0 — Core 1.4.0 · Data 1.3.0 · Web 1.3.1 · Tools 1.4.0] — 2026-07-20

### Added — Gateway background-run / progress primitive (Core 1.4.0 + Data 1.3.0)

The reusable way to run a heavy operation off the request thread: **trigger → run in the background → poll
status/progress**, so a long op (a large reconciliation, a bulk import) never blocks a request.
`IBackgroundRunService` (enqueue → RunId; get status = state + progress + result ref + error summary) with an
app-defined `IBackgroundJobHandler` (keyed by a `JobType` string — it can wrap an existing engine without
changing its signature). Durable per-run state (survives a restart — a ghost "Running" is recovered to
`Interrupted` at startup), throttled progress reporting (no per-row DB write), a default reject-duplicate
concurrency policy, and a single-node HA note. Self-hosted `BackgroundService` + channel (no external broker).
`AddBackgroundRuns()` + `AddBackgroundJob<T>()`. See [Background Runs](fundamentals/background-runs.md).

### Added — generator wiring for the background-run primitive (Tools 1.4.0)

`asdamir new app` (BOTH modes) now scaffolds the primitive by default: `AddBackgroundRuns` in the Gateway, a
fail-closed tenant-scoped `GET /background-runs/{id}` status endpoint (another tenant's run id → 404), and the
store migration into the app's own DB. No job handlers are registered — the runner idles until you add one.

### Added — localization-completeness gate (Tools 1.4.0)

- **`audit localization` (AUD015)** — a static gate that cross-checks every `L["…"]` used in `.razor`/`.cs`
  against every seeded key; a key that is unseeded or seeded in fewer than all three cultures is a build
  error (the raw key would otherwise render on screen). Dynamic `L[$"…{x}"]` keys are reported as INFO.
- **`localization verify`** — diffs the seed files' `(key, culture)` pairs against the live database to catch
  a key that is seeded but was never applied (apply-drift).

### Changed — `ApiStringLocalizer` missing-key warning deduped (Web 1.3.1)

The "localization key not found" warning now fires once per (cache-generation, culture, key) instead of on
every resolution, so a missing key surfaces in the logs without flooding them.

### Fixed — FeatureManager value-type fallback (Data 1.3.0, folds in the never-published Data 1.2.1)

The Data `1.2.1` patch (FeatureManager value-type fallback fix) ships inside Data `1.3.0` — it was never
published as a separate package.

## [Tools 1.3.15] — 2026-07-19

### Added — generated apps: profile menu + self-service password change (BOTH modes)

- **Profile menu (topbar):** every generated app's `MainLayout` now renders the avatar/name as a native
  `<details>/<summary>` dropdown (no JS, keyboard accessible, CSP-clean) with **Change Password**
  (→ `/change-password`) and **Sign out** (moved inside the menu — no standalone logout icon). New localized
  keys `App.Shell.UserMenu` / `App.Shell.ChangePassword` seeded in both models (tr/en/ru).
- **Self-service `/change-password` page in BOTH modes** (previously free-only and forced): a neutral
  current + new + confirm card rendered inside the app shell (`current-password`/`new-password`
  autocomplete). Both modes post to the SAME route, **`gateway/auth/change-password`** — the free Gateway
  serves it locally (unchanged endpoint), the commercial Gateway gains a **new proxy** to AppManagement's
  `app-change-password` (appCode injected exactly like the login proxy; failures surface the engine's ONE
  opaque localized ProblemDetails via `ToUserMessageAsync`). On success every refresh token is revoked
  server-side, so the page signs the user out and returns to login with a localized success note
  (`App.ChangePw.Done`).

### Removed — the forced first-login password-change flow (free mode)

- Product decision (Orhan): **no app forces a first-login password change; every app offers self-service
  instead.** The login redirect (`MustChangePassword`), the `AppLoginResponse` flag, the
  `User_GetForcePasswordChange` proc + repository read and the `ForcedHint` copy are gone; the free seed now
  creates the starter admin with `ForcePasswordChange = 0` (the column stays, defaulted off, and
  `User_ChangePassword` still resets it).
- **Trade-off, accepted:** the printed starter/bootstrap password now stays valid until the operator changes
  it — change it promptly via the profile menu (the docs say so explicitly).
- E2E updated: the free-app slice now walks login → **direct dashboard** → profile menu → self-service
  change → revoked sessions → re-login with the new password.

## [Web 1.3.0] — 2026-07-18

### Changed — `AsdamirJsonFilePicker` → `AsdamirFilePicker` (Web; general-purpose, real drag & drop)

The Asdamir.Web file-picker component is renamed and generalized into a **content-agnostic**
`AsdamirFilePicker`. The old component's JSON-specific logic (parse/validate/analyze/preview and its
models) had zero usage — dead code — and is removed; the component now only selects a file and hands the
`IBrowserFile` to the caller (`OnFileSelected` / `OnCleared` — the caller streams the content). Drag & drop
is now REAL: the raw `<InputFile>` stretches invisibly over the styled drop zone (via a `::deep`-scoped
overlay — a child component's rendered input carries no CSS-isolation scope attribute), so click-to-browse
AND a native drop land on the same input — no JS interop. Surface: `Accept` (also enforced component-side,
since a drop bypasses the browse-dialog filter), `MaxFileSize`, `Disabled`, `IsProcessing`, text parameters
with English defaults, a parent-side `Clear()`. Single-file only. This is a breaking rename for any external
consumer of the old component. Published as `Asdamir.Web 1.3.0`.

## [Tools 1.3.14]

### Fixed — reserved-word column names now produce valid SQL (identifier bracket-quoting)

`asdamir new entity` / `new feature` / `add field` now **[bracket]-quote every table and column identifier** in
the generated DDL, CRUD stored procedures, and sample-seed migration. Before this, a field whose name is a
T-SQL reserved word — `RowCount`, `Order`, `User`, `Group`, `Key`, `Percent`, … — emitted a bare
`RowCount INT NOT NULL` column definition, which fails at apply time with *"Incorrect syntax near the keyword
'RowCount'"* so the `CREATE TABLE` never runs. Bracketing is applied to **all** identifiers (not a reserved-word
allowlist, which can never be complete); parameters (`@Name`) are intentionally left unquoted — a reserved word
is valid as an `@`-prefixed parameter name. Verified end to end: a reserved-word entity migration now applies to
a real SQL Server and round-trips, pinned by a `Category=Scaffold` regression test.

### Changed — generated `run-tests.sh` excludes `Category=Integration`/`E2E` from the fast default run

The scaffolded `run-tests.sh` now runs `dotnet test --filter "Category!=Integration&Category!=E2E"`, so once a
developer adds Docker-only Integration or real-browser E2E tests to a generated app, the default fast run stays
Docker-free (run them explicitly with `--filter "Category=Integration"`). A fresh app has no such tests, so the
filter changes nothing for it.

## [Tools 1.3.12]

Generated apps (`asdamir new app`, free + commercial) gain two direct security/observability capabilities,
both proven end to end by the scaffold + browser E2E.

### Added — generated apps now enforce a nonce-based Content-Security-Policy

The Server previously shipped with CSP off. Generated apps now wire `UseCspNonce` + a strict
`Content-Security-Policy` (`script-src 'self' 'nonce-…'`); `App.razor` stamps the same per-request nonce
(URL-safe base64 so header + attribute match) on its single inline `<script>`. The change-password page's
inline toggle folds into that one nonced block (no per-page scripts), and the external Google-Fonts `@import`
is dropped (it violates the strict policy — self-host a woff2 to restore Inter).

### Added — generated apps now have an audit trail

A global `AuditActionFilter` records every state-changing request (POST/PUT/PATCH/DELETE) to the app's OWN
`dbo.AuditLog` — who did what, to which target, with what outcome. `AuditTrailController` serves it at
`GET gateway/admin/audittrail`; endpoints opt out with `[SkipAudit]` and refine the label with `[AuditAction]`.
The `dbo.AuditLog` table + `AuditLog_Insert`/`AuditLog_List` procs ship in the app's schema migration.

## [Tools 1.3.11]

### Fixed — generated apps: a session survived app teardown / restart (server-side session registry) (`Asdamir.Tools`)

A generated app's auth cookie was **self-contained**: the server held no session, so a cookie stayed valid
as long as it decrypted and hadn't expired. Two things combined into a real hole: **(1)** Data Protection
keys persist **outside** the app folder (`~/.aspnet/DataProtection-Keys/`, keyed by the stable
`DataProtection:ApplicationName`), so **deleting an app and re-creating it under the same name reused the same
key ring** and the old cookie still verified; **(2)** cookie auth had no server-side validation, so a restart
or a fresh (empty) DB didn't drop the session — the old cookie **landed on the dashboard without a login**.

The generated Server template now ships a server-side session registry:

- A new **`Auth/AppUserSessionStore.cs`** — an in-memory (singleton) registry of active sessions by `sub`.
- **Sign-in registers** the session (`Login.razor` → `Sessions.Register(sub)`); **sign-out removes** it
  (`AuthEndpoints` `/logout`).
- **`OnValidatePrincipal`** rejects any cookie whose `sub` is not in the store. Because the store is
  in-memory, a **process restart clears it → every cookie is rejected until the user signs in again**, and a
  torn-down + re-created app starts with an empty store.

Verified end-to-end on a free-mode app: same auth cookie returns **HTTP 200** on `/` before a restart and
**HTTP 302 → /login** after.

## [Tools 1.3.10]

### Added — restart reminder after `new entity`/`new page`/`new feature`/`add field` auto-applies a migration (`Asdamir.Tools`)

Now that these commands **apply the migration by default** (1.3.9), the one missing step was easy to forget:
a running generated app **caches its DB-backed menu + localization + config at startup** and **registers new
controllers at startup**, so a freshly-applied page/menu/field does **not** appear until the app is restarted.
That produced the confusing "I added a page but its menu didn't show" — the DB was correct; the app was still
serving its startup cache.

The scaffolders now print a restart reminder **after a migration is actually applied** (not on `--no-db`),
naming the app's own script:

```
✓ Feature 'Order' ready.
  ↻ Restart the app for the change to show (menu/localization is cached at startup):  ./restart-myapp.sh
```

- One reminder per command — `new feature` suppresses the entity/page steps' reminders and prints a single one.
- Locates the app's `restart-<app>.sh` by glob (every generated app ships one); falls back to a generic line.
- Commercial `new page` (whose AsdamirVault seeds are applied separately) prints a matching "then restart"
  note next to its "apply to AsdamirVault" guidance.

## [Tools 1.3.9]

### Changed — `new entity` / `new page` / `new feature` / `add field` run from the app root + auto-apply the migration (`Asdamir.Tools`)

These four commands no longer need you to `cd src/<App>.Gateway` first — run them **from the app root** and
each finds the right project itself (the nearest `.sln`, then `src/<App>.Gateway` / `src/<App>.Server`).
Running from inside the Gateway/Server directory still works (backward-compatible); `--output` /
`--gateway-dir` / `--server-dir` override the auto-detection. If you're not inside an Asdamir app they now
fail fast with *"Not inside an Asdamir app (no .sln found…)"* instead of scaffolding into the wrong place.

They also **apply the generated migration by default** — through the same journaled `db apply` runner,
resolving the connection from the Gateway user-secret `ConnectionStrings:Default` (the passwordless
resolution `db apply` already uses; explicit `--connection`/`-S`/`-d`/`-U`/`-P` override it). So
`asdamir new feature Order …` from the app root scaffolds **and** creates the table in one step — no `cd`,
no separate `db apply`.

- **`--no-db`** (new, on all four) scaffolds files only — don't touch SQL (offline / CI / review-first).
  It prints the exact `asdamir db apply --migrations <path>` line to run later.
- If **no connection is resolvable** (no secret, no flags), the migration is still **generated** and the
  command prints the `db apply` recovery line — never left blind, never a hard failure.
- `--apply` (the old opt-in flag on `new entity` / `new feature`) is now a **deprecated no-op** — apply is
  the default; use `--no-db` to opt out.
- `db apply` is unchanged and stays — it's what you run over the app's lifetime (clone, CI, prod);
  `new entity`/`new feature` just run it once for you at generation. Idempotent (the journaled runner skips
  already-applied migrations).

## [Tools 1.3.8]

### Fixed — `rollback app`: hide the AsdamirVault line when the mode can't be determined (`Asdamir.Tools`)

When the app **directory was already gone**, `rollback app` couldn't tell free from commercial (the free signal
lives in `db/migrations`), so it fell back to printing the commercial vault lines — a scary, irrelevant
"App registration (AsdamirVault): NOT purged …" for what may well have been a free app that never had a
registration (and nothing could act anyway without a connection). The vault line is now shown **only** when the
app is **known-commercial** (its directory is present and not free-mode) **or** `--vault-connection` was passed
explicitly (honoured in any mode). Undetermined mode + no `--vault-connection` → **silent** (the `App DB` line
stays — it's still actionable with `--connection`). Behaviour unchanged; visibility only.

## [Tools 1.3.7]

### Fixed — `rollback app` resolves the DB connection from the Gateway user-secret (no orphan DBs) (`Asdamir.Tools`)

`rollback app <Name>` said "App DB: SKIP — no connection (database not dropped)" even though `new app` had
prompted for the SQL password and stored it in the Gateway user-secret (`ConnectionStrings:Default`) — the same
value `db apply` reads. So `rollback app` deleted the directory but left the database behind (an orphan). It now
resolves the connection in the **same order as `db apply`** (reusing its resolver, no duplication): explicit
`--connection` → `-S/-d/-U/-P` flags → the **Gateway user-secret**. So `rollback app <Name>` with **no flags**
drops the DB. The secret is read **before** the directory is deleted (it holds the UserSecretsId); the
confirmation shows the resolved server + database and its source (**never** the password), and DROP DATABASE
stays idempotent (`IF DB_ID IS NULL`). Works in free + commercial mode.

## [Tools 1.3.6]

### Fixed — `rollback app`: clearer AsdamirVault wording + hidden in free mode (`Asdamir.Tools`)

`rollback app`'s teardown lines read `AsdamirVault: NOT purged …`, which sounded like the **AsdamirVault
database** could be purged (it can't) and frightened users. Two fixes: (1) the wording is now **"App
registration (AsdamirVault)"** and spells out that it removes the app's **registration + AppId-scoped rows** via
`App_Purge`, **NOT** the AsdamirVault DB (which is never dropped); (2) a **free-mode** app has no control-plane
registration, so the line is **hidden entirely** (no more "N/A — free-mode app" noise). Behaviour is unchanged —
only the messaging and the free-mode visibility. When the mode can't be determined (the app directory is already
gone), the line still shows, with the clear wording.

## [Tools 1.3.5]

### Changed — generated `restart-<app>.sh` frees the PORT, not just the process by name (`Asdamir.Tools`)

The generated restart script killed the old tiers by process name (`pkill -f "<App>.Gateway"/"<App>.Server"`),
which misses the real failure: a **different** app squatting the port (e.g. DemoPay's Server on `7010`) — name-kill
never finds it, so the new app can't bind. Restart stopping is now **two-layered**: the name-targeted kill (kept)
**plus** a **port-targeted** one that frees the Gateway/Server port whoever holds it (`lsof -ti:<port>`, `fuser`
fallback). It **warns before killing another process** ("Port 7010 is held by PID … — stopping it."), then
**verifies the port actually freed** (3×1s, escalating to SIGKILL) and **fails fast** (`exit 1`) rather than a
blind start into a bound port. Ports are parsed from the script's `GATEWAY_URL`/`SERVER_URL` (not hardcoded).

## [Tools 1.3.4]

### Changed — `asdamir new app` creates the database + applies migrations too (generate → run) (`Asdamir.Tools`)

Building on the auto-secret configuration, `new app` now also **sets up the database** — it runs
`db apply --create-database` for you (reusing the SAME journaled runner, no duplication; `CREATE DATABASE` is
idempotent via `IF DB_ID IS NULL`, and applied migrations are skipped). So a free app is **generate → run**:
`asdamir new app` → `./restart-<app>.sh`. It runs only when a real password was supplied (a masked prompt or a
full `--connection-string`); an **empty password** or the new **`--no-db`** flag scaffolds files only and prints
the `db apply` line for later (offline / CI / review-first). If the DB setup fails (server unreachable, no
rights) the files are still generated and the exact `cd <app> && asdamir db apply …` recovery command is printed
— never left half-done. `db apply` itself is unchanged and still used over the app's lifetime.

## [Tools 1.3.3]

### Changed — `asdamir new app` is run-ready: it auto-configures the Gateway dev user-secrets (`Asdamir.Tools`)

`new app` already asks for the SQL user + a masked password; it now **writes the Gateway's dev user-secrets**
itself so the app runs with no hand-editing — cutting the old 6-step "next steps" checklist to **2 commands**
(`asdamir db apply` → `./restart-<app>.sh`). It sets: a **CSPRNG `Jwt:Key`** (free mode only — the Gateway owns
its JWT; commercial mode's `Jwt:Key` must equal AppManagement's, so it stays manual), a **CSPRNG
`Security:EncryptionKey`**, and **`ConnectionStrings:Default`** (only when a real password was supplied — masked
prompt or a full `--connection-string`; an empty password keeps the printed manual line). Secrets go to
**user-secrets, NEVER `appsettings.json`** (the security model is unchanged), and the masked password is never
echoed back. New **`--no-secrets`** flag opts out (CI / external secret store) and restores the full manual
block. `--yes`/CI is unaffected (the CSPRNG keys are still generated; the connection string comes from
`--connection-string` if given).

## [Tools 1.3.2]

### Added — `asdamir rollback app <Name>`: whole-app teardown, the symmetric inverse of `new app` (`Asdamir.Tools`)

`rollback` gained an `app` subcommand that tears down a generated app end-to-end — the inverse of `new app`
(previously there was no CLI path back; you had to `DROP DATABASE` + `rm -rf` by hand). It removes: the generated
**directory** (the dir whose `<Name>.sln` exists — the guard against deleting the wrong dir), the app's **OWN
database** (`DROP DATABASE`, free **and** commercial), and — in commercial mode with `--vault-connection` — the
**AsdamirVault registration** + all AppId-scoped rows via the existing `dbo.App_Purge` proc. DESTRUCTIVE +
interactive by default (shows the full path + server/database + vault code, asks `[y/N]`; `-y`/`--yes` for
scripts). Fail-closed: it NEVER drops a protected DB (`AsdamirVault`/`master`/`model`/`msdb`/`tempdb`) and
`App_Purge` refuses the self-app. Every step is conditional + idempotent (a missing dir/DB/registration is
"already gone", never an error). Implemented as a subcommand, so the bare `rollback <Feature>` form is unaffected.

## [Tools 1.3.1]

### Fixed — `asdamir new app` prompts for SQL auth (user/password), not a Windows-only Trusted_Connection (`Asdamir.Tools`)

The interactive `new app` flow defaulted the connection string to `Trusted_Connection=True` (Windows integrated
auth), which is not portable to Linux/macOS/containers. It now asks for the **SQL user** (default `sa`) and a
**masked SQL password**, and composes a cross-platform **SQL-auth** connection string
(`Server=<host>,1433;Database=<db>;User Id=<user>;Password=…;TrustServerCertificate=True;`). A real password is
never written to `appsettings.json` (left empty, secret-free) — it goes to `dotnet user-secrets` per the printed
next steps (which use the entered user + a `<your-password>` placeholder, so the masked password is never echoed
back). The generated Gateway smoke-test factory's placeholder connection string was made portable too.

## [Data 1.2.1]

### Fixed — `FeatureManager.GetConfigurationAsync<T>` global fallback for value types (`Asdamir.Data`)

- **`GetConfigurationAsync<int>` / `<bool>` / any value type now correctly falls back to the global key.** It
  decided the tenant-scoped key's presence with `Get<T>() is not null`, but a value type binds to `default(T)`
  (0 / false) when the key is ABSENT — so the guard passed and the global fallback was unreachable, returning
  `default(T)` instead of the global value. Now uses `IConfigurationSection.Exists()`. Reference types were
  unaffected. Patch over the published `1.2.0`.

## [Core 1.3.0]

### Added — `Jwt:ConsoleAudience` for a cryptographic control-plane token boundary (`Asdamir.Core`)

- **`JwtService` now supports an optional distinct audience for control-plane tokens.** When
  `Jwt:ConsoleAudience` is configured, a token minted with `token_use=console` is stamped with THAT audience
  instead of `Jwt:Audience`; every other token keeps `Jwt:Audience` unchanged. This lets a host run two
  audience-scoped JWT bearer schemes so a lower-privilege token is rejected at the **authentication layer** on
  control-plane endpoints — not merely by a claim filter. **Additive and backward-compatible**: unset →
  console tokens fall back to `Jwt:Audience` (prior behavior). **Only `Asdamir.Core` is bumped;
  `Asdamir.Data`/`Web`/`Payments` remain `1.2.0` (no code change).**

## [Tools 1.3.0]

### Added — opt-in end-user billing scaffold (`asdamir new app --billing`)

- **`asdamir new app --billing`** (off by default) scaffolds an **end-user payment page** into a generated
  app (commercial mode only). It emits, all fully conditional on the flag:
  - a **Payment page** (`Payment.razor` at `/billing`) — lists plans, shows the current subscription, starts
    checkout with a **redirect to the tenant's Paddle hosted page** (pass-through Merchant-of-Record). When
    Paddle isn't configured yet it shows a calm localized message (never a raw status code / crash). Scoped
    CSS, no inline style, no inline script (CSP-safe).
  - a **Gateway proxy** (`gateway/billing/*` → AppManagement `api/admin/billing/*`) — forwards the bearer
    (whose `app_code` claim AppManagement uses to resolve the app's `AppId`); holds no DB, no Paddle secret.
  - an **AsdamirVault seed** (`db/admin-onboarding/seed_billing.sql`) — the `billing.view` permission + Admin
    grant + `/billing` nav menu row + `Billing.Page.*` / `Menu.Billing` localization (tr-TR/en-US/ru-RU) +
    `Payment:Paddle:*` / `Payment:Crypto:*` config templates (secrets seeded empty + encrypted, per-tenant).
- **Backward-compatible:** WITHOUT `--billing` a generated app is byte-identical to before — not one billing
  file is emitted.
- **`--billing --mode free` (Model B) is supported** — a free app gets **self-contained** billing: the
  Gateway serves billing LOCALLY from the app's own DB via the new open-core **`Asdamir.Payments`** package
  (`LocalDbBillingStore` + Paddle/crypto rails + local webhook) — no control plane, no central secret. It
  emits a local Gateway billing + webhook controller, the shared Payment page, and the app-own-DB billing
  schema/procs/seed migrations. Model A (commercial) output is unchanged.

## [Asdamir.Payments 1.2.0]

### Added — new package: the payment rails

- **`Asdamir.Payments`** (nuget) — the concrete payment plumbing on top of `Asdamir.Core`'s `IPaymentProvider`
  contract: `PaddlePaymentProvider` (Merchant-of-Record, pass-through — each tenant/app connects its own
  account) + a default-off crypto provider, a `PaymentService` facade, a store-agnostic `BillingWebhookProcessor`,
  the operational `IBillingStore`, the shared billing DTOs, an `AddPayments(...)` DI extension, and
  **`LocalDbBillingStore`** (app-local, single-tenant, over Core's `IDbConnectionFactory`). Free-mode apps
  consume it for self-contained (Model B) billing. Cohort-aligned at `1.2.0`.

- **FluentUI v5 migration** (`feature/fluentui-v5`) — pinned to the v5 RC (`5.0.0-rc.4-26180.1`); awaiting
  GA before merge to `main`. Not yet released.
- **No CI/CD** — `.github/workflows/*` were removed (GitHub Actions billing). The process is plain
  `git pull` / `git push` to `main`; verify locally before every push (`dotnet build` 0 warnings ·
  `./run-tests.sh` · `audit lint`).

## [Tools 1.2.2] — 2026-07-08

### Added
- **`audit lint` gains `AUD013` — the inline-style gate.** A raw inline `style="…"` attribute in a
  `.razor`/`.sbn` file now fails the audit (use scoped `.razor.css` classes instead — the framework's CSS
  isolation convention). `audit lint` now scans `.razor` + `.sbn` as well as `.cs`; each rule declares its
  file types. **Exempt:** the CSS-variable pattern `style="--x:@value"` (the supported way to pass a
  per-item dynamic value into scoped CSS) and a FluentUI `Style="…"` component parameter (capital `S`).

### Changed
- Framework auth components (`AccessDenied`, `ForgotPasswordDialog`, `ResetPasswordComponent`,
  `SessionWarningDialog`) moved their inline styles to scoped `.razor.css` — no visual change. The unused
  `Style` passthrough parameter on `AsdamirContentCard` was removed (use `AdditionalClass` + scoped CSS).

## [1.2.0] — 2026-07-08  ·  Core / Data / Web

### Added
- **`IPaymentProvider` + `PaymentProviderOptions` (`Asdamir.Core`)** — a new open-core abstraction for
  payment rails: `CreateCustomer` / `CreateCheckoutSession` / `CreateSubscription` / `CancelSubscription`
  / `VerifyWebhook` / `Refund`, all returning `Result<T>` (no exceptions on expected failure), with
  bind-time options. No SDK dependency — implement it to plug in your own rail. (This additive public API
  is the minor-version driver, `1.1.2` → `1.2.0`.)

### Changed
- **Public XML-doc gate is ON for all of open core (`Asdamir.Core` + `Asdamir.Data` + `Asdamir.Web`).**
  Every public member now carries an XML-doc comment; `GenerateDocumentationFile=true` with CS1591 no
  longer suppressed, so a new undocumented public member fails the build. Full IntelliSense coverage for
  consumers. Docs-only — no behaviour change.

## [1.1.2] — 2026-07-04  ·  Core / Data / Web

- **`Asdamir.Core`** — token-audience support: `IJwtService.IssueTokens` gains optional `tokenUse` /
  `appCode` parameters that emit `token_use` (control-plane vs app) and `app_code` claims, so a
  control-plane endpoint can reject an app-login token at the authorization layer.
- **`Asdamir.Web`** — removed unused code: the uncalled `IRateLimitService.GetLimitInfoAsync` /
  `RateLimitInfo`, and the dead `DatabaseDynamicResourceStore` (the active DB-backed localizer is
  `LocalizationHttpClient`). No behaviour change.
- **`Asdamir.Data`** — no change; republished at 1.1.2 to keep the Core/Data/Web trio version-aligned.
- Still FluentUI **v4**-based — the v5 migration stays on its branch until GA.

## [Tools 1.2.1] — 2026-07-07

- **`Asdamir.Tools` 1.2.1 — free-mode auth hardening + first-login password change** (PATCH;
  backward-compatible — only the generated **free-mode** output changes, a `commercial` app is unchanged).
  - **Constant-time login (no user-enumeration):** a free app's login pays the same password-hash verify
    cost on an unknown email as on a known one, so response timing no longer reveals whether an account
    exists.
  - **Seeded config now actually applies:** a free app synchronously loads its own `AppConfigurations` at
    startup, so seeded `RateLimiting:*` / `Security:*` / OTP settings reach the rate-limiter and options
    instead of silently falling back to code defaults.
  - **Fail-closed at-rest key:** a free app now throws at startup if `Security:EncryptionKey` is missing
    (like `Jwt:Key`) — no demo-key fallback; the quick-start banner adds the key step.
  - **First-login forced password change:** a free app ships a `ForcePasswordChange` flag on the starter
    admin; login reports it, the UI redirects to a new change-password page, and the endpoint verifies the
    current password, sets the new one, clears the flag, and revokes existing sessions.
  - **Cleaner quick-start:** `new app` no longer prints a plaintext database password — it uses the
    passwordless `db apply` (which resolves the connection from the Gateway user-secret).

## [Tools 1.2.0] — 2026-07-06

- **`Asdamir.Tools` 1.2.0 — a new self-contained FREE app mode** (MINOR; backward-compatible, the default
  is still `commercial`). `asdamir new app <Name> --mode free|commercial` (default `commercial`) picks where
  a generated app's identity/RBAC/menu/localization/config live:
  - **free** = the app is self-contained with **no control plane**. The management tables + `AppId`-free,
    single-tenant stored procs are emitted into the app's **own** database (schema + procs + a seed for the
    starter admin, Admin role, permissions, Dashboard menu, config and localization), and the Gateway
    **issues + validates its own JWTs** (Asdamir.Core `JwtService`) and serves auth/menu/localization/
    client-settings locally. Login gate is "user exists + active"; logging is file + console.
  - **commercial** (default) = unchanged: identity/menus/permissions/localization/config live centrally in
    `AsdamirVault`, managed from AppManagement; the Gateway proxies to it.
- **`db apply` passwordless connection fallback** — when no connection flags are given, it resolves
  `ConnectionStrings:Default` from the Gateway user-secret (via the `*.Gateway` project's `UserSecretsId`),
  then the `ConnectionStrings__Default` env var. So `asdamir db apply --create-database` works with no SQL
  password on the command line. Explicit flags still take precedence.
- **`new feature` / `new page`** in a free app emit the menu/permission + localization seeds as
  `V*__freemode_{menu,localize}_<plural>.sql` migrations into the app's own `db/migrations` (no
  `--vault-connection`). **`rollback`** in a free app tears those down symmetrically over the app
  connection (menu/permission/grants + localization + seed-journal + the seed migration files).
- **Scaffolding polish:** the onboarding banner is mode-branched (free wording; zero `AsdamirVault`/
  `AppManagement` references), `register_<app>.sql` is emitted for commercial only, generated apps pin
  `Asdamir.Core/Data/Web = 1.1.2` (published — was a `0.1.0-preview.*` float that broke restore), and the
  sample-seed rows are English (`Sample <Field> N`). Core/Data/Web unchanged at 1.1.2.

## [Tools 1.1.4] — 2026-07-04

- **`Asdamir.Tools` 1.1.4** — generated-template fixes: pin **`Microsoft.OpenApi 2.7.5`** in the generated
  `Directory.Packages` (closes NU1903/GHSA-v5pm so a scaffolded app builds under `TreatWarningsAsErrors`),
  templater **fail-fast** on an unknown member / unsupported parenthesis (was silently emitting empty/wrong
  fragments), and the **stale-TRX + skip-as-fail** fix in the generated `run-tests.sh`. Core/Data/Web
  unchanged at 1.1.1.

## [Tools 1.1.3] — 2026-06-30

- **`Asdamir.Tools` 1.1.3** — core workflow automation: `asdamir new feature <Name>` (entity + page +
  menu/permission/localization seeds in one command) and `asdamir rollback <Name>` (undo a generated
  feature across code + app-DB table + AsdamirVault menu/permissions). Two-DB apply is opt-in
  (`--apply` for the app DB, `--vault-connection` for AsdamirVault).

## [Tools 1.1.2] — 2026-06-28

- **`Asdamir.Tools` 1.1.2** — every generated app now ships an executable `run-tests.sh` (clean
  PASS/FAIL per test via TRX parse, same as the framework's).

## [1.1.1] — 2026-06-27  ·  Core / Data / Web

- **`Asdamir.Web`** — rate-limiter fix (fixed-window counter correctness).
- **`Asdamir.Tools`** generator changes rolled into the open-core release.

## [Tools 1.1.0] — 2026-06-26

- **`Asdamir.Tools` 1.1.0** — richer generated test suite: `asdamir new entity` now emits an update
  round-trip test, a list test (both service-level against an in-memory fake repo) and an **API
  auth-guard test** (`WebApplicationFactory`, token-less `GET` → 401), in addition to the existing
  create/get/delete/validator tests — **6 tests per entity, all DB-free**. `ScaffoldSmokeTests` now runs
  `dotnet test` on the generated solution, so a broken generated test fails the build. (Core/Data/Web
  unchanged at 1.0.4 at the time.)

## [1.0.4] — 2026-06-23  ·  Core / Data / Web / Tools

- Auto-DI by convention in generated Gateways (reflection scan registers `I<Name>Repository`/
  `I<Name>Service` — no per-entity `AddScoped`), DB-backed UI localization end-to-end, the topbar
  (theme / dark-mode / language) in generated Server hosts, and nav-menu label localization via
  `Menu.<Slug>` keys. Scaffold smoke test cut from ~16 min to ~10 s.
- Rolls up the intermediate `1.0.1`–`1.0.3` open-core bumps:
  - **1.0.1** (2026-06-22) — `Asdamir.Web.Http.ToUserMessageAsync` (the shared user-facing-error helper)
    + Core error-key fallback chain.
  - **1.0.2 / 1.0.3** (2026-06-23) — `Asdamir.Tools` scaffold template/command fixes, generated-app
    UI-auth + topbar wiring.

## [1.0.0] — 2026-06-20  ·  initial nuget.org publish

First public open-core release: `Asdamir.Core`, `Asdamir.Data`, `Asdamir.Web`, `Asdamir.Tools`
(LGPL-3.0). The CLI command was renamed `framework` → `asdamir` to match the brand/package/docs.

### Security

- **`CryptographyService`** — password hashing on **PBKDF2-SHA256, 210 000 iterations**, with a **16-byte
  per-record random salt**. Format: `$pbkdf2-sha256$210000$<salt>$<hash>`. `NeedsRehash` flags old-format
  hashes so callers can lazy-upgrade on login.
- **`EncryptionService`** — every encrypt uses a fresh 16-byte random IV (prepended to the ciphertext).
  A deterministic IV would leak plaintext equality.
- **`JwtService`** — enforces a minimum 64-byte signing key, reads access/refresh lifetimes from config,
  generates refresh tokens via `RandomNumberGenerator.GetBytes(32)`. The caller hashes (SHA-256) before
  DB insert.
- **`RouteAuthorizationMiddleware`** — exception path is **fail-closed** (`_isAuthorized = false` +
  redirect to `/access-denied` + Error log), never falling through to `true` on a catch.
- **`AppAuthStateProvider`** — does not carry the raw JWT in a claim; token access goes through
  `ITokenStore`.
- **`BearerHandler`** — 401 retry: on 401, call `IAuthorizationTokenService.TryRefreshTokenAsync` and
  replay the request once on success.
- **`AuthorizationRateLimiter`** — `IMemoryCache` + `SizeLimit` + thread-safety (bounded, no unbounded
  dictionary growth).
- **`AuthorizationCache`** — key includes `tenantId`, strips query strings, uses `IMemoryCache` +
  `SizeLimit`.
- **`AuthenticationBarrier`** — `TaskCompletionSource<bool>(RunContinuationsAsynchronously)` replaces a
  `SemaphoreSlim(0,1)` race.

### Reliability

- **`Web.Localization` (`SimpleStringLocalizer` + `LocalizationHttpClient`)** — cache-pin fix (`Lazy<T>`
  + force-refresh on lookup miss) and a background cache refresher.
- **`GlobalExceptionMiddleware`** — exception classification by type hierarchy (`DbException`, custom
  `EmailDeliveryException`) instead of locale-dependent `Message.Contains("database")`.
- **`HeaderTenantResolver`** — **advisory only**; tenant authority is the `tid` claim. The header survives
  for dev/test tooling.
- **`BaseValidator`** — `ApplyContextualRules` no longer appends rules on every call.
- **`WhiteListService`** — async-only API; sync-over-async overloads removed.
- **`RestErrorTranslator`** — uses `IHttpClientFactory`, not `new HttpClient()`.
- **`ModuleLoader`** — takes `IServiceCollection` parametrically; silent failures now log + throw.

### Added

- **`Asdamir.Web`** — shared FluentUI components (single version across the board).
- **`Asdamir.Data.HangfireJobs`** — `[DisableConcurrentExecution]`-wrapped base job + DI helpers.
- **`Asdamir.Data.Outbox`** — dispatcher background service + email/sms abstractions.
- **`Asdamir.Core.Modules`** — pluggable module loader for managed apps.

## AppManagement (control plane)

Asdamir also ships a commercial control plane — **AppManagement** — a Blazor admin console + REST API that
registers, configures and operates the apps built on the framework: central identity, roles, permissions,
menus, localization, configuration and logs, held once (scoped per app) and administered from one console.
It is not part of the open-core packages and is versioned and released separately.

## Naming

Package prefix is **`Asdamir.*`** throughout (NuGet ID, namespace) — `Asdamir.Core`, `Asdamir.Data`,
`Asdamir.Web`, `Asdamir.Tools`. The GitHub repo folder is kept as `entframework` on purpose; the
product/brand is **Asdamir**. No source, comment, or test references any brand name from a legacy upstream
integration, and the legacy `Ent*` / `Framework.*` names are fully retired.
