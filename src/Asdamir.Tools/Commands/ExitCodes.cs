// Copyright (C) 2026 Orhan Özşahin — Asdamir.
// Licensed under the GNU Lesser General Public License v3.0. See LICENSE.
// SPDX-License-Identifier: LGPL-3.0-or-later
//
// This file is part of the Asdamir open core. It is free software: you can redistribute it
// and/or modify it under the terms of the GNU Lesser General Public License as published by the
// Free Software Foundation, either version 3 of the License, or (at your option) any later
// version. It is distributed WITHOUT ANY WARRANTY; without even the implied warranty of
// MERCHANTABILITY or FITNESS FOR A PARTICULAR PURPOSE. See the GNU LGPL for more details.

namespace Asdamir.Tools.Commands;

/// <summary>
/// The CLI's exit-code contract, in one place. The normative statement for users is the "Exit codes" section
/// of <c>docs/cli.md</c>; this type is what the code compiles against, so the two cannot drift.
/// </summary>
/// <remarks>
/// <para><b>The model — one rule, every command.</b> Codes are split into two disjoint kinds:</para>
/// <list type="bullet">
/// <item><description><b>Results</b> — a statement about what the command examined. Each command owns its own
/// small result band starting at <see cref="Success"/>: a gate uses <c>0</c>/<c>1</c> (clean / findings),
/// <c>verify-archive</c> uses <c>0</c>–<c>4</c> (its four verdicts plus a refusal).</description></item>
/// <item><description><b>Usage</b> — <see cref="Usage"/>. The command was invoked wrongly, so it examined
/// nothing and is making no statement at all.</description></item>
/// </list>
/// <para><b>Why one number for the whole CLI rather than per-command.</b> A tool has a single "you invoked
/// this wrong" code or it effectively has none: a wrapper script that shells several subcommands would
/// otherwise need a per-command lookup table, and getting that table wrong is silent. <c>2</c> — the previous
/// house convention, used at 52 sites — cannot be that number, because in <c>verify-archive</c> <c>2</c> is a
/// VERDICT (<c>DIGEST_MISMATCH</c>). So the CLI already could not use <c>2</c> uniformly; the real choice was
/// <c>64</c> everywhere or <c>2</c>-except-one-command, and a rule with an exception is the kind nobody
/// remembers. <c>64</c> is <c>EX_USAGE</c> from sysexits(3), permanently outside any result band we might
/// grow later.</para>
/// <para><b>Why this matters more than tidiness — measured, not theorised.</b> Before this, every usage error
/// that <c>System.CommandLine</c> answered <i>before the handler ran</i> — a typo'd flag, an omitted required
/// option, an option given without its value, a stray argument, <c>--version</c> on a subcommand — exited
/// <c>1</c>. For the audit gates <c>1</c> is documented as <b>"findings — fails the build"</b>, so
/// <c>asdamir audit lint --pth src</c> reported a mistyped command line as a failing lint. In
/// <c>verify-archive</c> the same leak made <c>1</c> mean "the archive is intact" and <c>--help</c> mean
/// "VERIFIED". Handler-raised usage errors were correct all along, which is exactly why nothing noticed.</para>
/// <para><b>The enforcement is positive, not a blacklist</b> (see <c>Program.RunAsync</c>): a handler is the
/// only thing that may produce a result code, so any invocation that did not reach one is a usage error by
/// construction. Enumerating today's parser errors would go blind the moment the parser grows a new
/// pre-handler outcome — which is precisely how the previous gate went blind.</para>
/// </remarks>
public static class ExitCodes
{
    /// <summary>The command ran and its result is the affirmative one (clean gate, verified archive, done).</summary>
    public const int Success = 0;

    /// <summary>
    /// The command ran and its result is the negative one — a gate found something, a verification did not
    /// hold. Still a RESULT: the command did its job and is reporting what it saw.
    /// </summary>
    public const int Findings = 1;

    /// <summary>
    /// The command was invoked wrongly and therefore examined nothing — <c>EX_USAGE</c> (sysexits(3)).
    /// Deliberately outside every command's result band, so "invoked wrongly" can never be read as a finding,
    /// a verdict, or a clean run.
    /// </summary>
    public const int Usage = 64;

