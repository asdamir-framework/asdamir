// Copyright (C) 2026 Orhan Özşahin — Asdamir.
// Licensed under the GNU Lesser General Public License v3.0. See LICENSE.
// SPDX-License-Identifier: LGPL-3.0-or-later
//
// This file is part of the Asdamir open core. It is free software: you can redistribute it
// and/or modify it under the terms of the GNU Lesser General Public License as published by the
// Free Software Foundation, either version 3 of the License, or (at your option) any later
// version. It is distributed WITHOUT ANY WARRANTY; without even the implied warranty of
// MERCHANTABILITY or FITNESS FOR A PARTICULAR PURPOSE. See the GNU LGPL for more details.

using System.CommandLine;
using System.Text;
using Asdamir.Core.AgentAudit;

namespace Asdamir.Tools.Commands;

/// <summary>
/// <c>asdamir audit verify-archive --path &lt;dir|zip&gt; [--expected-digest &lt;hex&gt;] [--json]</c>
///
/// Verifies an agent action ledger archive (Archive Format v1) <b>offline</b>: no network, no database, no
/// AppManagement, no commercial licence. When a closed range of the ledger is folded, those rows leave the live
/// database and survive only as an archive — so if the only thing able to check that archive were the closed
/// component that produced it, the assurance would collapse to "trust the vendor". This command, the published
/// specification and the open-core <see cref="AgentLedgerArchiveVerifier"/> exist so a third party never has to
/// take our word for it.
///
/// <para><b>THE ONE THING THIS COMMAND MUST NEVER DO</b> is print "Verified" for a run with no expected digest.
/// An archive can attest to two different things — (A) it has not been altered since it was written, and (B) it
/// is genuinely the segment folded out of a particular live ledger — and only (A) is provable from the archive
/// alone. The proof of (B) is the <c>FoldSegmentDigest</c> on the tombstone in that ledger, passed here as
/// <c>--expected-digest</c>. Without it the outcome is <c>INTERNALLY_CONSISTENT (UNANCHORED)</c>, and both the
/// text and the JSON say which of the two was established.</para>
///
/// <para><b>Output is English only, deliberately.</b> A verification finding is evidence quoted in an audit;
/// two people comparing runs must be reading the same sentence, and a locale-dependent message is a finding
/// they cannot compare. (The console renders localized product text; this is a different surface.)</para>
///
/// Exit codes — the four verdicts plus the refusal, each distinct, and identical to the independent Python
/// reference fixture so the two implementations cannot disagree about what a code MEANS:
///   0  VERIFIED               internal checks pass AND the supplied digest matches   (A and B)
///   1  INTERNALLY_CONSISTENT  internal checks pass, NO digest supplied — UNANCHORED  (A only)
///   2  DIGEST_MISMATCH        internal checks pass, the supplied digest does not match
///   3  BROKEN                 an internal check failed — altered or corrupt
///   4  FORMAT_ERROR           the shape or version was not understood; NOTHING was checked
///  64  usage error            a bad argument — deliberately OUTSIDE the 0-4 outcome band, so "the tool was
///                             invoked wrongly" can never be mistaken for "the archive was judged".
/// </summary>
public static class VerifyArchiveCommand
{
    /// <summary>Exit code for <see cref="ArchiveVerificationStatus.Verified"/> — the only success.</summary>
    public const int ExitVerified = 0;

    /// <summary>Exit code for <see cref="ArchiveVerificationStatus.InternallyConsistent"/> — unanchored, NOT success.</summary>
    public const int ExitInternallyConsistent = 1;

    /// <summary>Exit code for <see cref="ArchiveVerificationStatus.DigestMismatch"/>.</summary>
    public const int ExitDigestMismatch = 2;

    /// <summary>Exit code for <see cref="ArchiveVerificationStatus.Broken"/>.</summary>
    public const int ExitBroken = 3;

    /// <summary>Exit code for a format error — the archive was refused, so nothing was checked.</summary>
    public const int ExitFormatError = 4;

    /// <summary>Exit code for a bad invocation. Outside the outcome band on purpose (sysexits <c>EX_USAGE</c>).</summary>
    public const int ExitUsage = 64;

    /// <summary>Builds the <c>audit verify-archive</c> subcommand.</summary>
    public static Command Build()
    {
        var pathOpt = new Option<string>(
            new[] { "--path", "-p" },
            description: "The archive: a .zip container, or a directory holding manifest.json + segment.ndjson.")
        {
            IsRequired = true,
        };

        var digestOpt = new Option<string?>(
            new[] { "--expected-digest" },
            description: "The anchor — the FoldSegmentDigest recorded on the tombstone in the live ledger "
                       + "(64 hex characters). WITHOUT it the archive can only be shown to be internally "
                       + "consistent, never anchored, and the result is never VERIFIED.",
            getDefaultValue: () => null);

        var jsonOpt = new Option<bool>(
            new[] { "--json" },
            description: "Machine-readable output. provesNotAltered and provesAnchored are reported "
                       + "SEPARATELY — they are two different claims.",
            getDefaultValue: () => false);

        var cmd = new Command(
            "verify-archive",
            "Verify a folded agent-audit segment offline (Archive Format v1) — no network, no database, no licence.")
        {
            pathOpt, digestOpt, jsonOpt,
        };

        cmd.SetHandler(
            (path, digest, json) => Environment.Exit(Execute(path, digest, json)),
            pathOpt, digestOpt, jsonOpt);

        return cmd;
    }

