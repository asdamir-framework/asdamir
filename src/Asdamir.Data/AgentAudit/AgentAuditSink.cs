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
using Asdamir.Core.AgentAudit;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;

namespace Asdamir.Data.AgentAudit;

/// <summary>
/// The client half of the agent action ledger: it accepts records, batches them, and posts them to the control
/// plane's ingest endpoint under an <c>agent-audit-ingest</c> service token. Same layer and shape as the
/// app-log forward sink — bounded channel, background pump, batched POST — but with a materially different
/// failure policy, because the two are not the same kind of data.
///
/// <para><b>A log sink may drop; this one must not.</b> The log forwarder sheds the oldest event under
/// back-pressure and swallows transport errors, and that is right for logs: the local file still has them.
/// An agent action record has no second copy, and its whole purpose is to be producible later as evidence. So
/// every path here ends somewhere accountable: delivered, spooled for retry, or DEAD-LETTERED with a standing
/// alarm. Nothing is dropped quietly.</para>
///
/// <para><b>An unknown status is TRANSIENT, always.</b> The per-item status set is versioned and will grow. If
/// a newer server returns a status this client does not recognise, it retries it to the configured cap and only
/// then dead-letters it. Guessing "permanent" would silently discard a record the server considered
/// retryable — a silent audit gap, which is the one outcome this design refuses.</para>
///
/// <para><b>An outage is not a poison message.</b> The retry cap exists to stop ONE record circulating for
/// ever. A connection failure, a timeout or a 5xx says nothing about any record, so it does not spend that
/// budget: the batch is retried with exponential backoff for as long as the control plane is away. Counting
/// outages against the cap used to turn a one-minute outage into a permanent "AUDIT GAP" dead letter.</para>
///
/// <para><b>Shutdown keeps the batch in flight.</b> A batch the pump has taken off the queue is in neither the
/// queue nor the spool, so it is tracked separately and spooled when the pump stops mid-delivery.</para>
/// </summary>
internal sealed class AgentAuditSink : IAgentActionAuditor, IHostedService, IDisposable
{
    /// <summary>The named <see cref="HttpClient"/> the sink posts through.</summary>
    internal const string HttpClientName = "agent-audit";

    private readonly AgentAuditOptions _options;
    private readonly IHttpClientFactory _httpFactory;
    private readonly ILogger<AgentAuditSink> _logger;
    private readonly AgentAuditSpool _spool;
    private readonly Channel<PendingRecord> _queue;
    private readonly CancellationTokenSource _cts = new();

    private Task? _pump;
    private Task? _alarm;
    private volatile string? _lastFailure;   // only consulted in Throw mode

    // The batch the pump has taken OFF the queue and not yet routed. It is in neither the queue nor the spool,
    // so shutdown has to account for it explicitly; Interlocked.Exchange makes that happen exactly once.
    private PendingRecord[]? _inFlight;
    private bool _outageLogged;              // pump-thread only: one warning per outage, not one per retry

    /// <summary>Creates the sink.</summary>
    /// <param name="options">Client options.</param>
    /// <param name="httpFactory">Factory for the named <c>agent-audit</c> client.</param>
    /// <param name="logger">Logger; dead-letter alarms are raised at Error level through it.</param>
    public AgentAuditSink(
        IOptions<AgentAuditOptions> options, IHttpClientFactory httpFactory, ILogger<AgentAuditSink> logger)
    {
        _options = options.Value;
        _httpFactory = httpFactory;
        _logger = logger;
        _spool = new AgentAuditSpool(_options, logger);

        _queue = Channel.CreateBounded<PendingRecord>(new BoundedChannelOptions(Math.Max(16, _options.QueueCapacity))
        {
            // NOT DropOldest, unlike the log sink: the overflow is handed to the configured failure policy so it
            // is spooled or reported, never discarded behind the caller's back.
            FullMode = BoundedChannelFullMode.DropWrite,
            SingleReader = true,
            SingleWriter = false,
        });
    }

