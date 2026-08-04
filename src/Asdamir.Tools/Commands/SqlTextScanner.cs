// Copyright (C) 2026 Orhan Özşahin — Asdamir.
// Licensed under the GNU Lesser General Public License v3.0. See LICENSE.
// SPDX-License-Identifier: LGPL-3.0-or-later
//
// This file is part of the Asdamir open core. It is free software: you can redistribute it
// and/or modify it under the terms of the GNU Lesser General Public License as published by the
// Free Software Foundation, either version 3 of the License, or (at your option) any later
// version. It is distributed WITHOUT ANY WARRANTY; without even the implied warranty of
// MERCHANTABILITY or FITNESS FOR A PARTICULAR PURPOSE. See the GNU LGPL for more details.

using System.Text;

namespace Asdamir.Tools.Commands;

/// <summary>
/// A tiny, single-pass T-SQL lexer used by the static SQL-seed gates (currently AUD016 — see
/// <see cref="PermissionPolicyScan"/>). It answers exactly two questions that a regex cannot answer
/// correctly: <b>which text is a comment</b> and <b>which text is a string literal</b>.
///
/// <para><b>Why this exists (the bug it retires).</b> The seed gates used to pull codes out of a
/// migration with a plain string-literal regex (<c>N?'((?:[^']|'')*)'</c>) applied to the RAW file. A
/// regex has no notion of comments, so a single unpaired apostrophe inside a comment — the word
/// <c>catalogue's</c> in a header block, an <c>it's</c> in a <c>--</c> note — opens a phantom "literal"
/// that runs to the next apostrophe in the file and <b>shifts literal pairing for everything after it</b>.
/// Both directions of that desync are wrong, and the second one is a security hole:</para>
/// <list type="bullet">
///   <item><description>a real seeded code becomes invisible → a correctly-seeded permission is reported
///   as missing (loud, merely annoying);</description></item>
///   <item><description>arbitrary <b>comment prose</b> lands inside the shifted pairing and is collected
///   as a "seeded code" → a policy requiring a permission that <b>no seed defines</b> passes the gate
///   (silent — and that policy is a guaranteed 403 for every user at runtime, which is precisely what
///   AUD016 exists to catch).</description></item>
/// </list>
///
/// <para>The scanner is deliberately minimal but correct on the shapes T-SQL actually uses: a
/// <c>--</c> line comment, a <c>/* … */</c> block comment (T-SQL <b>nests</b> these, so nesting depth is
/// tracked), a <c>'…'</c> literal in which an apostrophe is escaped by <b>doubling</b> it (<c>'it''s'</c>),
/// and the <c>N'…'</c> national-literal prefix (the <c>N</c> is an ordinary character before the quote —
/// the literal body is identical). Comment markers inside a literal are data, not comments; apostrophes
/// inside a comment are prose, not quotes. One pass resolves both, because the two questions are mutually
/// recursive and cannot be answered independently.</para>
/// </summary>
public static class SqlTextScanner
{
    /// <summary>
    /// Returns <paramref name="sql"/> with every <c>--</c> line comment and every (possibly nested)
    /// <c>/* … */</c> block comment blanked out — replaced by spaces, with <c>\r</c>/<c>\n</c> preserved so
    /// 1-based line numbers computed on the result still match the original file. String literals are
    /// copied through <b>verbatim</b> (a <c>--</c> or <c>/*</c> inside a literal is data, and a doubled
    /// <c>''</c> is consumed as an escaped apostrophe, not as a terminator).
    ///
    /// <para>An unterminated literal or block comment at end-of-file is consumed to EOF rather than
    /// throwing — the gates must survive a malformed file, not crash the build on it.</para>
    /// </summary>
    /// <param name="sql">Raw SQL text.</param>
    /// <returns>The same text, same length, with comment spans replaced by whitespace.</returns>
    public static string StripComments(string sql)
    {
        ArgumentNullException.ThrowIfNull(sql);

        var sb = new StringBuilder(sql.Length);
        var i = 0;
        var n = sql.Length;

        while (i < n)
        {
            var c = sql[i];

            // ---- String literal: copied verbatim; '' is an escaped apostrophe, not a terminator. ----
            if (c == '\'')
            {
                sb.Append(c);
                i++;
                while (i < n)
                {
                    if (sql[i] == '\'')
                    {
                        if (i + 1 < n && sql[i + 1] == '\'')
                        {
                            sb.Append("''");
                            i += 2;
                            continue;
                        }
                        sb.Append('\'');   // the terminator
                        i++;
                        break;
                    }
                    sb.Append(sql[i]);
                    i++;
                }
                continue;
            }

            // ---- `--` line comment: blank to end of line (newline itself is preserved below). ----
            if (c == '-' && i + 1 < n && sql[i + 1] == '-')
            {
                while (i < n && sql[i] != '\n')
                {
                    sb.Append(sql[i] == '\r' ? '\r' : ' ');
                    i++;
                }
                continue;
            }

            // ---- `/* … */` block comment: T-SQL nests these, so track depth. ----
            if (c == '/' && i + 1 < n && sql[i + 1] == '*')
            {
                var depth = 1;
                sb.Append("  ");
                i += 2;
                while (i < n && depth > 0)
                {
                    if (sql[i] == '/' && i + 1 < n && sql[i + 1] == '*')
                    {
                        depth++;
                        sb.Append("  ");
                        i += 2;
                        continue;
                    }
                    if (sql[i] == '*' && i + 1 < n && sql[i + 1] == '/')
                    {
                        depth--;
                        sb.Append("  ");
                        i += 2;
                        continue;
                    }
                    sb.Append(sql[i] is '\n' or '\r' ? sql[i] : ' ');
                    i++;
                }
                continue;
            }

            sb.Append(c);
            i++;
        }

        return sb.ToString();
    }

