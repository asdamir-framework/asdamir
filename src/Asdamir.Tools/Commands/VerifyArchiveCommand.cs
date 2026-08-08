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
using System.CommandLine.Parsing;
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
/// <para><b>Exit codes.</b> The NORMATIVE contract is the table in <c>docs/cli.md</c> ("Exit codes — NORMATIVE"),
/// which a third party's audit script may rely on; it is stated there once so the two cannot drift. In short:
/// <c>0</c>–<c>4</c> are claims about an archive — <c>VERIFIED</c>, <c>INTERNALLY_CONSISTENT</c> (unanchored),
/// <c>DIGEST_MISMATCH</c>, <c>BROKEN</c>, <c>FORMAT_ERROR</c> — and <b>nothing else may occupy them</b>. Every
/// invocation that produces no such claim exits <c>64</c>, including <c>--help</c> and <c>--version</c>: on this
/// command <c>0</c> is not "the program ran", it is the assertion that the archive is unaltered and anchored.
/// The four verdict codes match the independent Python reference fixture, so two implementations cannot
/// disagree about what a code MEANS. Enforced by <c>VerifyArchiveExitCodeBandTests</c> against the real
/// parse-and-invoke path — see <see cref="NormalizeParserExitCode"/>.</para>
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

    /// <summary>
    /// Records whether the verify-archive handler actually ran. Threaded explicitly rather than kept in a
    /// static field so that tests — which invoke the command in parallel — cannot observe each other's runs.
    /// </summary>
    internal sealed class VerdictReached
    {
        /// <summary>True once the handler has produced a verdict for this invocation.</summary>
        internal bool Value { get; set; }
    }

    /// <summary>Builds the <c>audit verify-archive</c> subcommand.</summary>
    public static Command Build() => Build(new VerdictReached());

    /// <summary>Builds the subcommand and reports, through <paramref name="verdictReached"/>, whether a verdict was produced.</summary>
    internal static Command Build(VerdictReached verdictReached)
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

        // The handler sets InvocationContext.ExitCode instead of calling Environment.Exit. Environment.Exit
        // tore the process down from inside the handler, which made the invocation pipeline untestable and —
        // worse — hid the fact that some invocations never reach here at all. See NormalizeParserExitCode.
        cmd.SetHandler(ctx =>
        {
            verdictReached.Value = true;
            ctx.ExitCode = Execute(
                ctx.ParseResult.GetValueForOption(pathOpt) ?? string.Empty,
                ctx.ParseResult.GetValueForOption(digestOpt),
                ctx.ParseResult.GetValueForOption(jsonOpt));

            return Task.CompletedTask;
        });

        return cmd;
    }

    /// <summary>
    /// Forces every invocation that produced <b>no verdict</b> out of the <c>0</c>–<c>4</c> band and onto
    /// <see cref="ExitUsage"/>.
    /// </summary>
    /// <remarks>
    /// <para><b>Why this exists.</b> The <c>0</c>–<c>4</c> codes are factual claims about an archive, and a
    /// third party's audit script reads them as such. But <c>System.CommandLine</c> answers a whole class of
    /// invocations <b>before</b> the handler runs — an unknown flag, a required option omitted, an option given
    /// with no value, a stray argument — and returned <c>1</c> for all of them. <c>1</c> is
    /// <c>INTERNALLY_CONSISTENT</c>. So <c>verify-archive --pth ./segment.zip</c> — one typo — made a script log
    /// "the archive is intact" for a command that verified nothing. <c>--help</c> and <c>--version</c> were worse
    /// still: they returned <c>0</c>, which is <c>VERIFIED</c>.</para>
    /// <para><b>Why help and version are included</b>, against the usual convention that help exits <c>0</c>: on
    /// this command <c>0</c> is not "the program ran" — it is the assertion <i>this archive is unaltered and
    /// anchored to the ledger it claims to come from</i>. Help cannot borrow that code. Nothing else in the CLI
    /// is affected; the rule is scoped to <c>verify-archive</c>, where the exit code is evidence.</para>
    /// <para>The check is <b>positive, not a blacklist of known parser errors</b>: the handler is the only thing
    /// that may hand out a <c>0</c>–<c>4</c>, so anything that did not run it is a usage error by construction.
    /// A future System.CommandLine that invents a new pre-handler outcome is therefore covered already — which
    /// is exactly the way the original gate failed, by enumerating the cases that existed when it was written.</para>
    /// </remarks>
    /// <param name="parseResult">The parse result for the whole invocation.</param>
    /// <param name="handlerRan">Whether the verify-archive handler actually produced a verdict.</param>
    /// <param name="exitCode">The exit code System.CommandLine returned.</param>
    /// <returns><paramref name="exitCode"/> when a verdict was produced; otherwise <see cref="ExitUsage"/>.</returns>
    internal static int NormalizeParserExitCode(ParseResult parseResult, bool handlerRan, int exitCode)
    {
        if (handlerRan)
        {
            return exitCode;
        }

        return TargetsVerifyArchive(parseResult) ? ExitUsage : exitCode;
    }

    /// <summary>
    /// True when the invocation was aimed at <c>audit verify-archive</c> — including when it failed to parse,
    /// where the command result points at the deepest command that DID parse and the rest lands in the errors.
    /// </summary>
    private static bool TargetsVerifyArchive(ParseResult parseResult)
    {
        for (var command = parseResult.CommandResult.Command; command is not null;)
        {
            if (command.Name == "verify-archive")
            {
                return true;
            }

            command = command.Parents.OfType<Command>().FirstOrDefault();
        }

        // A token-level failure ("--pth" typo'd before the option is matched) can leave CommandResult on the
        // parent, so fall back to the raw tokens. Both paths are covered by the usage-error test table.
        return parseResult.Tokens.Any(t => t.Value == "verify-archive");
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
