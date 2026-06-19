// Copyright (C) 2026 Orhan Özşahin — Asdamir.
// Licensed under the GNU Lesser General Public License v3.0. See LICENSE.
// SPDX-License-Identifier: LGPL-3.0-or-later
//
// This file is part of the Asdamir open core. It is free software: you can redistribute it
// and/or modify it under the terms of the GNU Lesser General Public License as published by the
// Free Software Foundation, either version 3 of the License, or (at your option) any later
// version. It is distributed WITHOUT ANY WARRANTY; without even the implied warranty of
// MERCHANTABILITY or FITNESS FOR A PARTICULAR PURPOSE. See the GNU LGPL for more details.

using System.Text.RegularExpressions;
using System.Collections;
using System.Reflection;
using System.Text;

namespace Asdamir.Tools.Commands;

/// <summary>
/// Tiny dependency-free template engine for the framework's .sbn templates.
///
/// Why hand-rolled: Scriban 6.x carries a transitive YamlDotNet vulnerability chain that
/// turns into a `Warning As Error` under audit-grade NuGet settings. Razor/RazorLight is
/// overkill. The templates only use three syntactic features:
///
///   • Substitution         <c>{{ EntityName }}</c>, <c>{{ f.CSharpType }}</c>
///   • Iteration            <c>{{~ for f in Fields ~}} ... {{~ end ~}}</c>
///                          (loop state <c>for.last</c> / <c>for.first</c> / <c>for.index</c> available)
///   • Conditional          <c>{{~ if expr ~}} ... {{~ else if expr ~}} ... {{~ else ~}} ... {{~ end ~}}</c>
///
/// Expressions support: identifiers, dotted property access, equality (==, !=) against
/// string/bool/null literals, logical AND/OR (&amp;&amp; / ||), and unary NOT (!).
/// </summary>
public static class TemplateRenderer
{
    private const string ResourcePrefix = "Asdamir.Tools.Templates.";

    public static string Render(string name, object model)
    {
        var asm = typeof(TemplateRenderer).Assembly;
        var resource = $"{ResourcePrefix}{name}.sbn";
        using var stream = asm.GetManifestResourceStream(resource)
            ?? throw new InvalidOperationException(
                $"Template '{resource}' not found. Available: " +
                string.Join(", ", asm.GetManifestResourceNames()));

        using var reader = new StreamReader(stream);
        var source = reader.ReadToEnd();

        var nodes = TemplateParser.Parse(source);
        var sb = new StringBuilder(source.Length * 2);
        var scope = new Scope(model);
        foreach (var node in nodes) node.Render(sb, scope);
        return sb.ToString();
    }

    /// <summary>
    /// Reads an embedded asset verbatim (no templating). Used for static SQL assets such
    /// as the managed-app DB schema, which carry no per-app tokens. <paramref name="fileName"/>
    /// includes the extension, e.g. <c>"DbSchema.sql"</c>.
    /// </summary>
    public static string ReadAsset(string fileName)
    {
        var asm = typeof(TemplateRenderer).Assembly;
        var resource = $"{ResourcePrefix}{fileName}";
        using var stream = asm.GetManifestResourceStream(resource)
            ?? throw new InvalidOperationException(
                $"Asset '{resource}' not found. Available: " +
                string.Join(", ", asm.GetManifestResourceNames()));
        using var reader = new StreamReader(stream);
        return reader.ReadToEnd();
    }
}

internal sealed class Scope
{
    private readonly Dictionary<string, object?> _locals = new(StringComparer.Ordinal);
    private readonly Scope? _parent;
    public object? Root { get; }

    public Scope(object root) { Root = root; _parent = null; }
    private Scope(Scope parent) { _parent = parent; Root = parent.Root; }

    public Scope Child() => new(this);
    public void Set(string name, object? value) => _locals[name] = value;

    public bool TryGetLocal(string name, out object? value)
    {
        if (_locals.TryGetValue(name, out value)) return true;
        if (_parent is not null && _parent.TryGetLocal(name, out value)) return true;
        value = null;
        return false;
    }
}