    /// <inheritdoc />
    public Task RecordAsync(AgentActionRecord record, CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(record);
        if (!_options.Enabled) return Task.CompletedTask;

        // Throw mode is fail-CLOSED: once delivery is known to be failing, subsequent audited actions are
        // refused rather than allowed to proceed unrecorded. Because delivery is asynchronous this cannot fail
        // the very record that could not be sent — it fails the NEXT one. That is an honest limitation of an
        // asynchronous sink, and it is why Spool, not Throw, is the default.
        if (_options.OnSinkFailure == AgentAuditOptions.SinkFailureMode.Throw && _lastFailure is not null)
            throw new InvalidOperationException($"The agent action ledger is not accepting records: {_lastFailure}");

        record = Sign(record);

        if (!_queue.Writer.TryWrite(new PendingRecord(record, 0, null)))
            HandleUndeliverable([new SpooledRecord(record, 0)], "queue_full", "the in-memory queue is saturated");

        return Task.CompletedTask;
    }

    /// <summary>
    /// Attaches the agent's signature, if one is configured.
    /// </summary>
    /// <remarks>
    /// <para><b>The signed body uses a ZERO AppId, and that is a rule, not a shortcut.</b> Canonical field 2
    /// is the chain's application id, which the control plane resolves from the ingest token and never from
    /// the payload — so an agent genuinely does not know it. Signing over a value the signer cannot know would
    /// mean either shipping the id into every application's configuration, where one wrong entry makes every
    /// record verify as INVALID (a configuration typo wearing the costume of tampering), or a start-up round
    /// trip for an optional feature. Neither is worth it, because AppId is not something the agent authored:
    /// it is an assignment made ABOUT the agent.</para>
    /// <para><b>Division of labour, so nobody later "fixes" this.</b> The signature carries the CONTENT
    /// guarantee — this agent produced these bytes. The ingest token carries the CONTEXT guarantee — which
    /// application's chain the record may join. A record cannot be moved into another application's chain by
    /// replaying its signature, because the token, not the signature, decides the chain.</para>
    /// <para>Signing failures do NOT drop the record. A sink whose purpose is accountability must not answer a
    /// local key problem by recording nothing: an unsigned record is a smaller loss than a missing one, and
    /// the verifier tells the two apart. The failure is logged as an error rather than swallowed.</para>
    /// </remarks>
    private AgentActionRecord Sign(AgentActionRecord record)
    {
        var signer = _options.Signer;
        if (signer is null) return record;

        try
        {
            var body = AgentActionCanonicalizer.BuildPrefix(SigningBodyAppId, record);
            var signature = signer.Sign(body);
            return record with
            {
                SignatureAlgo = signature.Algorithm,
                Signature = signature.Value,
                SigningKeyId = signature.KeyId,
            };
        }
        catch (Exception ex)
        {
            _logger.LogError(
                ex,
                "Agent action {EventId} could not be signed; recording it UNSIGNED rather than dropping it",
                record.EventId);
            return record;
        }
    }

    /// <summary>
    /// The AppId used when building the SIGNED body: all zeroes, on both the signing and the verifying side.
    /// </summary>
    /// <remarks>
    /// Kept as a named constant so the two sides cannot drift, and so a reader meets the reason rather than a
    /// bare <c>Guid.Empty</c> that looks like an oversight.
    /// </remarks>
    internal static readonly Guid SigningBodyAppId = Guid.Empty;

    /// <inheritdoc />
    public Task StartAsync(CancellationToken cancellationToken)
    {
        if (!_options.Enabled) return Task.CompletedTask;

        // A restart must not silence a standing audit gap, so the count comes back from disk.
        _spool.RestoreDeadLetterCount();

        _pump = Task.Run(PumpAsync, CancellationToken.None);
        _alarm = Task.Run(AlarmAsync, CancellationToken.None);
        return Task.CompletedTask;
    }