    /// <summary>
    /// Extracts the BODY of every T-SQL string literal in <paramref name="sql"/>, in source order, with
    /// doubled apostrophes un-escaped (<c>'it''s'</c> → <c>it's</c>). An <c>N</c> prefix needs no special
    /// case — it is an ordinary character preceding the opening quote, and the literal body is the same.
    ///
    /// <para>Callers should pass text that has already been through <see cref="StripComments"/>; this
    /// method trusts its input to be comment-free and will otherwise happily scan prose. An
    /// <b>unterminated</b> literal (no closing quote before EOF) is <b>dropped</b>, not returned as a
    /// truncated value: the file is malformed, and inventing a code from it is exactly the phantom-literal
    /// failure this type exists to prevent.</para>
    /// </summary>
    /// <param name="sql">SQL text, normally the output of <see cref="StripComments"/>.</param>
    /// <returns>Literal bodies in source order (duplicates included — the caller decides on a set).</returns>
    public static IReadOnlyList<string> ExtractStringLiterals(string sql)
    {
        ArgumentNullException.ThrowIfNull(sql);

        var result = new List<string>();
        var i = 0;
        var n = sql.Length;

        while (i < n)
        {
            if (sql[i] != '\'') { i++; continue; }

            i++;                       // step over the opening quote
            var sb = new StringBuilder();
            var terminated = false;
            while (i < n)
            {
                if (sql[i] == '\'')
                {
                    if (i + 1 < n && sql[i + 1] == '\'')
                    {
                        sb.Append('\'');   // escaped apostrophe
                        i += 2;
                        continue;
                    }
                    i++;                   // the terminator
                    terminated = true;
                    break;
                }
                sb.Append(sql[i]);
                i++;
            }

            if (terminated) result.Add(sb.ToString());
        }

        return result;
    }

    /// <summary>A slice of SQL text together with the 1-based line it starts on in the original file.</summary>
    /// <param name="Text">The slice's text, verbatim (newlines preserved).</param>
    /// <param name="Line">1-based line number of the slice's first character.</param>
    public sealed record SqlSlice(string Text, int Line);

    /// <summary>
    /// Splits <paramref name="sql"/> into T-SQL <b>batches</b> at the <c>GO</c> separator. <c>GO</c> is only a
    /// separator when it is the sole content of a line (optionally followed by a repeat count) — a <c>GO</c>
    /// inside a literal, an identifier, or in the middle of a statement is ordinary text.
    ///
    /// <para>Why batches matter to the seed gates: a <c>CREATE&#160;PROCEDURE</c> must be the FIRST statement of
    /// its batch, so "is this write inside a stored-procedure body?" — the question that separates the canonical
    /// <c>LocalizationResource_UpsertValue</c> writer from an ad-hoc seed MERGE — is answered by looking at the
    /// batch, with no need to find the procedure's <c>END</c>.</para>
    ///
    /// <para>Callers should pass text that has already been through <see cref="StripComments"/>.</para>
    /// </summary>
    /// <param name="sql">SQL text, normally the output of <see cref="StripComments"/>.</param>
    /// <returns>Non-blank batches in source order, each with its 1-based start line.</returns>
    public static IReadOnlyList<SqlSlice> SplitBatches(string sql)
    {
        ArgumentNullException.ThrowIfNull(sql);
        return Split(sql, splitOnSemicolon: false);
    }