internal static class Resolver
{
    public static object? Resolve(string expression, Scope scope)
    {
        var parts = expression.Split('.', StringSplitOptions.RemoveEmptyEntries);
        if (parts.Length == 0) return null;

        object? current;
        if (scope.TryGetLocal(parts[0], out var local))
            current = local;
        else if (scope.Root is not null)
            current = GetMember(scope.Root, parts[0]);
        else
            current = null;

        for (var i = 1; i < parts.Length && current is not null; i++)
            current = GetMember(current, parts[i]);

        return current;
    }

    private static object? GetMember(object obj, string name)
    {
        var type = obj.GetType();
        var prop = type.GetProperty(name, BindingFlags.Public | BindingFlags.Instance);
        if (prop is not null) return prop.GetValue(obj);
        var field = type.GetField(name, BindingFlags.Public | BindingFlags.Instance);
        return field?.GetValue(obj);
    }
}

internal abstract class TemplateNode
{
    public abstract void Render(StringBuilder sb, Scope scope);
}

internal sealed class TextNode : TemplateNode
{
    public string Text { get; }
    public TextNode(string text) => Text = text;
    public override void Render(StringBuilder sb, Scope scope) => sb.Append(Text);
}

internal sealed class SubstNode : TemplateNode
{
    public string Expression { get; }
    public SubstNode(string expr) => Expression = expr.Trim();

    public override void Render(StringBuilder sb, Scope scope)
    {
        var value = Resolver.Resolve(Expression, scope);
        if (value is null) return;
        if (value is bool b) { sb.Append(b ? "true" : "false"); return; }
        sb.Append(value);
    }
}

internal sealed class ForNode : TemplateNode
{
    public string LoopVar { get; }
    public string CollectionExpr { get; }
    public IReadOnlyList<TemplateNode> Body { get; }

    public ForNode(string loopVar, string collectionExpr, IReadOnlyList<TemplateNode> body)
    {
        LoopVar = loopVar;
        CollectionExpr = collectionExpr;
        Body = body;
    }

    public override void Render(StringBuilder sb, Scope scope)
    {
        var collection = Resolver.Resolve(CollectionExpr, scope) as IEnumerable
            ?? throw new InvalidOperationException($"For-loop expects an enumerable, '{CollectionExpr}' is not.");

        var list = collection.Cast<object?>().ToList();
        for (var i = 0; i < list.Count; i++)
        {
            var child = scope.Child();
            child.Set(LoopVar, list[i]);
            child.Set("for", new LoopState { last = i == list.Count - 1, first = i == 0, index = i });
            foreach (var node in Body) node.Render(sb, child);
        }
    }

    internal sealed class LoopState
    {
        public bool last { get; init; }
        public bool first { get; init; }
        public int index { get; init; }
    }
}

internal sealed class IfNode : TemplateNode
{
    public IReadOnlyList<(string? Condition, IReadOnlyList<TemplateNode> Body)> Branches { get; }
    public IfNode(IReadOnlyList<(string?, IReadOnlyList<TemplateNode>)> branches) => Branches = branches;

    public override void Render(StringBuilder sb, Scope scope)
    {
        foreach (var (cond, body) in Branches)
        {
            if (cond is null || ExpressionEvaluator.EvalBool(cond, scope))
            {
                foreach (var node in body) node.Render(sb, scope);
                return;
            }
        }
    }
}

internal static class ExpressionEvaluator
{
    public static bool EvalBool(string expression, Scope scope)
    {
        var tokens = Tokenize(expression);
        var idx = 0;
        return ParseOr(tokens, ref idx, scope);
    }

    private static bool ParseOr(List<string> tokens, ref int idx, Scope scope)
    {
        var left = ParseAnd(tokens, ref idx, scope);
        while (idx < tokens.Count && tokens[idx] == "||")
        {
            idx++;
            var right = ParseAnd(tokens, ref idx, scope);
            left = left || right;
        }
        return left;
    }

