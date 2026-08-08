// Copyright (C) 2026 Orhan Özşahin — Asdamir.
// Licensed under the GNU Lesser General Public License v3.0. See LICENSE.
// SPDX-License-Identifier: LGPL-3.0-or-later
//
// This file is part of the Asdamir open core. It is free software: you can redistribute it
// and/or modify it under the terms of the GNU Lesser General Public License as published by the
// Free Software Foundation, either version 3 of the License, or (at your option) any later
// version. It is distributed WITHOUT ANY WARRANTY; without even the implied warranty of
// MERCHANTABILITY or FITNESS FOR A PARTICULAR PURPOSE. See the GNU LGPL for more details.

using System.IO.Compression;
using System.Security.Cryptography;
using System.Text;
using System.Text.Json;

namespace Asdamir.Core.AgentAudit;

/// <summary>
/// Verifies an agent action ledger archive — <b>Archive Format v1</b> — offline: no network, no database, no
/// AppManagement, no licence.
///
/// <para><b>Why this is open core.</b> When a closed range of the ledger is folded, the rows leave the live
/// database and survive only as an archive. If the only thing able to check that archive were the closed
/// component that produced it, the assurance would collapse to "trust the vendor" — a system's own clean report
/// about itself is not audit evidence. The normative specification
/// (<c>docs/fundamentals/agent-audit-archive-format-v1.md</c>) exists so anyone can write their own verifier;
/// this class is the one we ship, and it is deliberately not privileged over one you write yourself.</para>
///
/// <para><b>It re-derives; it never re-hashes what it was handed.</b> Each row carries the canonical prefix that
/// was hashed when it was written, and this verifier rebuilds that prefix FROM THE ROW'S OWN COLUMNS and
/// compares. Hashing the stored prefix would be circular: an attacker who edits a projection column and leaves
/// the prefix and hash alone would pass. That circularity is exactly why the ledger's in-database check is
/// partial by construction, and the archive layer must not import it.</para>
///
/// <para><b>Two claims, never blurred.</b> See <see cref="ArchiveVerificationResult.ProvesNotAltered"/> and
/// <see cref="ArchiveVerificationResult.ProvesAnchored"/>. Without an expected digest the result is
/// <see cref="ArchiveVerificationStatus.InternallyConsistent"/> — never
/// <see cref="ArchiveVerificationStatus.Verified"/>.</para>
///
/// <para><b>A format error is not a verdict.</b> An archive whose shape or version was not understood was never
/// checked, so this class returns a result whose <see cref="ArchiveVerificationResult.Status"/> is
/// <c>null</c> rather than inventing a fifth status: see <see cref="ArchiveVerificationResult"/>.</para>
/// </summary>
public sealed class AgentLedgerArchiveVerifier
{
    /// <summary>The archive format versions this verifier implements. v1 is frozen; a future revision is v2.</summary>
    public static IReadOnlyCollection<int> SupportedFormatVersions { get; } =
        new[] { AgentLedgerArchiveManifest.FormatVersion1 };

    /// <summary>
    /// A ready-made verifier that implements canonicalization v1 only — what almost every caller wants.
    /// </summary>
    public static AgentLedgerArchiveVerifier Default { get; } = new();

    private static readonly JsonSerializerOptions ReadOptions = new() { PropertyNamingPolicy = null };

    /// <summary>Members of <c>manifest.json</c> whose absence or null is a format error (all twelve).</summary>
    private static readonly string[] ManifestMembers =
    [
        "FormatVersion", "HashVersion", "AppId", "TenantId", "SeqStart", "SeqEnd",
        "RowCount", "SegmentDigest", "PrevHashAtStart", "RowHashAtEnd", "ExportedAtUtc", "ProducerVersion",
    ];

    /// <summary>
    /// Members of a segment line whose absence is a format error. The nullable fields are deliberately absent
    /// from this list: for them a null member and an absent member are equivalent.
    /// </summary>
    private static readonly string[] RowRequiredMembers =
    [
        "SeqNo", "RecordedAtTicks", "HashVersion", "AppId", "TenantId", "RecordKind", "EventId",
        "OccurredAtUtc", "AgentId", "AuthorityKind", "ActionType", "Decision", "Outcome",
        "PrevHash", "RowHash", "CanonicalPrefix",
    ];

    private const int FoldTombstoneKind = 2;

    private readonly Dictionary<int, IAgentActionCanonicalizer> _canonicalizers;

    /// <summary>Builds a verifier that implements canonicalization v1 — the only version in existence.</summary>
    public AgentLedgerArchiveVerifier()
        : this([AgentActionCanonicalizer.V1])
    {
    }