    /// <summary>
    /// Splits <paramref name="sql"/> into statements at top-level <c>;</c> separators (and, defensively, at a
    /// <c>GO</c> line — a batch separator also ends a statement). Semicolons inside string literals and inside
    /// <c>[bracketed identifiers]</c> are data, not separators.
    ///
    /// <para><b>Known limit, stated rather than hidden:</b> T-SQL does not require statement terminators, so two
    /// unterminated statements in the same batch arrive here as ONE slice. The seed gates are built to tolerate
    /// that: over-merging can only widen a statement's context (a rule that fires on "this statement writes X
    /// AND matches Y" may fire on a merged pair), never split a real statement apart — so it can produce a loud
    /// false finding, never a silent pass. Callers grandfather such a case explicitly.</para>
    /// </summary>
    /// <param name="sql">SQL text, normally one batch from <see cref="SplitBatches"/>.</param>
    /// <returns>Non-blank statements in source order, each with its 1-based start line.</returns>
    public static IReadOnlyList<SqlSlice> SplitStatements(string sql)
    {
        ArgumentNullException.ThrowIfNull(sql);
        return Split(sql, splitOnSemicolon: true);
    }

    /// <summary>
    /// Splits an argument list (the text between, but not including, the delimiters) at commas that sit at
    /// paren-depth 0 and outside string literals / bracketed identifiers — so
    /// <c>N'a, b', @x, (1,2)</c> yields three arguments, not five. Each argument is returned trimmed.
    /// </summary>
    /// <param name="args">The raw argument text.</param>
    /// <returns>The arguments in source order.</returns>
    public static IReadOnlyList<string> SplitArguments(string args)
    {
        ArgumentNullException.ThrowIfNull(args);

        var result = new List<string>();
        var sb = new StringBuilder();
        var depth = 0;
        var i = 0;
        var n = args.Length;

        while (i < n)
        {
            var c = args[i];
            if (c == '\'') { CopyLiteral(args, ref i, sb); continue; }
            if (c == '[') { CopyBracket(args, ref i, sb); continue; }
            if (c == '(') { depth++; sb.Append(c); i++; continue; }
            if (c == ')') { depth--; sb.Append(c); i++; continue; }
            if (c == ',' && depth <= 0) { result.Add(sb.ToString().Trim()); sb.Clear(); i++; continue; }
            sb.Append(c);
            i++;
        }

        var tail = sb.ToString().Trim();
        if (tail.Length > 0 || result.Count > 0) result.Add(tail);
        return result;
    }

    /// <summary>
    /// Parses <paramref name="token"/> as a single T-SQL string literal — optionally <c>N</c>-prefixed — and
    /// returns its body with doubled apostrophes un-escaped. Returns <see langword="false"/> for anything that
    /// is not exactly one literal (a variable, an expression, a concatenation), which is how the seed gates
    /// tell "this argument is an inline literal row" from "this argument is fed from a tuple table".
    /// </summary>
    /// <param name="token">A single argument's text.</param>
    /// <param name="value">The literal body when the parse succeeds; otherwise <see langword="null"/>.</param>
    /// <returns>True when <paramref name="token"/> is exactly one string literal.</returns>
    public static bool TryParseStringLiteral(string token, out string? value)
    {
        value = null;
        if (token is null) return false;

        var t = token.Trim();
        if (t.Length >= 2 && (t[0] == 'N' || t[0] == 'n')) t = t[1..];
        if (t.Length < 2 || t[0] != '\'') return false;

        var sb = new StringBuilder();
        var i = 1;
        while (i < t.Length)
        {
            if (t[i] == '\'')
            {
                if (i + 1 < t.Length && t[i + 1] == '\'') { sb.Append('\''); i += 2; continue; }
                // Closing quote — anything after it means this token is not a bare literal.
                return i == t.Length - 1 && Assign(sb.ToString(), out value);
            }
            sb.Append(t[i]);
            i++;
        }
        return false;   // unterminated
    }

    private static bool Assign(string s, out string? target) { target = s; return true; }

