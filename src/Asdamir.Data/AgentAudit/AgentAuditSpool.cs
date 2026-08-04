// Copyright (C) 2026 Orhan Özşahin — Asdamir.
// Licensed under the GNU Lesser General Public License v3.0. See LICENSE.
// SPDX-License-Identifier: LGPL-3.0-or-later
//
// This file is part of the Asdamir open core. It is free software: you can redistribute it
// and/or modify it under the terms of the GNU Lesser General Public License as published by the
// Free Software Foundation, either version 3 of the License, or (at your option) any later
// version. It is distributed WITHOUT ANY WARRANTY; without even the implied warranty of
// MERCHANTABILITY or FITNESS FOR A PARTICULAR PURPOSE. See the GNU LGPL for more details.

using System.Globalization;
using System.Text;
using System.Text.Json;
using Asdamir.Core.AgentAudit;
using Microsoft.Extensions.Logging;

namespace Asdamir.Data.AgentAudit;

/// <summary>One spooled record and how many delivery attempts it has already survived.</summary>
/// <param name="Record">The action to record.</param>
/// <param name="Attempts">Delivery attempts made so far.</param>
internal sealed record SpooledRecord(AgentActionRecord Record, int Attempts);

/// <summary>
/// The on-disk side of the agent-audit client: a bounded NDJSON spool for records that could not be delivered,
/// and a SEPARATE dead-letter file for records the server said can never be written.
///
/// <para><b>Why two files and not one.</b> They mean opposite things. A spooled record is still going to be
/// delivered — it is waiting. A dead-lettered record never will be: it is a HOLE in the audit trail, and it
/// needs a human. Mixing them would let a permanent rejection sit in a retry queue for ever, looking busy,
/// which is precisely how an audit gap goes unnoticed.</para>
///
/// <para><b>The dead-letter alarm cannot be silenced by a restart.</b> The count is re-derived from the file on
/// startup, so the recurring alarm resumes until somebody actually deals with the file. That is deliberate: a
/// warning that a restart clears is a warning that will be cleared by a restart.</para>
/// </summary>
internal sealed class AgentAuditSpool
{
    private const string SpoolExtension = ".ndjson";
    private const string ReplayingExtension = ".ndjson.replaying";
    private const string DeadLetterFileName = "dead-letter.ndjson";

    private readonly AgentAuditOptions _options;
    private readonly ILogger _logger;
    private readonly object _gate = new();
    private int _warnedOverflow;   // 0/1 latch: the spool-overflow warning is genuinely once-per-process
    private long _deadLetterCount;

    /// <summary>Creates the spool over its directory.</summary>
    /// <param name="options">Client options (directory, size cap).</param>
    /// <param name="logger">Logger for overflow and dead-letter alarms.</param>
    internal AgentAuditSpool(AgentAuditOptions options, ILogger logger)
    {
        _options = options;
        _logger = logger;
    }

    /// <summary>How many records are currently dead-lettered. Non-zero keeps the alarm sounding.</summary>
    internal long DeadLetterCount => Interlocked.Read(ref _deadLetterCount);

    /// <summary>The dead-letter file's full path, named in every alarm so an operator can go straight to it.</summary>
    internal string DeadLetterPath => Path.Combine(_options.SpoolDirectory, DeadLetterFileName);

    /// <summary>
    /// Re-derives the dead-letter count from disk. Called at startup so a restart cannot silence the alarm.
    /// </summary>
    internal void RestoreDeadLetterCount()
    {
        try
        {
            if (!File.Exists(DeadLetterPath)) return;
            var lines = File.ReadLines(DeadLetterPath).Count(l => !string.IsNullOrWhiteSpace(l));
            Interlocked.Exchange(ref _deadLetterCount, lines);
        }
        catch (Exception ex) when (ex is IOException or UnauthorizedAccessException)
        {
            _logger.LogWarning(ex, "Agent-audit: the dead-letter file could not be read at {Path}", DeadLetterPath);
        }
    }

