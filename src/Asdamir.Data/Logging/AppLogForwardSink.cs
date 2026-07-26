// Copyright (C) 2026 Orhan Özşahin — Asdamir.
// Licensed under the GNU Lesser General Public License v3.0. See LICENSE.
// SPDX-License-Identifier: LGPL-3.0-or-later
//
// This file is part of the Asdamir open core. It is free software: you can redistribute it
// and/or modify it under the terms of the GNU Lesser General Public License as published by the
// Free Software Foundation, either version 3 of the License, or (at your option) any later
// version. It is distributed WITHOUT ANY WARRANTY; without even the implied warranty of
// MERCHANTABILITY or FITNESS FOR A PARTICULAR PURPOSE. See the GNU LGPL for more details.

using System.Net.Http.Json;
using System.Threading.Channels;
using Asdamir.Core.ErrorHandling.Logging;
using Serilog.Core;
using Serilog.Events;

namespace Asdamir.Data.Logging;

/// <summary>
/// Serilog sink that FORWARDS a generated app's Warning+ events to AppManagement's central log-ingest
/// endpoint (<c>POST /api/admin/applogs/ingest</c>), so every managed app's failures finally reach the
/// operator's ErrorMonitoring — the third sink alongside Console + File. The generated Gateway can't (and
/// mustn't) write to AsdamirVault directly (layered/CENTRAL rule); it forwards over HTTP, authenticated by a
/// short-lived <c>app-log</c> service token it mints with the shared signing key (<see cref="AppLogServiceToken"/>),
/// and AppManagement resolves the AppId from that token — never from the payload.
///
/// <para><b>Resilience (fail-SAFE, never fail-open):</b> events are enqueued to a bounded channel and a single
/// background loop batches + POSTs them. If AppManagement is slow or unreachable the app never blocks and no
/// request is delayed — the forward simply fails and is swallowed (the local File sink still has every line).
/// Under a flood the queue drops the OLDEST entry and warns ONCE (to Console, never back through Serilog).
/// <b>No-loop:</b> the sink never forwards its OWN transport logs (the forward <c>HttpClient</c>'s
/// <c>System.Net.Http.HttpClient.applog-forward.*</c> categories are skipped) or events it emitted itself, so a
/// failing POST can't cascade into a log storm.</para>
/// </summary>
public sealed class AppLogForwardSink : ILogEventSink, IDisposable
{
    /// <summary>The named <see cref="HttpClient"/> the sink posts through (also its skip-prefix for no-loop).</summary>
    public const string HttpClientName = "applog-forward";
    private const string ForwardLogCategory = "System.Net.Http.HttpClient." + HttpClientName;

    private readonly AppLogForwardOptions _opts;
    private readonly IHttpClientFactory _httpFactory;
    private readonly Channel<ForwardEntry> _queue;
    private readonly CancellationTokenSource _cts = new();
    private readonly Task _pump;
    private int _warnedDrop; // 0/1 latch: warn once when the queue overflows

    /// <summary>Creates the sink over its options and an <see cref="IHttpClientFactory"/> (named client <c>applog-forward</c>).</summary>
    public AppLogForwardSink(AppLogForwardOptions options, IHttpClientFactory httpFactory)
    {
        _opts = options;
        _httpFactory = httpFactory;
        _queue = Channel.CreateBounded<ForwardEntry>(new BoundedChannelOptions(Math.Max(16, options.QueueCapacity))
        {
            FullMode = BoundedChannelFullMode.DropOldest, // shed the oldest under back-pressure, never block the app
            SingleReader = true,
            SingleWriter = false,
        });
        _pump = Task.Run(PumpAsync);
    }

    /// <inheritdoc />
    public void Emit(LogEvent logEvent)
    {
        if (logEvent.Level < LogEventLevel.Warning) return; // error-focused sink; Console/File keep everything

        // No-loop: never forward the forward-transport's OWN logs (a failed POST logs a Warning here).
        if (logEvent.Properties.TryGetValue("SourceContext", out var sc)
            && sc.ToString().Trim('"').StartsWith(ForwardLogCategory, StringComparison.Ordinal))
            return;

        var entry = Map(logEvent);
        if (!_queue.Writer.TryWrite(entry) && Interlocked.Exchange(ref _warnedDrop, 1) == 0)
        {
            // DropOldest means TryWrite basically always succeeds; this guards the theoretical closed-channel case.
            Console.Error.WriteLine("[AppLogForwardSink] log-forward queue is saturated — dropping oldest events.");
        }
    }