    /// <summary>
    /// Builds a verifier over an explicit set of canonicalization implementations, keyed by
    /// <see cref="IAgentActionCanonicalizer.HashVersion"/>.
    ///
    /// <para>This is what makes version dispatch real rather than assumed, and it is the reason the two halves
    /// of the mixed-version rule can be told apart at all: a row whose version this verifier IMPLEMENTS but
    /// which contradicts the manifest is a content failure (<see cref="ArchiveVerificationStatus.Broken"/> — the
    /// manifest made a claim about the rows and a row disagrees), while a row whose version it does NOT
    /// implement is a format error, because it cannot rebuild that row's canonical body at all and must not
    /// report a result claiming it checked.</para>
    /// </summary>
    /// <param name="canonicalizers">One implementation per hash version. Must be non-empty and must not contain
    /// two implementations of the same version.</param>
    /// <exception cref="ArgumentException">The set is empty or declares a version twice.</exception>
    public AgentLedgerArchiveVerifier(IEnumerable<IAgentActionCanonicalizer> canonicalizers)
    {
        ArgumentNullException.ThrowIfNull(canonicalizers);

        _canonicalizers = new Dictionary<int, IAgentActionCanonicalizer>();
        foreach (var canonicalizer in canonicalizers)
        {
            ArgumentNullException.ThrowIfNull(canonicalizer);
            if (!_canonicalizers.TryAdd(canonicalizer.HashVersion, canonicalizer))
            {
                throw new ArgumentException(
                    $"Two canonicalizers declare HashVersion {canonicalizer.HashVersion}; a version has exactly "
                    + "one byte layout, so a second implementation of it is a bug, not a choice.",
                    nameof(canonicalizers));
            }
        }

        if (_canonicalizers.Count == 0)
        {
            throw new ArgumentException(
                "A verifier with no canonicalizer could never re-derive a row, so every archive would be "
                + "refused for a reason that is about the verifier rather than the archive.",
                nameof(canonicalizers));
        }
    }

    /// <summary>The canonicalization versions this verifier can re-derive, ascending.</summary>
    public IReadOnlyCollection<int> SupportedHashVersions => _canonicalizers.Keys.Order().ToArray();

    /// <summary>
    /// Verifies a ZIP archive held in memory.
    /// </summary>
    /// <param name="archiveZip">The container's bytes — exactly what the export produced.</param>
    /// <param name="expectedSegmentDigestHex">The anchor: the <c>FoldSegmentDigest</c> recorded on the tombstone
    /// in the live ledger. <b>Omit it and the result can never be <see cref="ArchiveVerificationStatus.Verified"/></b>
    /// — the digest inside the manifest is derived from the very rows it accompanies and proves nothing on its own.</param>
    /// <returns>The verdict, or a format error (see <see cref="ArchiveVerificationResult"/>).</returns>
    public ArchiveVerificationResult Verify(byte[] archiveZip, string? expectedSegmentDigestHex = null)
    {
        ArgumentNullException.ThrowIfNull(archiveZip);
        using var stream = new MemoryStream(archiveZip, writable: false);
        return Verify(stream, expectedSegmentDigestHex);
    }

    /// <summary>
    /// Verifies a ZIP archive from a stream. A non-seekable stream is buffered first, because a ZIP's central
    /// directory lives at the end of the file.
    /// </summary>
    /// <param name="archiveZip">The container.</param>
    /// <param name="expectedSegmentDigestHex">The anchor; see the other overload.</param>
    /// <returns>The verdict, or a format error.</returns>
    public ArchiveVerificationResult Verify(Stream archiveZip, string? expectedSegmentDigestHex = null)
    {
        ArgumentNullException.ThrowIfNull(archiveZip);

        Stream seekable = archiveZip;
        MemoryStream? buffered = null;

        if (!archiveZip.CanSeek)
        {
            buffered = new MemoryStream();
            archiveZip.CopyTo(buffered);
            buffered.Position = 0;
            seekable = buffered;
        }

        try
        {
            string manifestJson;
            string segmentNdjson;

            try
            {
                using var zip = new ZipArchive(seekable, ZipArchiveMode.Read, leaveOpen: true);

                // Entries beyond the two specified ones are IGNORED, not rejected: v1 reserves them so a future
                // revision is cheap, and nothing may be derived from them.
                var manifestEntry = zip.GetEntry(AgentLedgerArchiveManifest.ManifestEntryName);
                if (manifestEntry is null)
                {
                    return FormatError(
                        "manifest_entry_missing",
                        $"The container has no '{AgentLedgerArchiveManifest.ManifestEntryName}' entry, so it is "
                        + "not an Archive Format v1 archive and NO checks were performed.");
                }

                var segmentEntry = zip.GetEntry(AgentLedgerArchiveManifest.SegmentEntryName);
                if (segmentEntry is null)
                {
                    return FormatError(
                        "segment_entry_missing",
                        $"The container has no '{AgentLedgerArchiveManifest.SegmentEntryName}' entry, so it is "
                        + "not an Archive Format v1 archive and NO checks were performed.");
                }

                manifestJson = ReadEntry(manifestEntry);
                segmentNdjson = ReadEntry(segmentEntry);
            }
            catch (InvalidDataException ex)
            {
                return FormatError(
                    "container_not_readable",
                    "The file could not be read as a ZIP container, so NO checks were performed: " + ex.Message);
            }

            return Verify(manifestJson, segmentNdjson, expectedSegmentDigestHex);
        }
        finally
        {
            buffered?.Dispose();
        }
    }

