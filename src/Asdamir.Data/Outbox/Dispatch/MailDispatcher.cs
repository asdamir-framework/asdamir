// Copyright (C) 2026 Orhan Özşahin — Asdamir.
// Licensed under the GNU Lesser General Public License v3.0. See LICENSE.
// SPDX-License-Identifier: LGPL-3.0-or-later
//
// This file is part of the Asdamir open core. It is free software: you can redistribute it
// and/or modify it under the terms of the GNU Lesser General Public License as published by the
// Free Software Foundation, either version 3 of the License, or (at your option) any later
// version. It is distributed WITHOUT ANY WARRANTY; without even the implied warranty of
// MERCHANTABILITY or FITNESS FOR A PARTICULAR PURPOSE. See the GNU LGPL for more details.

using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;
using MimeKit;
using MailKit.Security;
using System.Text.Json;
using MailKit.Net.Smtp;

namespace Asdamir.Data.Outbox;

/// <summary>
/// Sends email outbox rows via MailKit SMTP. Multi-To / Cc / Bcc + ReplyTo + From override
/// + IsHtml + inline attachments are honored. SMTP host/auth come from <see cref="SmtpOptions"/>.
/// Transient SMTP failures throw — worker schedules retry with backoff. Address-shape /
/// MIME-build errors throw <see cref="PermanentDispatchException"/> so retries don't loop.
/// </summary>
public sealed class MailDispatcher : IOutboxDispatcher
{
    /// <inheritdoc/>
    public byte MessageType => 2; // Email

    private readonly SmtpOptions _smtp;
    private readonly ILogger<MailDispatcher> _logger;

    /// <summary>Creates the SMTP mail dispatcher from <see cref="SmtpOptions"/> + logger.</summary>
    public MailDispatcher(IOptions<SmtpOptions> smtp, ILogger<MailDispatcher> logger)
    {
        _smtp = smtp.Value;
        _logger = logger;
    }

    /// <inheritdoc/>
    public async Task DispatchAsync(ClaimedOutboxMessage message, CancellationToken ct)
    {
        var mime = BuildMimeMessage(message);

        using var client = new SmtpClient
        {
            Timeout = (int)_smtp.Timeout.TotalMilliseconds,
        };

        var secure = ParseSecurity(_smtp.Security);
        await client.ConnectAsync(_smtp.Host, _smtp.Port, secure, ct);
        if (!string.IsNullOrEmpty(_smtp.Username))
            await client.AuthenticateAsync(_smtp.Username, _smtp.Password ?? string.Empty, ct);

        try
        {
            await client.SendAsync(mime, ct);
            _logger.LogInformation("MailDispatcher sent {MessageId} to {Count} recipients (cc={CcCount}, bcc={BccCount})",
                message.Id, mime.To.Count, mime.Cc.Count, mime.Bcc.Count);
        }
        finally
        {
            await client.DisconnectAsync(true, ct);
        }
    }

    private MimeMessage BuildMimeMessage(ClaimedOutboxMessage m)
    {
        if (m.ToAddresses is null || m.ToAddresses.Length == 0)
            throw new PermanentDispatchException($"Outbox {m.Id} has no ToAddresses — refusing to send.");

        var mime = new MimeMessage();

        // From: per-message override, else SMTP default.
        var fromAddr = !string.IsNullOrWhiteSpace(m.FromAddress) ? m.FromAddress : _smtp.DefaultFromAddress;
        var fromName = !string.IsNullOrWhiteSpace(m.FromName) ? m.FromName : _smtp.DefaultFromName;
        mime.From.Add(new MailboxAddress(fromName, fromAddr));

        AddRecipients(mime.To, m.ToAddresses, m.Id, "To");
        if (m.CcAddresses is { Length: > 0 })  AddRecipients(mime.Cc,  m.CcAddresses,  m.Id, "Cc");
        if (m.BccAddresses is { Length: > 0 }) AddRecipients(mime.Bcc, m.BccAddresses, m.Id, "Bcc");

        if (!string.IsNullOrWhiteSpace(m.ReplyTo))
        {
            if (!MailboxAddress.TryParse(m.ReplyTo, out var rt))
                throw new PermanentDispatchException($"Outbox {m.Id} has invalid ReplyTo: {m.ReplyTo}");
            mime.ReplyTo.Add(rt);
        }

        mime.Subject = m.Subject ?? string.Empty;

        var builder = new BodyBuilder();
        if (m.IsHtml) builder.HtmlBody = m.Body; else builder.TextBody = m.Body;

        // Attachments: JSON array of {fileName, contentBase64, contentType, sizeBytes}.
        if (!string.IsNullOrWhiteSpace(m.AttachmentsJson))
        {
            try
            {
                var attachments = JsonSerializer.Deserialize<List<AttachmentSpec>>(m.AttachmentsJson)
                                  ?? new List<AttachmentSpec>();
                foreach (var a in attachments)
                {
                    if (string.IsNullOrWhiteSpace(a.FileName) || string.IsNullOrWhiteSpace(a.ContentBase64)) continue;
                    var bytes = Convert.FromBase64String(a.ContentBase64);
                    var contentType = !string.IsNullOrWhiteSpace(a.ContentType)
                        ? ContentType.Parse(a.ContentType)
                        : new ContentType("application", "octet-stream");
                    builder.Attachments.Add(a.FileName, bytes, contentType);
                }
            }
            catch (Exception ex) when (ex is JsonException or FormatException)
            {
                throw new PermanentDispatchException($"Outbox {m.Id} has malformed attachments JSON.", ex);
            }
        }

        mime.Body = builder.ToMessageBody();
        return mime;
    }

    private static void AddRecipients(InternetAddressList target, string[] addresses, Guid messageId, string field)
    {
        foreach (var a in addresses)
        {
            if (string.IsNullOrWhiteSpace(a)) continue;
            if (!MailboxAddress.TryParse(a, out var parsed))
                throw new PermanentDispatchException($"Outbox {messageId} has invalid {field} address: {a}");
            target.Add(parsed);
        }
        if (field == "To" && target.Count == 0)
            throw new PermanentDispatchException($"Outbox {messageId} ToAddresses parsed to empty list.");
    }

    private static SecureSocketOptions ParseSecurity(string s) => s?.ToLowerInvariant() switch
    {
        "none"          => SecureSocketOptions.None,
        "starttls"      => SecureSocketOptions.StartTls,
        "starttlswhenavailable" => SecureSocketOptions.StartTlsWhenAvailable,
        "sslonconnect"  => SecureSocketOptions.SslOnConnect,
        _               => SecureSocketOptions.Auto,
    };

    private sealed record AttachmentSpec(string FileName, string ContentBase64, string? ContentType, long? SizeBytes);
}