    private static bool ParseAnd(List<string> tokens, ref int idx, Scope scope)
    {
        var left = ParseUnary(tokens, ref idx, scope);
        while (idx < tokens.Count && tokens[idx] == "&&")
        {
            idx++;
            var right = ParseUnary(tokens, ref idx, scope);
            left = left && right;
        }
        return left;
    }

    private static bool ParseUnary(List<string> tokens, ref int idx, Scope scope)
    {
        if (idx < tokens.Count && tokens[idx] == "!")
        {
            idx++;
            return !ParsePrimary(tokens, ref idx, scope);
        }
        return ParsePrimary(tokens, ref idx, scope);
    }

    private static bool ParsePrimary(List<string> tokens, ref int idx, Scope scope)
    {
        var leftVal = ParseValue(tokens, ref idx, scope);
        if (idx < tokens.Count && (tokens[idx] == "==" || tokens[idx] == "!="))
        {
            var op = tokens[idx++];
            var rightVal = ParseValue(tokens, ref idx, scope);
            var equal = Equals(leftVal, rightVal);
            return op == "==" ? equal : !equal;
        }
        return ToBool(leftVal);
    }

    private static object? ParseValue(List<string> tokens, ref int idx, Scope scope)
    {
        if (idx >= tokens.Count) return null;
        var tok = tokens[idx++];
        if (tok.Length >= 2 && tok[0] == '"' && tok[^1] == '"') return tok[1..^1];
        if (tok == "true") return true;
        if (tok == "false") return false;
        if (tok == "null") return null;
        return Resolver.Resolve(tok, scope);
    }

    private static bool ToBool(object? v) => v switch
    {
        null => false,
        bool b => b,
        string s => !string.IsNullOrEmpty(s),
        _ => true,
    };

    private static List<string> Tokenize(string s)
    {
        var tokens = new List<string>();
        var i = 0;
        while (i < s.Length)
        {
            var c = s[i];
            if (char.IsWhiteSpace(c)) { i++; continue; }

            if (c == '"')
            {
                var start = i++;
                while (i < s.Length && s[i] != '"') i++;
                if (i >= s.Length) throw new InvalidOperationException("Unterminated string literal in expression.");
                tokens.Add(s[start..(i + 1)]);
                i++;
                continue;
            }

            if (i + 1 < s.Length)
            {
                var pair = s.Substring(i, 2);
                if (pair is "==" or "!=" or "&&" or "||")
                {
                    tokens.Add(pair);
                    i += 2;
                    continue;
                }
            }

            if (c is '!' or '(' or ')')
            {
                tokens.Add(c.ToString());
                i++;
                continue;
            }

            if (char.IsLetter(c) || c == '_')
            {
                var start = i;
                while (i < s.Length && (char.IsLetterOrDigit(s[i]) || s[i] == '_' || s[i] == '.')) i++;
                tokens.Add(s[start..i]);
                continue;
            }

            throw new InvalidOperationException($"Unexpected character '{c}' in expression '{s}'.");
        }
        return tokens;
    }
}

internal static class TemplateParser
{
    private static readonly Regex TagPattern = new(
        @"\{\{(?<trimLeft>~?)\s*(?<content>.*?)\s*(?<trimRight>~?)\}\}",
        RegexOptions.Singleline | RegexOptions.Compiled);

    public static IReadOnlyList<TemplateNode> Parse(string source)
    {
        var tokens = Lex(source);
        var idx = 0;
        return ParseNodes(tokens, ref idx, terminator: null);
    }