    /// <summary>
    /// Verifies an archive that has already been unpacked — the two entries as text. Used for the directory
    /// form of an archive, and by tests that build a segment by hand.
    /// </summary>
    /// <param name="manifestJson">The contents of <c>manifest.json</c>.</param>
    /// <param name="segmentNdjson">The contents of <c>segment.ndjson</c>.</param>
    /// <param name="expectedSegmentDigestHex">The anchor; see <see cref="Verify(byte[], string?)"/>.</param>
    /// <returns>The verdict, or a format error.</returns>
    /// <exception cref="ArgumentException"><paramref name="expectedSegmentDigestHex"/> is not 64 hex characters.
    /// A malformed anchor is the CALLER's mistake, not a finding about the archive, and reporting it as
    /// <see cref="ArchiveVerificationStatus.DigestMismatch"/> would blame the wrong party.</exception>
    public ArchiveVerificationResult Verify(
        string manifestJson, string segmentNdjson, string? expectedSegmentDigestHex = null)
    {
        ArgumentNullException.ThrowIfNull(manifestJson);
        ArgumentNullException.ThrowIfNull(segmentNdjson);

        byte[]? expectedDigest = null;
        if (expectedSegmentDigestHex is not null)
        {
            if (!TryHash(expectedSegmentDigestHex, out var parsedExpected))
            {
                throw new ArgumentException(
                    "The expected segment digest must be 64 hex characters (a 32-byte SHA-256), in either case.",
                    nameof(expectedSegmentDigestHex));
            }

            expectedDigest = parsedExpected;
        }

        // ---- Steps 1-3: the manifest ------------------------------------------------------------------
        JsonDocument manifestDoc;
        try
        {
            manifestDoc = JsonDocument.Parse(manifestJson);
        }
        catch (JsonException ex)
        {
            return FormatError("manifest_not_json", "manifest.json is not valid JSON: " + ex.Message);
        }

        using (manifestDoc)
        {
            var root = manifestDoc.RootElement;
            if (root.ValueKind != JsonValueKind.Object)
                return FormatError("manifest_not_json", "manifest.json must be a single JSON object.");

            // FormatVersion and HashVersion are checked FIRST and separately (§2): an unknown layout means the
            // checks were not performed, and a best-effort parse would present an unchecked archive as checked.
            if (Missing(root, "FormatVersion", out var formatVersionElement))
                return ManifestMemberMissing("FormatVersion");

            if (formatVersionElement.ValueKind != JsonValueKind.Number
                || !formatVersionElement.TryGetInt32(out var formatVersion))
                return ManifestMemberMalformed("FormatVersion", "an integer");

            if (!SupportedFormatVersions.Contains(formatVersion))
            {
                return FormatError(
                    "unsupported_format_version",
                    $"FormatVersion {formatVersion} is not implemented by this verifier (it implements "
                    + $"{string.Join(", ", SupportedFormatVersions)}). NO checks were performed — an unknown "
                    + "layout means a best-effort parse would present an unchecked archive as checked.");
            }

            if (Missing(root, "HashVersion", out var hashVersionElement))
                return ManifestMemberMissing("HashVersion");

            if (hashVersionElement.ValueKind != JsonValueKind.Number
                || !hashVersionElement.TryGetInt32(out var manifestHashVersion))
                return ManifestMemberMalformed("HashVersion", "an integer");

            if (!_canonicalizers.TryGetValue(manifestHashVersion, out var manifestCanonicalizer))
            {
                return FormatError(
                    "unsupported_hash_version",
                    $"HashVersion {manifestHashVersion} is not implemented by this verifier (it implements "
                    + $"{string.Join(", ", SupportedHashVersions)}). HashVersion is field 1 of the canonical "
                    + "body, so guessing changes every hash. NO checks were performed.");
            }

            foreach (var member in ManifestMembers)
            {
                if (Missing(root, member, out _)) return ManifestMemberMissing(member);
            }

            AgentLedgerArchiveManifest manifest;
            try
            {
                manifest = JsonSerializer.Deserialize<AgentLedgerArchiveManifest>(manifestJson, ReadOptions)!;
            }
            catch (JsonException ex)
            {
                return FormatError(
                    "manifest_member_malformed",
                    "manifest.json has a member of the wrong type, so it is not an Archive Format v1 manifest "
                    + "and NO checks were performed: " + ex.Message);
            }

            // A manifest that contradicts ITSELF is a shape failure, not a finding about the rows: nothing has
            // been compared yet. (A line count that contradicts RowCount is the opposite case — see step 4d.)
            if (manifest.RowCount != manifest.SeqEnd - manifest.SeqStart + 1)
            {
                return FormatError(
                    "manifest_range_inconsistent",
                    $"The manifest contradicts itself: RowCount {manifest.RowCount} but SeqEnd - SeqStart + 1 = "
                    + $"{manifest.SeqEnd - manifest.SeqStart + 1}. NO checks were performed.");
            }

            if (!TryHash(manifest.SegmentDigest, out var manifestDigest))
                return ManifestMemberMalformed("SegmentDigest", "64 hex characters");
            if (!TryHash(manifest.PrevHashAtStart, out var manifestPrevHashAtStart))
                return ManifestMemberMalformed("PrevHashAtStart", "64 hex characters");
            if (!TryHash(manifest.RowHashAtEnd, out var manifestRowHashAtEnd))
                return ManifestMemberMalformed("RowHashAtEnd", "64 hex characters");

            return VerifySegment(
                manifest, manifestHashVersion, manifestCanonicalizer, manifestDigest, manifestPrevHashAtStart,
                manifestRowHashAtEnd, segmentNdjson, expectedDigest, expectedSegmentDigestHex);
        }
    }

