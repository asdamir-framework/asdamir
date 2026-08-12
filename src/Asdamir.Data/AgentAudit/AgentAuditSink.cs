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

        // Whatever is still queued at shutdown goes to the spool rather than evaporating.
        var leftovers = new List<SpooledRecord>();
        while (_queue.Reader.TryRead(out var pending)) leftovers.Add(new SpooledRecord(pending.Record, pending.Attempts));
        if (leftovers.Count > 0) _spool.Write(leftovers);

        foreach (var task in new[] { _pump, _alarm })
        {
            if (task is null) continue;
            try { await task.WaitAsync(TimeSpan.FromSeconds(5), cancellationToken); }
            catch (Exception ex) when (ex is OperationCanceledException or TimeoutException) { /* best effort */ }
        }
    }

    /// <summary>Replays the spool, then batches and posts for the process's lifetime.</summary>
    private async Task PumpAsync()
    {
        ReplaySpool();

        var batch = new List<PendingRecord>(_options.BatchSize);
        var backoff = TimeSpan.Zero;

        try
        {
            while (await _queue.Reader.WaitToReadAsync(_cts.Token).ConfigureAwait(false))
            {
                batch.Clear();
                var cap = Math.Clamp(_options.BatchSize, 1, ServerBatchCap);
                while (batch.Count < cap && _queue.Reader.TryRead(out var item)) batch.Add(item);
                if (batch.Count == 0) continue;

                if (backoff > TimeSpan.Zero)
                    await Task.Delay(backoff, _cts.Token).ConfigureAwait(false);

                var delivered = await SendAsync(batch).ConfigureAwait(false);

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

    private void ReplaySpool()
    {
        foreach (var (path, records) in _spool.Claim())
        {
            var claim = new SpoolClaim(path, records.Count, _spool);
            foreach (var record in records)
            {
                if (!_queue.Writer.TryWrite(new PendingRecord(record.Record, record.Attempts, claim)))
                {
                    // The queue is already full: leave the file for the next startup rather than lose it.
                    claim.Abandon();
                    break;
                }
            }
        }
    }

    /// <summary>Posts one batch and routes each item by its status. Returns false when the transport failed.</summary>
    private async Task<bool> SendAsync(List<PendingRecord> batch)
    {
        AgentAuditIngestWire.Response? response = null;
        string? transportError = null;

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
            }
        }
        catch (OperationCanceledException) when (_cts.IsCancellationRequested)
        {
            throw;
        }
        catch (Exception ex)
        {
            transportError = ex.Message;
        }

        if (response is null)
        {
            _lastFailure = transportError ?? "the ingest response could not be read";
            Retry(batch, "transport", _lastFailure);
            return false;
        }

        _lastFailure = null;
        Classify(batch, response);
        return true;
    }

    /// <summary>Routes each item by the server's per-item status.</summary>
    private void Classify(List<PendingRecord> batch, AgentAuditIngestWire.Response response)
    {
        var byEvent = response.Results?.ToDictionary(r => r.EventId) ?? [];
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
            Retry(retry, "server-side transient failure", null);
    }

    /// <summary>Re-queues items that may still succeed, dead-lettering those that have exhausted their budget.</summary>
    private void Retry(List<PendingRecord> items, string reason, string? detail)
    {
        var exhausted = new List<(SpooledRecord, string, string?)>();
        var undeliverable = new List<SpooledRecord>();

        foreach (var item in items)
        {
            var attempts = item.Attempts + 1;

            if (attempts >= _options.MaxDeliveryAttempts)
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

        /// <summary>Leaves the file in place for a later replay.</summary>
        internal void Abandon() => Interlocked.Exchange(ref _outstanding, int.MaxValue);
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