    // Shared engine for SplitBatches / SplitStatements. `GO` always ends a slice; `;` does too when
    // splitOnSemicolon. Literals and [bracketed identifiers] are copied through so their contents never
    // separate anything. Line numbers are tracked so every slice knows where it starts in the file.
    private static IReadOnlyList<SqlSlice> Split(string sql, bool splitOnSemicolon)
    {
        var result = new List<SqlSlice>();
        var sb = new StringBuilder();
        var line = 1;
        var startLine = 1;
        var atLineStart = true;
        var i = 0;
        var n = sql.Length;

        void Flush(int nextStartLine)
        {
            var raw = sb.ToString();
            if (raw.Trim().Length > 0)
            {
                // Drop LEADING whitespace and advance the reported line past it, so `Line` is the line of the
                // slice's first real character. Callers locate a match inside `Text` as
                // `Line + <newlines in Text before the match>` — that arithmetic is only exact if the text
                // does not start with the blank tail of the previous statement.
                var lead = 0;
                var leadLines = 0;
                while (lead < raw.Length && char.IsWhiteSpace(raw[lead]))
                {
                    if (raw[lead] == '\n') leadLines++;
                    lead++;
                }
                result.Add(new SqlSlice(raw[lead..], startLine + leadLines));
            }
            sb.Clear();
            startLine = nextStartLine;
        }

        while (i < n)
        {
            if (atLineStart && IsGoLine(sql, i, out var afterGo, out var goLines))
            {
                Flush(line + goLines);
                i = afterGo;
                line += goLines;
                atLineStart = true;
                continue;
            }

            var c = sql[i];
            if (c == '\'') { var before = i; CopyLiteral(sql, ref i, sb); line += CountNewlines(sql, before, i); atLineStart = false; continue; }
            if (c == '[') { var before = i; CopyBracket(sql, ref i, sb); line += CountNewlines(sql, before, i); atLineStart = false; continue; }
            if (c == ';' && splitOnSemicolon) { Flush(line); i++; atLineStart = false; continue; }
            if (c == '\n') { line++; sb.Append(c); i++; atLineStart = true; continue; }

            sb.Append(c);
            i++;
            if (c != ' ' && c != '\t' && c != '\r') atLineStart = false;
        }

        Flush(line);
        return result;
    }

    // True when position `i` (known to be at the start of a line, modulo leading blanks) begins a lone `GO`
    // batch separator — optionally followed by a repeat count — with nothing else on the line.
    private static bool IsGoLine(string sql, int i, out int afterGo, out int linesConsumed)
    {
        afterGo = i;
        linesConsumed = 0;

        var j = i;
        while (j < sql.Length && (sql[j] == ' ' || sql[j] == '\t')) j++;
        if (j + 1 >= sql.Length) return false;
        if ((sql[j] != 'G' && sql[j] != 'g') || (sql[j + 1] != 'O' && sql[j + 1] != 'o')) return false;

        var k = j + 2;
        while (k < sql.Length && (sql[k] == ' ' || sql[k] == '\t')) k++;
        while (k < sql.Length && char.IsAsciiDigit(sql[k])) k++;
        while (k < sql.Length && (sql[k] == ' ' || sql[k] == '\t' || sql[k] == '\r')) k++;
        if (k < sql.Length && sql[k] != '\n') return false;

        if (k < sql.Length) { k++; linesConsumed = 1; }   // consume the newline
        afterGo = k;
        return true;
    }

    private static int CountNewlines(string s, int from, int to)
    {
        var count = 0;
        for (var i = from; i < to && i < s.Length; i++) if (s[i] == '\n') count++;
        return count;
    }

    // Copies a '…' literal (with '' escapes) verbatim from `s[i]` into `sb`, advancing `i` past it.
    private static void CopyLiteral(string s, ref int i, StringBuilder sb)
    {
        sb.Append(s[i]);
        i++;
        while (i < s.Length)
        {
            if (s[i] == '\'')
            {
                if (i + 1 < s.Length && s[i + 1] == '\'') { sb.Append("''"); i += 2; continue; }
                sb.Append('\'');
                i++;
                return;
            }
            sb.Append(s[i]);
            i++;
        }
    }

    // Copies a [bracketed identifier] (with ]] escapes) verbatim from `s[i]` into `sb`, advancing `i` past it.
    private static void CopyBracket(string s, ref int i, StringBuilder sb)
    {
        sb.Append(s[i]);
        i++;
        while (i < s.Length)
        {
            if (s[i] == ']')
            {
                if (i + 1 < s.Length && s[i + 1] == ']') { sb.Append("]]"); i += 2; continue; }
                sb.Append(']');
                i++;
                return;
            }
            sb.Append(s[i]);
            i++;
        }
    }
}