    /// <summary>
    /// Re-derives ONE row and reports the first divergence — the authoritative canonical check, exposed because
    /// the live-ledger verification needs exactly this and a second copy of it would be free to drift from the
    /// archive's.
    ///
    /// <para>Returns <see cref="ArchiveVerificationStatus.InternallyConsistent"/> when the row re-derives: a
    /// single row can attest that its own bytes are unaltered and nothing more, so
    /// <see cref="ArchiveVerificationStatus.Verified"/> is not reachable here at all.</para>
    /// </summary>
    /// <param name="row">The row to check. A fold tombstone, or any row with no stored prefix, is NOT
    /// re-derivable — its row hash is carried over rather than derived — and comes back as a format error
    /// rather than as a failure, because nothing was checked.</param>
    /// <returns>The row's result.</returns>
    public ArchiveVerificationResult VerifyRow(AgentLedgerArchiveRow row)
    {
        ArgumentNullException.ThrowIfNull(row);

        if (!_canonicalizers.TryGetValue(row.HashVersion, out var canonicalizer))
        {
            return FormatError(
                "unsupported_hash_version",
                $"Row {row.SeqNo} declares HashVersion {row.HashVersion}, which this verifier does not "
                + "implement, so its canonical body could not be rebuilt and NOTHING was checked.",
                row.SeqNo);
        }

        if (row.RecordKind == FoldTombstoneKind || row.CanonicalPrefix is null)
        {
            return FormatError(
                "row_not_re_derivable",
                $"Row {row.SeqNo} carries no canonical prefix (RecordKind {row.RecordKind}). A fold tombstone's "
                + "RowHash is CARRIED OVER from the folded range's last row rather than derived from its own "
                + "content, so re-deriving it would report a mismatch that is not a finding. NOTHING was checked.",
                row.SeqNo);
        }

        var malformed = MalformedRowMember(row);
        if (malformed is not null)
        {
            return FormatError(
                "row_member_malformed",
                $"Row {row.SeqNo} has a malformed member ({malformed}), so it is not an Archive Format v1 row "
                + "and NOTHING was checked.",
                row.SeqNo);
        }

        return CheckRow(row, canonicalizer, previousRowHash: null, rowsChecked: 0)
               ?? new ArchiveVerificationResult
               {
                   Status = ArchiveVerificationStatus.InternallyConsistent,
                   Code = "internally_consistent",
                   Message =
                       $"Row {row.SeqNo} re-derives from its own columns and its RowHash agrees. This says the "
                       + "row has not been altered since it was written; it says nothing about the rest of the "
                       + "chain and nothing about anchoring.",
                   SeqNo = row.SeqNo,
                   RowsChecked = 1,
               };
    }

    // =================================================================================================
    // Steps 4-8
    // =================================================================================================