    /// <summary>Appends records to a fresh spool file, enforcing the size cap.</summary>
    /// <param name="records">The undelivered records.</param>
    internal void Write(IReadOnlyList<SpooledRecord> records)
    {
        if (records.Count == 0) return;

        lock (_gate)
        {
            try
            {
                Directory.CreateDirectory(_options.SpoolDirectory);
                EnforceCap();

                var name = string.Create(CultureInfo.InvariantCulture,
                    $"agent-audit-{DateTime.UtcNow:yyyyMMdd-HHmmss-fffffff}{SpoolExtension}");
                var builder = new StringBuilder();
                foreach (var record in records)
                    builder.Append(JsonSerializer.Serialize(record)).Append('\n');

                File.AppendAllText(Path.Combine(_options.SpoolDirectory, name), builder.ToString(), Encoding.UTF8);
            }
            catch (Exception ex) when (ex is IOException or UnauthorizedAccessException)
            {
                // The spool is the last line of defence; if even it fails, say so loudly rather than pretend.
                _logger.LogError(ex,
                    "Agent-audit: {Count} record(s) could NOT be spooled to {Directory} and are lost. "
                    + "This is an audit gap.", records.Count, _options.SpoolDirectory);
            }
        }
    }

    /// <summary>
    /// Drops the OLDEST spool files until the directory is back under its cap. This is the one place the client
    /// can lose a record it accepted, so it is reported — but only ONCE per process, because it is a
    /// steady-state condition rather than a new event each time.
    /// </summary>
    private void EnforceCap()
    {
        var files = new DirectoryInfo(_options.SpoolDirectory)
            .GetFiles("agent-audit-*" + SpoolExtension)
            .OrderBy(f => f.CreationTimeUtc)
            .ToList();

        var total = files.Sum(f => f.Length);
        while (total > _options.SpoolMaxBytes && files.Count > 0)
        {
            var oldest = files[0];
            files.RemoveAt(0);
            total -= oldest.Length;

            if (Interlocked.Exchange(ref _warnedOverflow, 1) == 0)
            {
                _logger.LogError(
                    "Agent-audit: the spool exceeded {Cap} bytes at {Directory}; the oldest file {File} was "
                    + "dropped. Records are being LOST — restore ingest connectivity or raise the cap.",
                    _options.SpoolMaxBytes, _options.SpoolDirectory, oldest.Name);
            }

            try { oldest.Delete(); }
            catch (Exception ex) when (ex is IOException or UnauthorizedAccessException) { /* next pass retries */ }
        }
    }

    /// <summary>
    /// Claims every spool file for replay by renaming it, then returns its records.
    ///
    /// <para>Renaming first is what makes a crash mid-replay safe: a <c>.replaying</c> file is picked up again
    /// by the next startup, and a re-sent record is harmless because ingest is idempotent on EventId. The file
    /// is only deleted once every record it held has reached a terminal state, which is what "successfully-sent
    /// lines are removed" means in practice — anything still undelivered is written to a NEW spool file.</para>
    /// </summary>
    /// <returns>One entry per claimed file: its path and the records it held.</returns>
    internal IReadOnlyList<(string Path, IReadOnlyList<SpooledRecord> Records)> Claim()
    {
        lock (_gate)
        {
            var claimed = new List<(string, IReadOnlyList<SpooledRecord>)>();
            try
            {
                if (!Directory.Exists(_options.SpoolDirectory)) return claimed;

                // Fresh spool files AND anything a previous process died in the middle of replaying.
                var pending = Directory.GetFiles(_options.SpoolDirectory, "agent-audit-*" + SpoolExtension)
                    .Concat(Directory.GetFiles(_options.SpoolDirectory, "agent-audit-*" + ReplayingExtension))
                    .OrderBy(p => p, StringComparer.Ordinal)
                    .ToList();

                foreach (var path in pending)
                {
                    var target = path.EndsWith(ReplayingExtension, StringComparison.Ordinal)
                        ? path
                        : path[..^SpoolExtension.Length] + ReplayingExtension;

                    if (!string.Equals(path, target, StringComparison.Ordinal))
                        File.Move(path, target, overwrite: true);

                    var records = new List<SpooledRecord>();
                    foreach (var line in File.ReadLines(target))
                    {
                        if (string.IsNullOrWhiteSpace(line)) continue;
                        try
                        {
                            var record = JsonSerializer.Deserialize<SpooledRecord>(line);
                            if (record is not null) records.Add(record);
                        }
                        catch (JsonException ex)
                        {
                            _logger.LogError(ex,
                                "Agent-audit: a spooled record in {File} could not be read back and is lost.", target);
                        }
                    }

                    if (records.Count == 0) { TryDelete(target); continue; }
                    claimed.Add((target, records));
                }
            }
            catch (Exception ex) when (ex is IOException or UnauthorizedAccessException)
            {
                _logger.LogWarning(ex, "Agent-audit: the spool at {Directory} could not be replayed",
                    _options.SpoolDirectory);
            }
            return claimed;
        }
    }

