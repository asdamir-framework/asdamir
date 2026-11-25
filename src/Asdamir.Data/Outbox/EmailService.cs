// Copyright (C) 2026 Orhan Özşahin — Asdamir.
// Licensed under the GNU Lesser General Public License v3.0. See LICENSE.
// SPDX-License-Identifier: LGPL-3.0-or-later
//
// This file is part of the Asdamir open core. It is free software: you can redistribute it
// and/or modify it under the terms of the GNU Lesser General Public License as published by the
// Free Software Foundation, either version 3 of the License, or (at your option) any later
// version. It is distributed WITHOUT ANY WARRANTY; without even the implied warranty of
// MERCHANTABILITY or FITNESS FOR A PARTICULAR PURPOSE. See the GNU LGPL for more details.

using Asdamir.Data.Outbox.Options;
using Asdamir.Core.Sanitization;
using MailKit.Net.Smtp;
using MailKit.Security;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;
using MimeKit;

namespace Asdamir.Data.Outbox.Services;

/// <summary>
/// Email service using MailKit + <see cref="EmailOptions"/> bound at startup.
///
/// Audit fix (CRITICAL):
///  - v1 read every SMTP setting (host, port, username, password, from-name) from
///    <c>IAppConfigurationService.GetValueAsync</c> on EVERY send. A misconfigured row
///    or a DB outage produced empty credentials (`smtpPass ?? string.Empty`) sent over
///    the wire, where some relays accept unauthenticated connections silently. Plus
///    6 DB round-trips per send.
///  - v2 binds <see cref="EmailOptions"/> once at startup. Missing required values
///    surface at boot via <see cref="EmailOptionsValidator"/>, not mid-flight.
/// </summary>
public class EmailService : IEmailService
{
    private readonly EmailOptions _opt;
    private readonly ILogger<EmailService> _logger;

    /// <summary>Creates the SMTP-backed email service from <c>EmailOptions</c> + logger.</summary>
    public EmailService(IOptions<EmailOptions> options, ILogger<EmailService> logger)
    {
        _opt = options.Value;
        _logger = logger;
    }

    /// <summary>Creates the SMTP client for one send. A seam for tests; production uses MailKit's client.</summary>
    internal Func<ISmtpClient> SmtpClientFactory { get; set; } = static () => new SmtpClient();

    /// <inheritdoc/>
    public Task SendAsync(string to, string subject, string htmlBody, CancellationToken cancellationToken = default)
        => SendInternalAsync(to, subject, htmlBody, isHtml: true, cancellationToken);

    /// <inheritdoc/>
    public Task SendTextAsync(string to, string subject, string textBody, CancellationToken cancellationToken = default)
        => SendInternalAsync(to, subject, textBody, isHtml: false, cancellationToken);

    /// <inheritdoc/>
    /// <remarks>
    /// Every recipient is attempted; a failure for one does not stop the others. If ANY send failed, a
    /// <see cref="BulkEmailSendException"/> is thrown once all have been attempted — it names the recipients
    /// that failed so a caller can retry those alone (retrying the whole list would mail the rest twice).
    /// Before this, the method completed normally even when every single send had failed.
    /// </remarks>
    public async Task SendBulkAsync(IEnumerable<string> to, string subject, string htmlBody, CancellationToken cancellationToken = default)
    {
        var recipients = to.ToList();
        _logger.LogInformation("Sending bulk email to {Count} recipients with subject '{Subject}'", recipients.Count, subject);

        // Bounded concurrency: a single bulk run shouldn't open hundreds of SMTP
        // connections at once. 4 is a reasonable default; tune later via options.
        const int maxConcurrency = 4;
        using var semaphore = new SemaphoreSlim(maxConcurrency);
        var failed = new System.Collections.Concurrent.ConcurrentBag<string>();

        var tasks = recipients.Select(async recipient =>
        {
            await semaphore.WaitAsync(cancellationToken);
            try
            {
                await SendInternalAsync(recipient, subject, htmlBody, isHtml: true, cancellationToken);
            }
            catch (OperationCanceledException)
            {
                throw;   // cancellation is not a per-recipient failure
            }
            catch (Exception ex)
            {
                failed.Add(recipient);
                _logger.LogError(ex, "Bulk-email send failed for {Recipient}", RecipientMasker.Mask(recipient));
            }
            finally
            {
                semaphore.Release();
            }
        });

        await Task.WhenAll(tasks);
        var failures = failed.Count;
        _logger.LogInformation("Bulk email finished: {Sent} sent, {Failed} failed of {Total}",
            recipients.Count - failures, failures, recipients.Count);

        if (failures > 0)
            throw new BulkEmailSendException(failed.ToArray(), recipients.Count);
    }