    private ArchiveVerificationResult VerifySegment(
        AgentLedgerArchiveManifest manifest, int manifestHashVersion,
        IAgentActionCanonicalizer manifestCanonicalizer, byte[] manifestDigest, byte[] manifestPrevHashAtStart,
        byte[] manifestRowHashAtEnd, string segmentNdjson, byte[]? expectedDigest, string? expectedDigestHex)
    {
        var lines = SplitLines(segmentNdjson);

        for (var i = 0; i < lines.Count; i++)
        {
            if (lines[i].Length == 0 || string.IsNullOrWhiteSpace(lines[i]))
            {
                return FormatError(
                    "segment_line_empty",
                    $"segment.ndjson line {i + 1} is blank. Every line must be one JSON object; NO checks were "
                    + "performed.",
                    manifest: manifest);
            }
        }

        var documents = new List<JsonDocument>(lines.Count);

        try
        {
            for (var i = 0; i < lines.Count; i++)
            {
                try
                {
                    documents.Add(JsonDocument.Parse(lines[i]));
                }
                catch (JsonException ex)
                {
                    return FormatError(
                        "segment_line_not_json",
                        $"segment.ndjson line {i + 1} is not valid JSON, so NO checks were performed: {ex.Message}",
                        manifest: manifest);
                }

                if (documents[i].RootElement.ValueKind != JsonValueKind.Object)
                {
                    return FormatError(
                        "segment_line_not_json",
                        $"segment.ndjson line {i + 1} is not a JSON object. NO checks were performed.",
                        manifest: manifest);
                }
            }

            // ---- 4a: the tombstone check runs BEFORE required members, and the order is NORMATIVE ------
            // A tombstone legitimately has no CanonicalPrefix, so checking required members first would
            // reject a tombstone archive for a MISSING FIELD rather than for CONTAINING A TOMBSTONE — the
            // same file, two different explanations, which is the divergence the specification exists to stop.
            for (var i = 0; i < documents.Count; i++)
            {
                var element = documents[i].RootElement;
                if (element.TryGetProperty("RecordKind", out var kind)
                    && kind.ValueKind == JsonValueKind.Number
                    && kind.TryGetInt32(out var recordKind)
                    && recordKind == FoldTombstoneKind)
                {
                    return FormatError(
                        "segment_contains_tombstone",
                        $"segment.ndjson line {i + 1} (SeqNo {SeqNoText(element, i)}) is a fold tombstone "
                        + "(RecordKind 2). A v1 segment must not contain one — the live ledger refuses to "
                        + "re-fold a range that already holds a tombstone, so an archive carrying one was not "
                        + "produced by the fold flow. NO checks were performed.",
                        SeqNoOf(element), manifest);
                }
            }

            // ---- 4b: one HashVersion per archive -------------------------------------------------------
            for (var i = 0; i < documents.Count; i++)
            {
                var element = documents[i].RootElement;
                if (!element.TryGetProperty("HashVersion", out var hv)
                    || hv.ValueKind != JsonValueKind.Number
                    || !hv.TryGetInt32(out var rowHashVersion)
                    || rowHashVersion == manifestHashVersion)
                {
                    continue;   // absent/malformed is caught by 4c, which owns that finding
                }

                var seqNo = SeqNoOf(element);
                var seqNoText = SeqNoText(element, i);

                // The bucket depends on WHY the verifier cannot proceed, and the two halves are genuinely
                // different findings. A version we IMPLEMENT contradicts a claim the manifest made about the
                // rows — a detectable content failure, exactly like a line count that disagrees with RowCount.
                // A version we do NOT implement means the row's canonical body cannot be rebuilt at all, so
                // reporting any verdict would claim a check that never happened.
                if (_canonicalizers.ContainsKey(rowHashVersion))
                {
                    return Broken(
                        "row_hash_version_contradicts_manifest",
                        $"Row {seqNoText} declares HashVersion {rowHashVersion} but the manifest says "
                        + $"{manifestHashVersion}. One HashVersion per archive: no manifest can honestly "
                        + "describe a mixed segment.",
                        seqNo, manifest, rowsChecked: 0);
                }

                return FormatError(
                    "unsupported_hash_version",
                    $"Row {seqNoText} declares HashVersion {rowHashVersion}, which this verifier does not "
                    + $"implement (it implements {string.Join(", ", SupportedHashVersions)}), and the manifest "
                    + $"says {manifestHashVersion}. That row's canonical body cannot be rebuilt at all, so NO "
                    + "checks were performed.",
                    seqNo, manifest);
            }

            // ---- 4c: every required member of every row ------------------------------------------------
            for (var i = 0; i < documents.Count; i++)
            {
                var element = documents[i].RootElement;
                foreach (var member in RowRequiredMembers)
                {
                    if (!Missing(element, member, out _)) continue;

                    return FormatError(
                        "row_member_missing",
                        $"segment.ndjson line {i + 1} (SeqNo {SeqNoText(element, i)}) is missing the required "
                        + $"member '{member}'. NO checks were performed.",
                        SeqNoOf(element), manifest);
                }
            }

            var rows = new List<AgentLedgerArchiveRow>(documents.Count);
            for (var i = 0; i < lines.Count; i++)
            {
                AgentLedgerArchiveRow row;
                try
                {
                    row = JsonSerializer.Deserialize<AgentLedgerArchiveRow>(lines[i], ReadOptions)!;
                }
                catch (JsonException ex)
                {
                    return FormatError(
                        "row_member_malformed",
                        $"segment.ndjson line {i + 1} has a member of the wrong type, so it is not an Archive "
                        + $"Format v1 row and NO checks were performed: {ex.Message}",
                        SeqNoOf(documents[i].RootElement), manifest);
                }

                var malformed = MalformedRowMember(row);
                if (malformed is not null)
                {
                    return FormatError(
                        "row_member_malformed",
                        $"segment.ndjson line {i + 1} (SeqNo {row.SeqNo}) has a malformed member ({malformed}). "
                        + "NO checks were performed.",
                        row.SeqNo, manifest);
                }

                rows.Add(row);
            }

            // ---- 4d: from here on a failure is BROKEN, not a format error -------------------------------
            // The archive parsed as v1 and then failed a check: that is a finding about its CONTENT. A deleted
            // or added row is precisely tampering, and the manifest is well-formed — which is why this is
            // Broken while a MISSING RowCount member (above) is a format error. Same field, different bucket.
            if (rows.Count != manifest.RowCount)
            {
                return Broken(
                    "row_count_mismatch",
                    $"segment.ndjson holds {rows.Count} lines but the manifest says RowCount "
                    + $"{manifest.RowCount}. A row was added or removed.",
                    seqNo: null, manifest, rowsChecked: 0);
            }

            for (var i = 0; i < rows.Count; i++)
            {
                var expectedSeqNo = manifest.SeqStart + i;
                if (rows[i].SeqNo == expectedSeqNo) continue;

                return Broken(
                    "seq_no_not_dense",
                    $"SeqNo must run densely and strictly ascending from {manifest.SeqStart} to "
                    + $"{manifest.SeqEnd}: line {i + 1} carries SeqNo {rows[i].SeqNo} where {expectedSeqNo} was "
                    + "expected. Rows were reordered, removed or inserted.",
                    rows[i].SeqNo, manifest, rowsChecked: 0);
            }

            // ---- 5: per row, in order -------------------------------------------------------------------
            byte[]? previousRowHash = null;
            long rowsChecked = 0;

            foreach (var row in rows)
            {
                var failure = CheckRow(row, manifestCanonicalizer, previousRowHash, rowsChecked);
                if (failure is not null) return failure with { Manifest = manifest };

                previousRowHash = Convert.FromHexString(row.RowHash);
                rowsChecked++;
            }

            // ---- 6: the boundaries the tombstone also carries --------------------------------------------
            if (!Convert.FromHexString(rows[0].PrevHash).AsSpan().SequenceEqual(manifestPrevHashAtStart))
            {
                return Broken(
                    "prev_hash_at_start_mismatch",
                    $"The first row's PrevHash does not match the manifest's PrevHashAtStart, so this segment "
                    + "does not attach where the manifest says it does.",
                    rows[0].SeqNo, manifest, rowsChecked,
                    expectedHex: manifest.PrevHashAtStart.ToLowerInvariant(),
                    actualHex: rows[0].PrevHash.ToLowerInvariant());
            }

            if (!Convert.FromHexString(rows[^1].RowHash).AsSpan().SequenceEqual(manifestRowHashAtEnd))
            {
                return Broken(
                    "row_hash_at_end_mismatch",
                    "The last row's RowHash does not match the manifest's RowHashAtEnd, so the row that followed "
                    + "this segment would not link back to it.",
                    rows[^1].SeqNo, manifest, rowsChecked,
                    expectedHex: manifest.RowHashAtEnd.ToLowerInvariant(),
                    actualHex: rows[^1].RowHash.ToLowerInvariant());
            }

            // ---- 7: the segment digest the manifest claims -----------------------------------------------
            var computedDigest = ComputeSegmentDigest(rows);
            var computedDigestHex = Convert.ToHexString(computedDigest).ToLowerInvariant();

            if (!computedDigest.AsSpan().SequenceEqual(manifestDigest))
            {
                return Broken(
                    "manifest_digest_mismatch",
                    "The manifest's SegmentDigest does not describe these rows: recomputing it over the row "
                    + "hashes gives a different value.",
                    seqNo: null, manifest, rowsChecked,
                    expectedHex: computedDigestHex, actualHex: manifest.SegmentDigest.ToLowerInvariant());
            }

            // ---- 8: anchoring, the one claim the archive cannot make for itself --------------------------
            if (expectedDigest is null)
            {
                return new ArchiveVerificationResult
                {
                    Status = ArchiveVerificationStatus.InternallyConsistent,
                    Code = "internally_consistent",
                    Message =
                        $"UNANCHORED. All {rowsChecked} rows re-derive from their own columns, the chain links "
                        + "hold and the sequence is dense — so this archive has NOT been altered since it was "
                        + "written. It is NOT proven to be the segment that was folded out of a live ledger: "
                        + "that needs the FoldSegmentDigest recorded on the tombstone, supplied with "
                        + "--expected-digest. The digest inside the manifest is derived from these very rows, "
                        + "so it cannot serve as its own anchor.",
                    RowsChecked = rowsChecked,
                    ComputedSegmentDigest = computedDigestHex,
                    Manifest = manifest,
                };
            }

            if (!computedDigest.AsSpan().SequenceEqual(expectedDigest))
            {
                return new ArchiveVerificationResult
                {
                    Status = ArchiveVerificationStatus.DigestMismatch,
                    Code = "digest_mismatch",
                    Message =
                        "These rows are internally consistent but they are NOT the segment you asked about: the "
                        + $"segment digest is {computedDigestHex}, the expected digest was "
                        + $"{expectedDigestHex!.ToLowerInvariant()}.",
                    RowsChecked = rowsChecked,
                    ComputedSegmentDigest = computedDigestHex,
                    ExpectedSegmentDigest = expectedDigestHex.ToLowerInvariant(),
                    ExpectedHex = expectedDigestHex.ToLowerInvariant(),
                    ActualHex = computedDigestHex,
                    Manifest = manifest,
                };
            }

            return new ArchiveVerificationResult
            {
                Status = ArchiveVerificationStatus.Verified,
                Code = "verified",
                Message =
                    $"All {rowsChecked} rows re-derive from their own columns, the chain links hold, the "
                    + "sequence is dense, AND the segment digest matches the expected digest — so this archive "
                    + "has not been altered since it was written AND it is the segment that was folded out of "
                    + "the ledger holding that digest.",
                RowsChecked = rowsChecked,
                ComputedSegmentDigest = computedDigestHex,
                ExpectedSegmentDigest = expectedDigestHex!.ToLowerInvariant(),
                Manifest = manifest,
            };
        }
        finally
        {
            foreach (var document in documents) document.Dispose();
        }
    }