    private static IReadOnlyList<TemplateNode> ParseNodes(IReadOnlyList<Token> tokens, ref int idx, string? terminator)
    {
        var nodes = new List<TemplateNode>();
        while (idx < tokens.Count)
        {
            var token = tokens[idx];
            if (token.Kind == TokenKind.Text)
            {
                nodes.Add(new TextNode(token.Content));
                idx++;
                continue;
            }

            var content = token.Content;
            if (terminator != null && (content == "end" || content.StartsWith("else", StringComparison.Ordinal)))
                return nodes;

            if (content.StartsWith("for ", StringComparison.Ordinal))
            {
                idx++;
                var (loopVar, collExpr) = ParseForHeader(content);
                var body = ParseNodes(tokens, ref idx, "for");
                ExpectEnd(tokens, ref idx, "for");
                nodes.Add(new ForNode(loopVar, collExpr, body));
                continue;
            }

            if (content.StartsWith("if ", StringComparison.Ordinal))
            {
                idx++;
                var branches = new List<(string?, IReadOnlyList<TemplateNode>)>();
                var firstCond = content.Substring(3).Trim();
                var firstBody = ParseNodes(tokens, ref idx, "if");
                branches.Add((firstCond, firstBody));

                while (idx < tokens.Count && tokens[idx].Kind == TokenKind.Tag)
                {
                    var c = tokens[idx].Content;
                    if (c.StartsWith("else if ", StringComparison.Ordinal))
                    {
                        idx++;
                        var cond = c.Substring(8).Trim();
                        var body = ParseNodes(tokens, ref idx, "if");
                        branches.Add((cond, body));
                    }
                    else if (c == "else")
                    {
                        idx++;
                        var body = ParseNodes(tokens, ref idx, "if");
                        branches.Add((null, body));
                        break;
                    }
                    else break;
                }
                ExpectEnd(tokens, ref idx, "if");
                nodes.Add(new IfNode(branches));
                continue;
            }

            nodes.Add(new SubstNode(content));
            idx++;
        }
        return nodes;
    }

    private static void ExpectEnd(IReadOnlyList<Token> tokens, ref int idx, string owner)
    {
        if (idx >= tokens.Count || tokens[idx].Kind != TokenKind.Tag || tokens[idx].Content != "end")
            throw new InvalidOperationException($"Missing {{{{ end }}}} for {{{{ {owner} ... }}}} block.");
        idx++;
    }

    private static (string LoopVar, string CollectionExpr) ParseForHeader(string content)
    {
        var parts = content.Substring(4).Trim().Split(new[] { " in " }, 2, StringSplitOptions.None);
        if (parts.Length != 2)
            throw new InvalidOperationException($"Bad for-header: '{content}'. Expected 'for VAR in COLLECTION'.");
        return (parts[0].Trim(), parts[1].Trim());
    }

    private enum TokenKind { Text, Tag }
    private sealed record Token(TokenKind Kind, string Content);

    private static IReadOnlyList<Token> Lex(string source)
    {
        var result = new List<Token>();
        var cursor = 0;
        var matches = TagPattern.Matches(source);
        foreach (Match m in matches)
        {
            var preText = source[cursor..m.Index];
            if (m.Groups["trimLeft"].Value == "~" && preText.Length > 0)
                preText = TrimTrailingWhitespaceAndNewline(preText);
            if (preText.Length > 0) result.Add(new Token(TokenKind.Text, preText));
            result.Add(new Token(TokenKind.Tag, m.Groups["content"].Value.Trim()));
            cursor = m.Index + m.Length;
            if (m.Groups["trimRight"].Value == "~" && cursor < source.Length)
                cursor = SkipLeadingWhitespaceAndNewline(source, cursor);
        }
        if (cursor < source.Length) result.Add(new Token(TokenKind.Text, source[cursor..]));
        return result;
    }

    private static string TrimTrailingWhitespaceAndNewline(string text)
    {
        var end = text.Length;
        while (end > 0 && (text[end - 1] == ' ' || text[end - 1] == '\t')) end--;
        if (end > 0 && text[end - 1] == '\n') end--;
        if (end > 0 && text[end - 1] == '\r') end--;
        return text[..end];
    }

    private static int SkipLeadingWhitespaceAndNewline(string source, int cursor)
    {
        while (cursor < source.Length && (source[cursor] == ' ' || source[cursor] == '\t')) cursor++;
        if (cursor < source.Length && source[cursor] == '\r') cursor++;
        if (cursor < source.Length && source[cursor] == '\n') cursor++;
        return cursor;
    }
}