    /// <summary>
    /// A scaffolding command REFUSED to act because its target already exists and is non-empty — a RESULT,
    /// not a usage error: the command ran, examined the target, and declined rather than overwriting.
    /// </summary>
    /// <remarks>
    /// Kept distinct from <see cref="Success"/> so a script can tell "generated" from "already there", and
    /// distinct from <see cref="Usage"/> because the invocation was perfectly well-formed.
    /// </remarks>
    public const int RefusedExistingTarget = 3;

    /// <summary>
    /// The command failed for a reason that is neither a result nor a bad invocation — an unexpected internal
    /// error. <c>EX_SOFTWARE</c> (sysexits(3)).
    /// </summary>
    /// <remarks>
    /// Distinct from <see cref="Usage"/> on purpose: telling a user "you typed it wrong" when the tool itself
    /// broke sends them to fix the wrong thing. Distinct from the result band for the same reason every other
    /// non-result is — a crash examined nothing, so it may not report a finding or a clean run.
    /// <para>Reaching this code is a BUG REPORT, not a supported outcome. The two occurrences found by
    /// measurement were both mis-shaped input that should have been rejected as usage errors, and both now
    /// are; this exists so the next one prints a sentence instead of a stack trace.</para>
    /// </remarks>
    public const int Internal = 70;
}

/// <summary>
/// Records whether a command's handler actually ran during one invocation — the single fact that separates a
/// RESULT from a USAGE error.
/// </summary>
/// <remarks>
/// Threaded explicitly through <c>Program.RunAsync</c> rather than kept in a static field, so that tests —
/// which drive the real parse-and-invoke path, in parallel — cannot observe each other's invocations.
/// </remarks>
public sealed class HandlerReached
{
    /// <summary>True once a command handler has produced a result for this invocation.</summary>
    public bool Value { get; set; }
}

/// <summary>
/// Validation shared by every command that takes a PascalCase name as a POSITIONAL argument.
/// </summary>
/// <remarks>
/// <para>A positional argument happily swallows a mistyped option: <c>asdamir new app --bogus</c> hands
/// <c>--bogus</c> to the command as the NAME. Seven commands each had their own copy of
/// <c>!char.IsUpper(name[0])</c>, so all seven answered a typo'd flag with <i>"must be PascalCase"</i> —
/// blaming the user's capitalisation for what was actually a spelling mistake in an option, which is the
/// harder of the two to spot. The exit code was already right; the message sent people to the wrong place.</para>
/// <para>One implementation, because seven copies of a rule are seven chances for the next one to drift —
/// the same reasoning as the single exit-code contract itself.</para>
/// </remarks>
public static class NameArgument
{
    /// <summary>True when the value is a usable PascalCase identifier (and therefore not an option).</summary>
    /// <param name="value">The candidate name.</param>
    /// <returns>True when the value may be used as a name.</returns>
    /// <remarks>
    /// <para><b>An identifier, not merely "starts with a capital".</b> The name flows verbatim into file
    /// paths, C# type names, SQL object names and shell command lines, so the previous check (first character
    /// upper-case, anything after it) accepted <c>A/../../x</c> (a path that leaves the output directory),
    /// <c>A'; DROP TABLE x--</c> (SQL) and <c>A$(id)</c> (shell). The rule is now the intersection of what is
    /// safe in all four places: an ASCII letter A–Z first, then ASCII letters, digits or underscores, at most
    /// <see cref="MaxLength"/> characters. ASCII on purpose — a Unicode letter is a legal C# identifier but
    /// not a safe unquoted SQL identifier or file name on every platform.</para>
    /// </remarks>
    public static bool IsValid(string? value)
    {
        if (string.IsNullOrWhiteSpace(value) || value.Length > MaxLength) return false;
        if (value[0] is < 'A' or > 'Z') return false;

        foreach (var c in value)
        {
            var ok = c is >= 'A' and <= 'Z' or >= 'a' and <= 'z' or >= '0' and <= '9' or '_';
            if (!ok) return false;
        }

        return true;
    }