    /// <summary>
    /// The re-derivation itself: rebuild the prefix from the row's columns, compare it to the STORED prefix,
    /// then hash the REBUILT bytes — never the stored ones. Returns null when the row is sound.
    /// </summary>
    private static ArchiveVerificationResult? CheckRow(
        AgentLedgerArchiveRow row, IAgentActionCanonicalizer canonicalizer, byte[]? previousRowHash,
        long rowsChecked)
    {
        var stored = Convert.FromHexString(row.CanonicalPrefix!);
        var rebuilt = canonicalizer.BuildPrefix(row);

        if (!rebuilt.AsSpan().SequenceEqual(stored))
        {
            return new ArchiveVerificationResult
            {
                Status = ArchiveVerificationStatus.Broken,
                Code = "canonical_prefix_mismatch",
                DivergedIn = "canonical_prefix",
                Message =
                    $"Row {row.SeqNo}: the columns no longer produce the bytes that were hashed — first "
                    + $"difference at byte {FirstDifference(rebuilt, stored)} of "
                    + $"{Math.Max(rebuilt.Length, stored.Length)}. A projection column was edited after the "
                    + "fact; re-hashing the stored prefix would not have seen it.",
                SeqNo = row.SeqNo,
                RowsChecked = rowsChecked,
                ExpectedHex = Convert.ToHexString(rebuilt).ToLowerInvariant(),
                ActualHex = Convert.ToHexString(stored).ToLowerInvariant(),
            };
        }

        var prevHash = Convert.FromHexString(row.PrevHash);
        var expectedHash = canonicalizer.ComputeRowHash(prevHash, rebuilt, row.SeqNo, row.RecordedAtTicks);
        var storedHash = Convert.FromHexString(row.RowHash);

        if (!expectedHash.AsSpan().SequenceEqual(storedHash))
        {
            return new ArchiveVerificationResult
            {
                Status = ArchiveVerificationStatus.Broken,
                Code = "row_hash_mismatch",
                DivergedIn = "row_hash",
                Message =
                    $"Row {row.SeqNo}: RowHash mismatch. SHA-256(PrevHash || prefix || SeqNo || "
                    + "RecordedAtTicks) over this row's own values does not equal the stored RowHash.",
                SeqNo = row.SeqNo,
                RowsChecked = rowsChecked,
                ExpectedHex = Convert.ToHexString(expectedHash).ToLowerInvariant(),
                ActualHex = row.RowHash.ToLowerInvariant(),
            };
        }

        if (previousRowHash is not null && !prevHash.AsSpan().SequenceEqual(previousRowHash))
        {
            return new ArchiveVerificationResult
            {
                Status = ArchiveVerificationStatus.Broken,
                Code = "chain_link_mismatch",
                DivergedIn = "prev_hash",
                Message =
                    $"Row {row.SeqNo}: PrevHash does not equal the previous row's RowHash — the chain is broken "
                    + "here.",
                SeqNo = row.SeqNo,
                RowsChecked = rowsChecked,
                ExpectedHex = Convert.ToHexString(previousRowHash).ToLowerInvariant(),
                ActualHex = row.PrevHash.ToLowerInvariant(),
            };
        }

        return null;
    }