    /// <inheritdoc />
    public async Task StopAsync(CancellationToken cancellationToken)
    {
        _queue.Writer.TryComplete();
        await _cts.CancelAsync();

        // The pump goes FIRST: it owns the batch in flight, and it hands that batch to the spool as it
        // unwinds. Draining the queue before it has stopped would race it for records it is about to re-queue.
        foreach (var task in new[] { _pump, _alarm })
        {
            if (task is null) continue;
            try { await task.WaitAsync(TimeSpan.FromSeconds(5), cancellationToken); }
            catch (Exception ex) when (ex is OperationCanceledException or TimeoutException) { /* best effort */ }
        }

        // If the pump did not stop in time it still holds its batch; take it rather than lose it. Should the
        // pump finish its delivery afterwards, the record is merely sent twice — ingest is idempotent.
        PersistInFlight();

        // Whatever is still queued at shutdown goes to the spool rather than evaporating.
        var leftovers = new List<PendingRecord>();
        while (_queue.Reader.TryRead(out var pending)) leftovers.Add(pending);
        PersistAtShutdown(leftovers);
    }

    /// <summary>Spools the batch the pump was holding, if it has not been routed. Safe to call from both sides.</summary>
    private void PersistInFlight()
    {
        var inFlight = Interlocked.Exchange(ref _inFlight, null);
        if (inFlight is { Length: > 0 }) PersistAtShutdown(inFlight);
    }

    /// <summary>
    /// Applies the configured failure policy to records the process is still holding as it stops, and — once
    /// they are safely in a NEW spool file — retires the older spool file they were replayed from, so a restart
    /// during an outage does not replay both copies and double the spool each time.
    /// </summary>
    private void PersistAtShutdown(IReadOnlyList<PendingRecord> records)
    {
        if (records.Count == 0) return;

        var spooled = records.Select(r => new SpooledRecord(r.Record, r.Attempts)).ToList();

        if (_options.OnSinkFailure == AgentAuditOptions.SinkFailureMode.DropAndWarn)
        {
            HandleUndeliverable(spooled, "shutdown", "the process stopped before delivery");
            return;   // a replayed record's original spool file is left alone: dropping is not deleting evidence
        }

        if (!_spool.Write(spooled)) return;   // the write failed: keep the original file as the only copy

        foreach (var record in records) record.Claim?.Complete();
    }