    /// <summary>The longest name accepted — well inside SQL Server's 128-character identifier limit once a
    /// plural suffix and a <c>PK_</c>/<c>IX_</c> prefix are added, and inside every file system's name limit.</summary>
    public const int MaxLength = 100;

    /// <summary>
    /// True when <paramref name="value"/> is a dotted sequence of PascalCase identifiers (<c>Acme.Orders</c>) —
    /// the shape of a namespace or a project name. Each segment must satisfy <see cref="IsValid"/>.
    /// </summary>
    /// <param name="value">The candidate namespace / project name.</param>
    /// <returns>True when every dot-separated segment is a valid name.</returns>
    public static bool IsValidDotted(string? value)
        => !string.IsNullOrWhiteSpace(value)
           && value.Length <= 200
           && value.Split('.').All(IsValid);

    /// <summary>
    /// True when <paramref name="value"/> is usable as a SQL Server database name without quoting: an ASCII
    /// letter or underscore first, then letters, digits or underscores. It is composed into a connection
    /// string (where <c>;</c> starts a new keyword) and printed into shell commands, so nothing else is allowed.
    /// </summary>
    /// <param name="value">The candidate database name.</param>
    /// <returns>True when the value is a plain identifier.</returns>
    public static bool IsValidDatabaseName(string? value)
    {
        if (string.IsNullOrWhiteSpace(value) || value.Length > 128) return false;
        if (!(value[0] is >= 'A' and <= 'Z' or >= 'a' and <= 'z' or '_')) return false;
        return value.All(c => c is >= 'A' and <= 'Z' or >= 'a' and <= 'z' or >= '0' and <= '9' or '_');
    }

    /// <summary>
    /// Escapes <paramref name="value"/> for use INSIDE a T-SQL string literal (<c>N'…'</c>) — doubles every
    /// single quote. The only correct way to place user text in a generated <c>.sql</c> file, which cannot be
    /// parameterised: <c>--admin-email o'brien@x.com</c> used to terminate the literal and break the seed.
    /// </summary>
    /// <param name="value">The raw text.</param>
    /// <returns>The text with each <c>'</c> doubled.</returns>
    public static string SqlLiteral(string value) => value.Replace("'", "''", StringComparison.Ordinal);

    /// <summary>
    /// The message to print when <see cref="IsValid"/> is false — it distinguishes "you mistyped an option"
    /// from "your name is not PascalCase", which is the whole point of having this in one place.
    /// </summary>
    /// <param name="value">The rejected value.</param>
    /// <param name="what">What the argument is, for the message (e.g. "app name", "entity name").</param>
    /// <param name="usage">The correct invocation, e.g. <c>asdamir new app &lt;Name&gt;</c>.</param>
    /// <returns>A message naming the actual mistake.</returns>
    public static string Explain(string? value, string what, string usage)
        => value is not null && value.StartsWith('-')
            ? $"'{value}' looks like an option, not {Article(what)} {what}. If you meant an option, check its "
              + $"spelling; the name is the first positional argument: {usage}"
            : $"{char.ToUpperInvariant(what[0])}{what[1..]} must be a PascalCase identifier — an ASCII letter A–Z "
              + $"first, then letters, digits or underscores only (no spaces, dots, slashes or quotes; at most "
              + $"{MaxLength} characters), e.g. {usage}";

    /// <summary>"a" or "an" for <paramref name="noun"/> — by its first SOUND, not by its first letter.</summary>
    /// <remarks>
    /// A one-line thing to get wrong and a jarring thing to read: the first attempt tested
    /// <c>what.StartsWith('a')</c>, which produced "not a entity name" — grammatically wrong in a message
    /// whose only job is to be read carefully by someone who has just made a mistake.
    /// </remarks>
    /// <param name="noun">The noun phrase that follows the article.</param>
    /// <returns>"an" before a vowel sound, otherwise "a".</returns>
    private static string Article(string noun)
        => noun.Length > 0 && "aeiou".Contains(char.ToLowerInvariant(noun[0])) ? "an" : "a";
}