    /// <summary>SHA-256 over the raw 32-byte row hashes, ascending, no separator — exactly 32 x RowCount bytes.</summary>
    private static byte[] ComputeSegmentDigest(IReadOnlyList<AgentLedgerArchiveRow> rows)
    {
        var body = new byte[rows.Count * 32];
        for (var i = 0; i < rows.Count; i++)
            Convert.FromHexString(rows[i].RowHash).CopyTo(body, i * 32);

        return SHA256.HashData(body);
    }

    // =================================================================================================
    // Shape helpers
    // =================================================================================================

    /// <summary>
    /// Names the first malformed member of a row, or null when every member is well-formed. "Malformed" is a
    /// SHAPE failure (a hash that is not 64 hex characters, an enum byte outside 0-255), so it is a format
    /// error — unlike a well-formed value that simply disagrees, which is tampering.
    /// </summary>
    private static string? MalformedRowMember(AgentLedgerArchiveRow row)
    {
        if (!InByteRange(row.HashVersion)) return "HashVersion is outside 0-255";
        if (!InByteRange(row.RecordKind)) return "RecordKind is outside 0-255";
        if (!InByteRange(row.AuthorityKind)) return "AuthorityKind is outside 0-255";
        if (!InByteRange(row.Decision)) return "Decision is outside 0-255";
        if (!InByteRange(row.Outcome)) return "Outcome is outside 0-255";

        if (!TryHash(row.PrevHash, out _)) return "PrevHash is not 64 hex characters";
        if (!TryHash(row.RowHash, out _)) return "RowHash is not 64 hex characters";
        if (row.CanonicalPrefix is not null && !TryHex(row.CanonicalPrefix, out _))
            return "CanonicalPrefix is not hex";
        if (row.InputDigest is not null && !TryHex(row.InputDigest, out _)) return "InputDigest is not hex";
        if (row.OutputDigest is not null && !TryHex(row.OutputDigest, out _)) return "OutputDigest is not hex";
        if (row.ErrorDigest is not null && !TryHex(row.ErrorDigest, out _)) return "ErrorDigest is not hex";

        return null;
    }