    /// <summary>
    /// Runs the verification and RETURNS the exit code instead of terminating, so it is unit-testable.
    /// </summary>
    /// <param name="path">A <c>.zip</c> archive, or a directory holding the two entries.</param>
    /// <param name="expectedDigestHex">The tombstone's <c>FoldSegmentDigest</c>, or null for an unanchored run.</param>
    /// <param name="json">Emit machine-readable JSON instead of prose.</param>
    /// <param name="outWriter">Where output is written; defaults to <see cref="Console.Out"/>.</param>
    /// <param name="errWriter">Where usage errors are written; defaults to <see cref="Console.Error"/>.</param>
    /// <returns>0/1/2/3/4 per the outcome, or 64 for a usage error.</returns>
    internal static int Execute(
        string path, string? expectedDigestHex, bool json,
        TextWriter? outWriter = null, TextWriter? errWriter = null)
    {
        var @out = outWriter ?? Console.Out;
        var err = errWriter ?? Console.Error;

        if (string.IsNullOrWhiteSpace(path))
        {
            err.WriteLine("--path is required: point it at a .zip archive or at a directory holding "
                          + "manifest.json + segment.ndjson.");
            return ExitUsage;
        }

        if (expectedDigestHex is not null && !IsHash(expectedDigestHex))
        {
            err.WriteLine($"--expected-digest '{expectedDigestHex}' is not 64 hex characters. It is the "
                          + "FoldSegmentDigest from the tombstone in the live ledger (a 32-byte SHA-256).");
            return ExitUsage;
        }

        ArchiveVerificationResult result;

        if (Directory.Exists(path))
        {
            var manifestPath = Path.Combine(path, AgentLedgerArchiveManifest.ManifestEntryName);
            var segmentPath = Path.Combine(path, AgentLedgerArchiveManifest.SegmentEntryName);

            foreach (var required in new[] { manifestPath, segmentPath })
            {
                if (File.Exists(required)) continue;
                err.WriteLine($"'{path}' is a directory but does not hold '{Path.GetFileName(required)}'.");
                return ExitUsage;
            }

            // Read as raw bytes and decode without BOM detection: the format says both entries are UTF-8 with
            // NO byte-order mark, and silently swallowing one here would make this tool accept a file a
            // conforming verifier rejects.
            result = AgentLedgerArchiveVerifier.Default.Verify(
                Decode(File.ReadAllBytes(manifestPath)), Decode(File.ReadAllBytes(segmentPath)),
                expectedDigestHex);
        }
        else if (File.Exists(path))
        {
            result = AgentLedgerArchiveVerifier.Default.Verify(File.ReadAllBytes(path), expectedDigestHex);
        }
        else
        {
            err.WriteLine($"'{path}' does not exist.");
            return ExitUsage;
        }

        if (json) EmitJson(@out, result);
        else EmitText(@out, result);

        return ExitCodeFor(result);
    }

    /// <summary>Maps a result onto its exit code. A format error has no verdict, hence its own code.</summary>
    /// <param name="result">The verification result.</param>
    /// <returns>The process exit code.</returns>
    internal static int ExitCodeFor(ArchiveVerificationResult result) => result.Status switch
    {
        ArchiveVerificationStatus.Verified => ExitVerified,
        ArchiveVerificationStatus.InternallyConsistent => ExitInternallyConsistent,
        ArchiveVerificationStatus.DigestMismatch => ExitDigestMismatch,
        ArchiveVerificationStatus.Broken => ExitBroken,
        _ => ExitFormatError,
    };

    /// <summary>The stable outcome name, shared with the independent Python fixture.</summary>
    /// <param name="result">The verification result.</param>
    /// <returns><c>VERIFIED</c>, <c>INTERNALLY_CONSISTENT</c>, <c>DIGEST_MISMATCH</c>, <c>BROKEN</c> or <c>FORMAT_ERROR</c>.</returns>
    internal static string OutcomeOf(ArchiveVerificationResult result) => result.Status switch
    {
        ArchiveVerificationStatus.Verified => "VERIFIED",
        ArchiveVerificationStatus.InternallyConsistent => "INTERNALLY_CONSISTENT",
        ArchiveVerificationStatus.DigestMismatch => "DIGEST_MISMATCH",
        ArchiveVerificationStatus.Broken => "BROKEN",
        _ => "FORMAT_ERROR",
    };