    /// <summary>Replays the spool, then batches and posts for the process's lifetime.</summary>
    private async Task PumpAsync()
    {
        var backoff = TimeSpan.Zero;
        var replayInterval = _options.SpoolReplayInterval < MinSpoolReplayInterval
            ? MinSpoolReplayInterval
            : _options.SpoolReplayInterval;

        ReplaySpool();
        var nextReplay = DateTime.UtcNow + replayInterval;

        try
        {
            while (!_cts.IsCancellationRequested)
            {
                // The spool is not only a startup concern: records spooled during an outage (queue overflow)
                // would otherwise wait for the next restart. Replay only while delivery is healthy, so an
                // outage does not shuffle the same files in and out of the queue.
                if (backoff == TimeSpan.Zero && DateTime.UtcNow >= nextReplay)
                {
                    ReplaySpool();
                    nextReplay = DateTime.UtcNow + replayInterval;
                }

                if (!await WaitForRecordsAsync(nextReplay).ConfigureAwait(false))
                {
                    if (_queue.Reader.Completion.IsCompleted) break;
                    continue;   // the replay timer fired with nothing queued
                }

                var cap = Math.Clamp(_options.BatchSize, 1, ServerBatchCap);
                var batch = new List<PendingRecord>(cap);
                while (batch.Count < cap && _queue.Reader.TryRead(out var item)) batch.Add(item);
                if (batch.Count == 0) continue;

                // From here until the batch is routed it exists nowhere else.
                Volatile.Write(ref _inFlight, batch.ToArray());

                if (backoff > TimeSpan.Zero)
                    await Task.Delay(backoff, _cts.Token).ConfigureAwait(false);

                bool delivered;
                try
                {
                    delivered = await SendAsync(batch).ConfigureAwait(false);
                }
                catch (OperationCanceledException) when (_cts.IsCancellationRequested)
                {
                    throw;
                }
                catch (Exception ex)
                {
                    // An unexpected fault while routing ONE batch must not end delivery for the life of the
                    // process. Give the batch back through the budgeted path (so a batch that faults every time
                    // still terminates in the dead letter) and carry on.
                    _logger.LogError(ex, "Agent-audit: a batch of {Count} record(s) could not be processed; retrying it.",
                        batch.Count);
                    if (Interlocked.Exchange(ref _inFlight, null) is not null)
                        Retry(batch, "client-side processing failure", ex.Message, consumesBudget: true);
                    delivered = false;
                }

                // Exponential backoff on the transport, reset the moment a batch gets through. It starts at the
                // flush interval rather than a hard-coded second, so the sink's pacing is governed by ONE
                // configured value instead of two that can drift apart.
                backoff = delivered
                    ? TimeSpan.Zero
                    : backoff == TimeSpan.Zero
                        ? _options.FlushInterval
                        : TimeSpan.FromMilliseconds(Math.Min(MaxBackoffMs, backoff.TotalMilliseconds * 2));

                await Task.Delay(_options.FlushInterval, _cts.Token).ConfigureAwait(false);
            }
        }
        catch (OperationCanceledException) { /* shutting down */ }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Agent-audit: the delivery pump stopped; records will be spooled on shutdown.");
        }
        finally
        {
            // Cancelled between taking the batch and routing it: it is this method's to save.
            PersistInFlight();
        }
    }

    /// <summary>
    /// Waits until a record is queued or the next spool replay is due. Returns false on the timer (or when the
    /// queue has been completed); shutdown surfaces as <see cref="OperationCanceledException"/>.
    /// </summary>
    private async Task<bool> WaitForRecordsAsync(DateTime nextReplayUtc)
    {
        var wait = nextReplayUtc - DateTime.UtcNow;
        if (wait < TimeSpan.FromMilliseconds(1)) wait = TimeSpan.FromMilliseconds(1);

        using var timer = CancellationTokenSource.CreateLinkedTokenSource(_cts.Token);
        timer.CancelAfter(wait);
        try
        {
            return await _queue.Reader.WaitToReadAsync(timer.Token).ConfigureAwait(false);
        }
        catch (OperationCanceledException) when (!_cts.IsCancellationRequested)
        {
            return false;
        }
    }

    /// <summary>Re-raises the dead-letter alarm on a timer, for as long as anything is dead-lettered.</summary>
    private async Task AlarmAsync()
    {
        try
        {
            using var timer = new PeriodicTimer(_options.DeadLetterWarningInterval);
            while (await timer.WaitForNextTickAsync(_cts.Token).ConfigureAwait(false))
                _spool.RaiseStandingAlarm();
        }
        catch (OperationCanceledException) { /* shutting down */ }
    }

    /// <summary>
    /// Moves spooled records back into the queue — as many whole files as fit in the room the queue has now.
    /// Runs at startup and then periodically; the spool remembers which files are still in flight.
    /// </summary>
    private void ReplaySpool()
    {
        var room = Math.Max(16, _options.QueueCapacity) - _queue.Reader.Count;
        if (room <= 0) return;

        foreach (var (path, records) in _spool.Claim(room))
        {
            var claim = new SpoolClaim(path, records.Count, _spool);
            foreach (var record in records)
            {
                if (!_queue.Writer.TryWrite(new PendingRecord(record.Record, record.Attempts, claim)))
                {
                    // The queue filled underneath us: leave the file for a later pass rather than lose it.
                    claim.Abandon();
                    break;
                }
            }
        }
    }

    /// <summary>
    /// Posts one batch and routes each item. Returns false when the batch did not get through.
    /// </summary>
    private async Task<bool> SendAsync(List<PendingRecord> batch)
    {
        AgentAuditIngestWire.Response? response = null;
        string? transportError = null;
        var outage = false;   // true = the failure says nothing about these records

        try
        {
            var token = AgentAuditServiceToken.Mint(
                _options.SigningKey, _options.Issuer, _options.Audience, _options.AppCode, TimeSpan.FromMinutes(2));

            var client = _httpFactory.CreateClient(HttpClientName);
            using var request = new HttpRequestMessage(HttpMethod.Post, _options.IngestPath)
            {
                Content = JsonContent.Create(new { events = batch.Select(b => b.Record).ToList() }),
            };
            request.Headers.Authorization = new System.Net.Http.Headers.AuthenticationHeaderValue("Bearer", token);

            using var http = await client.SendAsync(request, _cts.Token).ConfigureAwait(false);

            // 200 and 207 both carry per-item results; anything else is a transport-level failure. A 401/403 is
            // TRANSIENT on purpose: it is nearly always a misconfigured or expired key, which is fixed by an
            // operator — discarding the records would turn a configuration mistake into permanent data loss.
            if (http.StatusCode is System.Net.HttpStatusCode.OK or System.Net.HttpStatusCode.MultiStatus)
            {
                response = await http.Content.ReadFromJsonAsync<AgentAuditIngestWire.Response>(_cts.Token)
                    .ConfigureAwait(false);
            }
            else
            {
                transportError = $"ingest returned {(int)http.StatusCode}";
                outage = IsOutageStatus(http.StatusCode);
            }
        }
        catch (OperationCanceledException) when (_cts.IsCancellationRequested)
        {
            throw;
        }
        catch (Exception ex)
        {
            transportError = ex.Message;

            // A connection failure or a timeout (HttpClient reports its own timeout as a cancellation that is
            // NOT ours). Anything else — an unreadable body, a token that could not be minted — is not known to
            // be an outage, so it keeps spending the budget and cannot loop for ever.
            outage = ex is HttpRequestException or OperationCanceledException or IOException
                or System.Net.Sockets.SocketException;
        }

        if (response is null)
        {
            _lastFailure = transportError ?? "the ingest response could not be read";

            if (outage && !_outageLogged)
            {
                _outageLogged = true;
                _logger.LogWarning(
                    "Agent-audit: the ledger ingest is unreachable ({Reason}). Records are being held and "
                    + "retried with backoff; they are NOT dead-lettered for an outage.", _lastFailure);
            }

            Retry(batch, "transport", _lastFailure, consumesBudget: !outage);
            Interlocked.Exchange(ref _inFlight, null);   // routed: it is the queue's or the spool's again
            return false;
        }

        if (_outageLogged)
        {
            _outageLogged = false;
            _logger.LogInformation("Agent-audit: the ledger ingest is reachable again; delivery has resumed.");
        }

        _lastFailure = null;
        Classify(batch, response);
        Interlocked.Exchange(ref _inFlight, null);   // routed: every item is delivered, re-queued or on disk
        return true;
    }

    /// <summary>
    /// Whether a non-success status describes the SERVICE rather than the request: any 5xx, a timeout or
    /// throttle (408/429), and 401/403 — a wrong or expired key that an operator fixes. A 400/404/413 may be
    /// caused by the batch itself, so those keep spending the retry budget.
    /// </summary>
    private static bool IsOutageStatus(System.Net.HttpStatusCode status) =>
        (int)status >= 500
        || status is System.Net.HttpStatusCode.RequestTimeout
            or System.Net.HttpStatusCode.TooManyRequests
            or System.Net.HttpStatusCode.Unauthorized
            or System.Net.HttpStatusCode.Forbidden;

    /// <summary>Routes each item by the server's per-item status.</summary>
    private void Classify(List<PendingRecord> batch, AgentAuditIngestWire.Response response)
    {
        var byEvent = IndexResults(response.Results);
        var permanent = new List<(SpooledRecord, string, string?)>();
        var retry = new List<PendingRecord>();

        foreach (var pending in batch)
        {
            if (!byEvent.TryGetValue(pending.Record.EventId, out var result))
            {
                // The server said nothing about this record. Treat that as transient — assuming it was written
                // would be the same silent gap as assuming it was permanently refused.
                retry.Add(pending);
                continue;
            }

            switch (result.Status)
            {
                case AgentAuditIngestWire.Appended:
                case AgentAuditIngestWire.Duplicate:
                    pending.Claim?.Complete();      // terminal + successful
                    break;

                case AgentAuditIngestWire.Rejected:
                    // PERMANENT. It must NOT go back in the queue: retrying for ever would hide the gap.
                    permanent.Add((new SpooledRecord(pending.Record, pending.Attempts + 1),
                        result.Reason ?? "rejected", result.Detail));
                    pending.Claim?.Complete();
                    break;

                default:
                    // 'failed' AND every status this client version does not know. An unknown status is treated
                    // as transient BY RULE: a newer server may consider it retryable, and discarding it because
                    // this client has never heard of it would be a silent audit gap. The retry cap is what stops
                    // an unknown status circulating for ever.
                    retry.Add(pending);
                    break;
            }
        }

        if (permanent.Count > 0)
            _spool.DeadLetter(permanent, "the ledger permanently refused the record");

        if (retry.Count > 0)
            Retry(retry, "server-side transient failure", null, consumesBudget: true);
    }

    /// <summary>
    /// Indexes the per-item results by EventId, tolerating a repeated id.
    /// </summary>
    /// <remarks>
    /// The same EventId can legitimately come back twice: the same record recorded twice lands in one batch and
    /// the server answers <c>appended</c> for one copy and <c>duplicate</c> for the other. A plain
    /// <c>ToDictionary</c> threw on that and ended the pump for the life of the process. When the answers for
    /// one id differ, the one that says the record IS in the ledger wins — that is a fact about the id, and it
    /// holds for every copy of it.
    /// </remarks>
    internal static Dictionary<Guid, AgentAuditIngestWire.ItemResult> IndexResults(
        IReadOnlyList<AgentAuditIngestWire.ItemResult>? results)
    {
        var byEvent = new Dictionary<Guid, AgentAuditIngestWire.ItemResult>();
        if (results is null) return byEvent;

        foreach (var result in results)
        {
            if (result is null) continue;
            if (!byEvent.TryGetValue(result.EventId, out var existing) || (!IsInLedger(existing) && IsInLedger(result)))
                byEvent[result.EventId] = result;
        }
        return byEvent;

        static bool IsInLedger(AgentAuditIngestWire.ItemResult r) =>
            r.Status is AgentAuditIngestWire.Appended or AgentAuditIngestWire.Duplicate;
    }

    /// <summary>
    /// Re-queues items that may still succeed. When <paramref name="consumesBudget"/> is set, each pass spends
    /// one delivery attempt and an item that has exhausted its budget is dead-lettered; an outage passes
    /// <see langword="false"/>, so it can last as long as it lasts without dead-lettering anything.
    /// </summary>
    private void Retry(List<PendingRecord> items, string reason, string? detail, bool consumesBudget)
    {
        var exhausted = new List<(SpooledRecord, string, string?)>();
        var undeliverable = new List<SpooledRecord>();

        foreach (var item in items)
        {
            var attempts = consumesBudget ? item.Attempts + 1 : item.Attempts;

            if (consumesBudget && attempts >= _options.MaxDeliveryAttempts)
            {
                // The second, and only other, route into the dead letter: not permanent by declaration, but
                // undeliverable in practice. Either way an operator has to know.
                exhausted.Add((new SpooledRecord(item.Record, attempts),
                    $"retry_cap_exhausted ({reason})", detail));
                item.Claim?.Complete();
                continue;
            }

            if (!_queue.Writer.TryWrite(item with { Attempts = attempts }))
            {
                undeliverable.Add(new SpooledRecord(item.Record, attempts));
                item.Claim?.Complete();
            }
        }

        if (exhausted.Count > 0)
            _spool.DeadLetter(exhausted, $"delivery failed {_options.MaxDeliveryAttempts} times");

        if (undeliverable.Count > 0)
            HandleUndeliverable(undeliverable, reason, detail);
    }

    /// <summary>Applies the configured failure policy to records that cannot be held in memory any longer.</summary>
    private void HandleUndeliverable(IReadOnlyList<SpooledRecord> records, string reason, string? detail)
    {
        switch (_options.OnSinkFailure)
        {
            case AgentAuditOptions.SinkFailureMode.Spool:
                _spool.Write(records);
                break;

            case AgentAuditOptions.SinkFailureMode.Throw:
                _lastFailure = detail ?? reason;
                _spool.Write(records);   // fail-closed on the caller, but still keep the evidence
                break;

            case AgentAuditOptions.SinkFailureMode.DropAndWarn:
                _logger.LogWarning(
                    "Agent-audit: {Count} record(s) were DROPPED ({Reason}). OnSinkFailure is DropAndWarn, so "
                    + "this gap is accepted by configuration.", records.Count, reason);
                break;

            default:
                _spool.Write(records);
                break;
        }
    }

    /// <inheritdoc />
    public void Dispose()
    {
        _cts.Cancel();
        _cts.Dispose();
    }

    /// <summary>The server's own per-request cap; sending more only produces transient rejections.</summary>
    private const int ServerBatchCap = 200;

    /// <summary>Ceiling for the transport backoff, so a long outage settles at a steady retry rate.</summary>
    private const double MaxBackoffMs = 60_000;

    /// <summary>Floor for the periodic spool replay, so a zero or negative setting cannot spin the pump.</summary>
    private static readonly TimeSpan MinSpoolReplayInterval = TimeSpan.FromSeconds(1);

    /// <summary>A queued record, its attempt count, and the spool file it came from (if any).</summary>
    private sealed record PendingRecord(AgentActionRecord Record, int Attempts, SpoolClaim? Claim);

    /// <summary>
    /// Tracks how many records from one claimed spool file are still in flight, so the file is deleted only once
    /// every one of them has reached a terminal state. A crash before that leaves the file for the next startup;
    /// re-sending is harmless because ingest is idempotent on EventId.
    /// </summary>
    private sealed class SpoolClaim(string path, int outstanding, AgentAuditSpool spool)
    {
        private int _outstanding = outstanding;

        internal void Complete()
        {
            if (Interlocked.Decrement(ref _outstanding) <= 0) spool.ReleaseClaim(path);
        }

        /// <summary>Leaves the file in place for a later replay, and lets a later pass claim it again.</summary>
        internal void Abandon()
        {
            Interlocked.Exchange(ref _outstanding, int.MaxValue);
            spool.AbandonClaim(path);
        }
    }
}

/// <summary>The ingest wire contract, mirrored from the control plane's response shape.</summary>
internal static class AgentAuditIngestWire
{
    internal const string Appended = "appended";
    internal const string Duplicate = "duplicate";
    internal const string Rejected = "rejected";

    /// <summary>The batch response envelope; <c>ContractVersion</c> is what makes an unknown status detectable.</summary>
    internal sealed record Response(int ContractVersion, IReadOnlyList<ItemResult>? Results);

    /// <summary>One per-item outcome.</summary>
    internal sealed record ItemResult(Guid EventId, long? SeqNo, string Status, string? Reason, string? Detail);
}