    private static bool InByteRange(int value) => value is >= 0 and <= 255;

    /// <summary>
    /// Decodes hex, accepting UPPER, lower or mixed case. The specification requires a producer to emit
    /// lowercase and a verifier to compare the DECODED BYTES — case is a spelling of the same value, and
    /// refusing it would fail an archive that is byte-for-byte sound.
    /// </summary>
    private static bool TryHex(string? text, out byte[] bytes)
    {
        bytes = [];
        if (text is null || text.Length % 2 != 0) return false;

        try
        {
            bytes = Convert.FromHexString(text);
            return true;
        }
        catch (FormatException)
        {
            // Not a finding about integrity — the value is not hex at all, which the caller reports as a
            // malformed member. Swallowing it here keeps the shape check total instead of throwing at the API.
            return false;
        }
    }

    private static bool TryHash(string? text, out byte[] bytes)
        => TryHex(text, out bytes) && bytes.Length == 32;

    private static bool Missing(JsonElement element, string member, out JsonElement value)
        => !element.TryGetProperty(member, out value) || value.ValueKind == JsonValueKind.Null;

    private static string SeqNoText(JsonElement element, int lineIndex)
        => element.TryGetProperty("SeqNo", out var seq) && seq.ValueKind == JsonValueKind.Number
            ? seq.GetRawText()
            : $"unknown, line {lineIndex + 1}";

    private static long? SeqNoOf(JsonElement element)
        => element.TryGetProperty("SeqNo", out var seq) && seq.ValueKind == JsonValueKind.Number
           && seq.TryGetInt64(out var value)
            ? value
            : null;

    private static List<string> SplitLines(string ndjson)
    {
        var lines = ndjson.Split('\n').ToList();

        // A trailing newline after the last line is permitted; a blank line anywhere else is not.
        if (lines.Count > 0 && lines[^1].Length == 0) lines.RemoveAt(lines.Count - 1);

        return lines;
    }

    private static string ReadEntry(ZipArchiveEntry entry)
    {
        using var stream = entry.Open();
        using var reader = new StreamReader(
            stream, new UTF8Encoding(encoderShouldEmitUTF8Identifier: false),
            detectEncodingFromByteOrderMarks: false);
        return reader.ReadToEnd();
    }

    private static int FirstDifference(ReadOnlySpan<byte> left, ReadOnlySpan<byte> right)
    {
        var shared = Math.Min(left.Length, right.Length);
        for (var i = 0; i < shared; i++)
        {
            if (left[i] != right[i]) return i;
        }

        return shared;
    }

    // =================================================================================================
    // Result factories
    // =================================================================================================

    private static ArchiveVerificationResult FormatError(
        string code, string message, long? seqNo = null, AgentLedgerArchiveManifest? manifest = null)
        => new()
        {
            Status = null,      // NOT one of the four verdicts: nothing was checked. See ArchiveVerificationResult.
            Code = code,
            Message = message,
            SeqNo = seqNo,
            Manifest = manifest,
        };

    private static ArchiveVerificationResult ManifestMemberMissing(string member)
        => FormatError(
            "manifest_member_missing",
            $"manifest.json is missing the required member '{member}'. All twelve members are required, so this "
            + "is not an Archive Format v1 manifest and NO checks were performed.");

    private static ArchiveVerificationResult ManifestMemberMalformed(string member, string expected)
        => FormatError(
            "manifest_member_malformed",
            $"manifest.json member '{member}' is not {expected}, so it is not an Archive Format v1 manifest and "
            + "NO checks were performed.");

    private static ArchiveVerificationResult Broken(
        string code, string message, long? seqNo, AgentLedgerArchiveManifest manifest, long rowsChecked,
        string? expectedHex = null, string? actualHex = null)
        => new()
        {
            Status = ArchiveVerificationStatus.Broken,
            Code = code,
            Message = message,
            SeqNo = seqNo,
            RowsChecked = rowsChecked,
            ExpectedHex = expectedHex,
            ActualHex = actualHex,
            Manifest = manifest,
        };
}