    private static void EmitText(TextWriter @out, ArchiveVerificationResult result)
    {
        var position = result.SeqNo is { } seq ? $" at SeqNo {seq}" : string.Empty;

        @out.WriteLine($"{OutcomeOf(result)}{position}: {result.Message}");
        @out.WriteLine();

        // The two claims are printed as two lines, always both, in every outcome. Collapsing them into one
        // verdict line is exactly how a tool ends up telling a user their archive is "verified" when the only
        // thing established is that it has not been edited since somebody wrote it.
        @out.WriteLine("  Proven — this archive has not been altered since it was written : "
                       + YesNo(result.ProvesNotAltered));
        @out.WriteLine("  Proven — these rows ARE the segment folded from that live ledger : "
                       + YesNo(result.ProvesAnchored));

        if (!result.ProvesAnchored && result.Status is not null)
        {
            @out.WriteLine();
            @out.WriteLine("  UNANCHORED. Re-run with --expected-digest <the tombstone's FoldSegmentDigest> to "
                           + "establish the second claim.");
            @out.WriteLine("  The SegmentDigest inside manifest.json is NOT an anchor: it is derived from the "
                           + "very rows it accompanies.");
        }

        if (result.Manifest is { } manifest)
        {
            @out.WriteLine();
            @out.WriteLine($"  Scope    : AppId {manifest.AppId}, TenantId '{manifest.TenantId}'");
            @out.WriteLine($"  Range    : SeqNo {manifest.SeqStart}-{manifest.SeqEnd} ({manifest.RowCount} rows)");
            @out.WriteLine($"  Exported : {manifest.ExportedAtUtc} by {manifest.ProducerVersion}");
        }

        if (result.ComputedSegmentDigest is { } computed)
            @out.WriteLine($"  Digest   : {computed} (recomputed from the rows read)");

        @out.WriteLine($"  Rows re-derived: {result.RowsChecked}");
    }

    private static void EmitJson(TextWriter @out, ArchiveVerificationResult result)
    {
        var sb = new StringBuilder();
        sb.Append('{');
        sb.Append("\"result\":\"").Append(OutcomeOf(result)).Append("\",");
        sb.Append("\"code\":\"").Append(JsonEscape(result.Code)).Append("\",");
        sb.Append("\"isFormatError\":").Append(result.IsFormatError ? "true" : "false").Append(',');

        // TWO booleans, never one. They are the two independent claims of the assurance boundary, and a caller
        // that can only read a single "ok" flag will inevitably render an unanchored archive as proven.
        sb.Append("\"provesNotAltered\":").Append(result.ProvesNotAltered ? "true" : "false").Append(',');
        sb.Append("\"provesAnchored\":").Append(result.ProvesAnchored ? "true" : "false").Append(',');

        sb.Append("\"seqNo\":").Append(result.SeqNo?.ToString(System.Globalization.CultureInfo.InvariantCulture)
                                       ?? "null").Append(',');
        sb.Append("\"rowsChecked\":").Append(result.RowsChecked.ToString(System.Globalization.CultureInfo.InvariantCulture)).Append(',');
        sb.Append("\"divergedIn\":").Append(Quoted(result.DivergedIn)).Append(',');
        sb.Append("\"expectedHex\":").Append(Quoted(result.ExpectedHex)).Append(',');
        sb.Append("\"actualHex\":").Append(Quoted(result.ActualHex)).Append(',');
        sb.Append("\"computedSegmentDigest\":").Append(Quoted(result.ComputedSegmentDigest)).Append(',');
        sb.Append("\"expectedSegmentDigest\":").Append(Quoted(result.ExpectedSegmentDigest)).Append(',');
        sb.Append("\"exitCode\":").Append(ExitCodeFor(result).ToString(System.Globalization.CultureInfo.InvariantCulture)).Append(',');
        sb.Append("\"message\":\"").Append(JsonEscape(result.Message)).Append('"');
        sb.Append('}');

        @out.WriteLine(sb.ToString());
    }

    private static string YesNo(bool value) => value ? "YES" : "no";

    private static string Quoted(string? value) => value is null ? "null" : "\"" + JsonEscape(value) + "\"";

    private static string Decode(byte[] bytes) =>
        new UTF8Encoding(encoderShouldEmitUTF8Identifier: false).GetString(bytes);

    private static bool IsHash(string text)
    {
        if (text.Length != 64) return false;

        foreach (var c in text)
        {
            var ok = c is >= '0' and <= '9' or >= 'a' and <= 'f' or >= 'A' and <= 'F';
            if (!ok) return false;
        }

        return true;
    }

    private static string JsonEscape(string s)
    {
        var sb = new StringBuilder(s.Length + 8);
        foreach (var c in s)
        {
            switch (c)
            {
                case '"': sb.Append("\\\""); break;
                case '\\': sb.Append("\\\\"); break;
                case '\n': sb.Append("\\n"); break;
                case '\r': sb.Append("\\r"); break;
                case '\t': sb.Append("\\t"); break;
                default:
                    if (c < 0x20) sb.Append("\\u").Append(((int)c).ToString("x4", System.Globalization.CultureInfo.InvariantCulture));
                    else sb.Append(c);
                    break;
            }
        }
        return sb.ToString();
    }
}