    /// <summary>Deletes a claimed spool file once every record it held has reached a terminal state.</summary>
    /// <param name="path">The claimed file's path.</param>
    internal void ReleaseClaim(string path)
    {
        lock (_gate) TryDelete(path);
    }

    /// <summary>
    /// Dead-letters records the server permanently refused, or that exhausted their retry budget, and raises the
    /// alarm. This is an AUDIT GAP, so the log is at Error — which, in a managed application, is the level the
    /// framework forwards to the control plane's central error monitoring.
    /// </summary>
    /// <param name="records">The undeliverable records.</param>
    /// <param name="reason">Why they can never be delivered.</param>
    internal void DeadLetter(IReadOnlyList<(SpooledRecord Record, string Reason, string? Detail)> records, string reason)
    {
        if (records.Count == 0) return;

        lock (_gate)
        {
            try
            {
                Directory.CreateDirectory(_options.SpoolDirectory);
                var builder = new StringBuilder();
                foreach (var (record, itemReason, detail) in records)
                {
                    builder.Append(JsonSerializer.Serialize(new
                    {
                        deadLetteredAtUtc = DateTime.UtcNow,
                        reason = itemReason,
                        detail,
                        attempts = record.Attempts,
                        record = record.Record,
                    })).Append('\n');
                }
                File.AppendAllText(DeadLetterPath, builder.ToString(), Encoding.UTF8);
            }
            catch (Exception ex) when (ex is IOException or UnauthorizedAccessException)
            {
                _logger.LogError(ex,
                    "Agent-audit: {Count} record(s) could not even be dead-lettered to {Path}.",
                    records.Count, DeadLetterPath);
            }
        }

        Interlocked.Add(ref _deadLetterCount, records.Count);

        _logger.LogError(
            "AGENT AUDIT GAP: {Count} agent action record(s) were permanently rejected ({Reason}) and written to "
            + "{Path}. They are NOT in the ledger and never will be without action. Total dead-lettered: {Total}.",
            records.Count, reason, DeadLetterPath, DeadLetterCount);
    }

    /// <summary>
    /// Re-raises the dead-letter alarm. Called on a timer for as long as anything is dead-lettered — this is
    /// deliberately NOT a warn-once: a single line that scrolls away is exactly how an audit gap is missed.
    /// </summary>
    internal void RaiseStandingAlarm()
    {
        var count = DeadLetterCount;
        if (count == 0) return;

        _logger.LogError(
            "AGENT AUDIT GAP UNRESOLVED: {Total} agent action record(s) remain dead-lettered in {Path}. "
            + "The ledger is INCOMPLETE for this application. This alarm repeats every {Interval} and survives a "
            + "restart; it stops only when the file is dealt with and removed.",
            count, DeadLetterPath, _options.DeadLetterWarningInterval);
    }

    private void TryDelete(string path)
    {
        try { if (File.Exists(path)) File.Delete(path); }
        catch (Exception ex) when (ex is IOException or UnauthorizedAccessException)
        {
            _logger.LogWarning(ex, "Agent-audit: the spool file {File} could not be removed", path);
        }
    }
}