    private async Task SendInternalAsync(string to, string subject, string body, bool isHtml, CancellationToken cancellationToken)
    {
        try
        {
            var message = new MimeMessage();
            message.From.Add(new MailboxAddress(_opt.FromName, _opt.FromEmail));
            message.To.Add(new MailboxAddress(string.Empty, to));
            message.Subject = subject;

            var builder = new BodyBuilder();
            if (isHtml) builder.HtmlBody = body;
            else builder.TextBody = body;
            message.Body = builder.ToMessageBody();

            using var client = SmtpClientFactory();
            client.Timeout = (int)TimeSpan.FromSeconds(_opt.TimeoutSeconds).TotalMilliseconds;
            await client.ConnectAsync(
                _opt.SmtpHost,
                _opt.SmtpPort,
                _opt.EnableSsl ? SecureSocketOptions.StartTls : SecureSocketOptions.None,
                cancellationToken);

            if (!string.IsNullOrEmpty(_opt.SmtpUsername))
            {
                await client.AuthenticateAsync(_opt.SmtpUsername, _opt.SmtpPassword ?? string.Empty, cancellationToken);
            }

            await client.SendAsync(message, cancellationToken);

            // The server has accepted the message; a failure while hanging up cannot un-send it and must not
            // be reported as "Failed to send email" (a caller that retries would deliver a duplicate).
            try
            {
                await client.DisconnectAsync(quit: true, CancellationToken.None);
            }
            catch (Exception ex)
            {
                _logger.LogWarning(ex, "The SMTP session could not be closed cleanly after a successful send");
            }

            // Audit fix: log a masked recipient instead of the raw address. Logs ship
            // to centralized stores; full addresses are unnecessary PII for a successful
            // send. We keep the first character + domain for debugging.
            _logger.LogInformation("Email sent to {To} (html={IsHtml})", RecipientMasker.Mask(to), isHtml);
        }
        catch (OperationCanceledException)
        {
            throw;
        }
        catch (Exception ex)
        {
            // Audit fix: ex.Message from MailKit can contain SMTP server hostnames,
            // ports, and auth failure reasons. We log the full exception server-side
            // (Serilog captures it as structured data) but the rethrown exception's
            // Message is now generic — callers who need details can walk InnerException.
            _logger.LogError(ex, "Failed to send email. To: {To}, Subject: {Subject}", RecipientMasker.Mask(to), subject);
            throw new InvalidOperationException("Failed to send email.", ex);
        }
    }

    // Audit fix: the private static MaskRecipient that used to live here is now
    // Asdamir.Core.Sanitization.RecipientMasker so it can be regression-tested AND
    // reused by other services (audit middleware, SMS sender) that need to log a
    // recipient without leaking PII.
}

/// <summary>
/// Thrown by <see cref="IEmailService.SendBulkAsync"/> when one or more recipients could not be mailed. Every
/// recipient has been attempted by the time it is thrown.
/// </summary>
public sealed class BulkEmailSendException : InvalidOperationException
{
    /// <summary>Creates the exception.</summary>
    /// <param name="failedRecipients">The addresses whose send failed.</param>
    /// <param name="totalRecipients">How many recipients the bulk send was asked to mail.</param>
    public BulkEmailSendException(IReadOnlyList<string> failedRecipients, int totalRecipients)
        : base($"Bulk email failed for {failedRecipients.Count} of {totalRecipients} recipient(s).")
    {
        FailedRecipients = failedRecipients;
        TotalRecipients = totalRecipients;
    }

    /// <summary>
    /// The addresses whose send failed — retry these, not the whole list. Deliberately NOT part of
    /// <see cref="Exception.Message"/>, so logging the exception does not write addresses to the log.
    /// </summary>
    public IReadOnlyList<string> FailedRecipients { get; }

    /// <summary>How many recipients the bulk send was asked to mail.</summary>
    public int TotalRecipients { get; }

    /// <summary>True when not a single recipient was mailed.</summary>
    public bool AllFailed => FailedRecipients.Count == TotalRecipients;
}

/// <summary>
/// Startup-time validator: refuses to boot when SMTP host / from address is missing.
/// Register with <c>services.AddSingleton&lt;IValidateOptions&lt;EmailOptions&gt;, EmailOptionsValidator&gt;()</c>.
/// </summary>
public sealed class EmailOptionsValidator : IValidateOptions<EmailOptions>
{
    /// <inheritdoc/>
    public ValidateOptionsResult Validate(string? name, EmailOptions options)
    {
        var failures = new List<string>();
        if (string.IsNullOrWhiteSpace(options.SmtpHost) || options.SmtpHost == "localhost")
            failures.Add("EmailOptions.SmtpHost must be configured (env var Email__SmtpHost).");
        if (options.SmtpPort <= 0)
            failures.Add("EmailOptions.SmtpPort must be a positive integer.");
        if (string.IsNullOrWhiteSpace(options.FromEmail) || options.FromEmail == "noreply@example.com")
            failures.Add("EmailOptions.FromEmail must be configured for the deployment.");
        if (!string.IsNullOrWhiteSpace(options.SmtpUsername) && string.IsNullOrEmpty(options.SmtpPassword))
            failures.Add("EmailOptions.SmtpPassword required when SmtpUsername is set.");

        return failures.Count == 0
            ? ValidateOptionsResult.Success
            : ValidateOptionsResult.Fail(failures);
    }
}
