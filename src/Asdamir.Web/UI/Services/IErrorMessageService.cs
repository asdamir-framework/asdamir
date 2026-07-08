// Copyright (C) 2026 Orhan Özşahin — Asdamir.
// Licensed under the GNU Lesser General Public License v3.0. See LICENSE.
// SPDX-License-Identifier: LGPL-3.0-or-later
//
// This file is part of the Asdamir open core. It is free software: you can redistribute it
// and/or modify it under the terms of the GNU Lesser General Public License as published by the
// Free Software Foundation, either version 3 of the License, or (at your option) any later
// version. It is distributed WITHOUT ANY WARRANTY; without even the implied warranty of
// MERCHANTABILITY or FITNESS FOR A PARTICULAR PURPOSE. See the GNU LGPL for more details.

using System.Net;
using System.Text.Json;

namespace Asdamir.Web.UI.Services;

/// <summary>
/// Maps a failed HTTP response to a short, user-facing message (the "user channel" of the
/// two-channel error model): it prefers the localized <c>ProblemDetails.Title</c> from the
/// response body and otherwise falls back to a message chosen from the status code.
/// </summary>
public interface IErrorMessageService
{
    /// <summary>
    /// Produces a friendly, display-ready message for a failed response.
    /// </summary>
    /// <param name="response">The non-success HTTP response (its status code is the fallback source).</param>
    /// <param name="rawBody">The raw response body, parsed for a <c>ProblemDetails</c> title when present.</param>
    /// <returns>A user-facing message; never the raw status code.</returns>
    string GetFriendlyMessage(HttpResponseMessage response, string? rawBody);
}

/// <summary>
/// Default <see cref="IErrorMessageService"/>: parses <c>ProblemDetails</c> from the body and,
/// failing that, maps the HTTP status code to a localized message.
/// </summary>
public sealed class ErrorMessageService : IErrorMessageService
{
    private readonly ILogger<ErrorMessageService> _logger;

    /// <summary>Creates the service.</summary>
    /// <param name="logger">Logger used to record body-parse fallbacks (at debug level).</param>
    public ErrorMessageService(ILogger<ErrorMessageService> logger)
    {
        _logger = logger;
    }

    /// <inheritdoc/>
    public string GetFriendlyMessage(HttpResponseMessage response, string? rawBody)
    {
        try
        {
            if (!string.IsNullOrWhiteSpace(rawBody))
            {
                var pd = JsonSerializer.Deserialize<ProblemDetailsDto>(rawBody);
                if (!string.IsNullOrWhiteSpace(pd?.title))
                {
                    return MapProblemTitle(pd!.title!, response.StatusCode);
                }
            }
        }
        catch (Exception ex)
        {
            _logger.LogDebug(ex, "[ErrorMessageService] Failed to deserialize ProblemDetails from response body, falling back to status code mapping. StatusCode={StatusCode}", (int)response.StatusCode);
        }

        return MapByStatus(response.StatusCode);
    }

    private static string MapProblemTitle(string title, HttpStatusCode status)
        => title switch
        {
            "Invalid credentials" => "E‑posta veya şifre hatalı.",
            _ => MapByStatus(status)
        };

    private static string MapByStatus(HttpStatusCode status)
        => status switch
        {
            HttpStatusCode.Unauthorized => "E‑posta veya şifre hatalı.",
            HttpStatusCode.Forbidden => "Bu işlemi yapmak için yetkiniz bulunmuyor.",
            HttpStatusCode.BadRequest => "Eksik veya hatalı bilgi gönderildi.",
            (HttpStatusCode)429 => "Çok fazla deneme yapıldı. Lütfen biraz sonra tekrar deneyin.",
            _ => (int)status >= 500
                ? "Sunucuda beklenmeyen bir hata oluştu. Lütfen tekrar deneyin."
                : "İstek başarısız oldu. Lütfen bilgilerinizi kontrol edip tekrar deneyin."
        };

    private sealed class ProblemDetailsDto
    {
        public string? title { get; set; }
        public string? detail { get; set; }
        public int? status { get; set; }
    }
}