    private async Task PumpAsync()
    {
        var reader = _queue.Reader;
        var batch = new List<ForwardEntry>(_opts.BatchSize);
        try
        {
            while (await reader.WaitToReadAsync(_cts.Token).ConfigureAwait(false))
            {
                batch.Clear();
                while (batch.Count < _opts.BatchSize && reader.TryRead(out var e)) batch.Add(e);
                if (batch.Count == 0) continue;

                await SendAsync(batch).ConfigureAwait(false);

                // Small coalescing window so a burst rides one POST instead of many.
                try { await Task.Delay(_opts.FlushInterval, _cts.Token).ConfigureAwait(false); }
                catch (OperationCanceledException) { break; }
            }
        }
        catch (OperationCanceledException) { /* shutting down */ }
        catch (Exception ex) { Console.Error.WriteLine($"[AppLogForwardSink] pump stopped: {ex.Message}"); }
    }

    private async Task SendAsync(IReadOnlyList<ForwardEntry> batch)
    {
        // FAIL-SAFE: any transport error is swallowed — the app must not slow or crash because central
        // logging is down; the local File sink already captured every line.
        try
        {
            var token = AppLogServiceToken.Mint(_opts.SigningKey, _opts.Issuer, _opts.Audience, _opts.AppCode,
                TimeSpan.FromMinutes(2));
            var client = _httpFactory.CreateClient(HttpClientName);
            using var req = new HttpRequestMessage(HttpMethod.Post, _opts.IngestPath)
            {
                Content = JsonContent.Create(new { entries = batch }),
            };
            req.Headers.Authorization = new System.Net.Http.Headers.AuthenticationHeaderValue("Bearer", token);
            using var resp = await client.SendAsync(req, _cts.Token).ConfigureAwait(false);
            // A non-success response is logged once to Console (NOT Serilog — no loop) and otherwise ignored.
            if (!resp.IsSuccessStatusCode && Interlocked.Exchange(ref _warnedDrop, 1) == 0)
                Console.Error.WriteLine($"[AppLogForwardSink] ingest returned {(int)resp.StatusCode}; central logs may be incomplete.");
        }
        catch (OperationCanceledException) { /* shutdown */ }
        catch (Exception ex)
        {
            if (Interlocked.Exchange(ref _warnedDrop, 1) == 0)
                Console.Error.WriteLine($"[AppLogForwardSink] forward failed (logs stay in the local File): {ex.Message}");
        }
    }

    private static ForwardEntry Map(LogEvent e)
    {
        var source = e.Properties.TryGetValue("SourceContext", out var sc) ? sc.ToString().Trim('"') : null;
        var errorKey = e.Properties.TryGetValue("ErrorKey", out var ek) ? ek.ToString().Trim('"') : null;
        return new ForwardEntry(
            e.Level.ToString(), e.RenderMessage(), source, errorKey,
            e.Exception?.GetType().FullName, e.Exception?.Message, e.Exception?.StackTrace,
            e.Exception?.InnerException?.ToString(), null);
    }

    /// <inheritdoc />
    public void Dispose()
    {
        _queue.Writer.TryComplete();
        _cts.Cancel();
        try { _pump.Wait(TimeSpan.FromSeconds(2)); } catch { /* best-effort flush on shutdown */ }
        _cts.Dispose();
    }

    // The wire shape MUST match AppManagement's AppLogWriteEntry (Level/Message/Source/ErrorKey/Exception*).
    private sealed record ForwardEntry(
        string Level, string Message, string? Source, string? ErrorKey,
        string? ExceptionType, string? ExceptionMessage, string? ExceptionStackTrace,
        string? ExceptionInnerException, string? UserLanguage);
}

/// <summary>
/// Configuration for <see cref="AppLogForwardSink"/> — read from the generated Gateway's config
/// (<c>AdminConsole:BaseUrl</c>, <c>Jwt:*</c>, <c>App:Code</c>, and the opt-out flag). Central forwarding is
/// ON by default in commercial mode; it is a no-op in free mode (there is no control plane to forward to).
/// </summary>
public sealed class AppLogForwardOptions
{
    /// <summary>The ingest endpoint path, relative to the <c>applog-forward</c> client's base address.</summary>
    public string IngestPath { get; init; } = "api/admin/applogs/ingest";
    /// <summary>The shared HMAC signing key (<c>Jwt:Key</c>) used to mint the app-log service token.</summary>
    public required string SigningKey { get; init; }
    /// <summary>The token issuer (<c>Jwt:Issuer</c>) — must match AppManagement.</summary>
    public string? Issuer { get; init; }
    /// <summary>The token audience (<c>Jwt:Audience</c>) — the managed-app audience.</summary>
    public string? Audience { get; init; }
    /// <summary>This app's registered code (<c>App:Code</c>); AppManagement resolves the AppId from it.</summary>
    public required string AppCode { get; init; }
    /// <summary>Max events per POST.</summary>
    public int BatchSize { get; init; } = 50;
    /// <summary>Coalescing window between batches (a burst rides one POST).</summary>
    public TimeSpan FlushInterval { get; init; } = TimeSpan.FromSeconds(2);
    /// <summary>Bounded queue capacity; overflow drops the OLDEST entry (never blocks the app).</summary>
    public int QueueCapacity { get; init; } = 1000;
}
