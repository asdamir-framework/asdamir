// Copyright (C) 2026 Orhan Özşahin — Asdamir.
// Licensed under the GNU Lesser General Public License v3.0. See LICENSE.
// SPDX-License-Identifier: LGPL-3.0-or-later
//
// This file is part of the Asdamir open core. It is free software: you can redistribute it
// and/or modify it under the terms of the GNU Lesser General Public License as published by the
// Free Software Foundation, either version 3 of the License, or (at your option) any later
// version. It is distributed WITHOUT ANY WARRANTY; without even the implied warranty of
// MERCHANTABILITY or FITNESS FOR A PARTICULAR PURPOSE. See the GNU LGPL for more details.

using Asdamir.Core.ErrorHandling.Abstractions;
using Microsoft.Extensions.Logging;
using System.Text.Json;

namespace Asdamir.Core.ErrorHandling.Http;

/// <summary>
/// Translates internal error codes to user-language messages by calling a
/// configured REST endpoint.
///
/// Audit fix: v1 allocated <c>new HttpClient()</c> on every call to <see cref="Translate"/>
/// and ALSO a leaked, unused one in the constructor. Under load that exhausts
/// ephemeral ports (TIME_WAIT) and the translation calls start failing with
/// "Only one usage of each socket address" — which then masks the original
/// error the user was trying to read. Now backed by <see cref="IHttpClientFactory"/>;
/// the factory's <c>PrimaryHttpMessageHandler</c> rotates pooled handlers
/// transparently and the class is no longer <see cref="IDisposable"/>.
/// </summary>
public class RestErrorTranslator : IErrorTranslator
{
    /// <summary>
    /// Name of the <see cref="IHttpClientFactory"/> client this translator resolves — register a client
    /// under this name to configure the handler/base address used for translation calls.
    /// </summary>
    public const string HttpClientName = "ErrorTranslation";

    private readonly string _apiBaseUrl;
    private readonly IHttpClientFactory _httpClientFactory;
    private readonly ILogger<RestErrorTranslator> _logger;

    /// <summary>
    /// Captures the translation endpoint's base address and a pooled-client factory (never a raw
    /// <c>new HttpClient()</c>), avoiding the socket exhaustion that previously masked the real error.
    /// </summary>
    /// <param name="apiBaseUrl">Base URL of the error-translation REST API.</param>
    /// <param name="httpClientFactory">Factory that supplies pooled clients under <see cref="HttpClientName"/>.</param>
    /// <param name="logger">Sink for debug/warning/error diagnostics of the translation call.</param>
    public RestErrorTranslator(string apiBaseUrl, IHttpClientFactory httpClientFactory, ILogger<RestErrorTranslator> logger)
    {
        _apiBaseUrl = apiBaseUrl;
        _httpClientFactory = httpClientFactory;
        _logger = logger;
    }

    /// <summary>
    /// Calls the translation endpoint to turn a stable error code into a message in the user's language;
    /// on any failure (non-success status, unparseable body, network/timeout) it logs and returns a safe
    /// <c>"Error: &lt;errorCode&gt;"</c> fallback rather than throwing.
    /// </summary>
    /// <param name="errorCode">Stable error code to translate.</param>
    /// <param name="userLanguage">Target culture/language for the message.</param>
    /// <returns>The translated message, or the <c>"Error: &lt;errorCode&gt;"</c> fallback if translation fails.</returns>
    public async Task<string> Translate(string errorCode, string userLanguage)
    {
        try
        {
            _logger.LogDebug("[RestErrorTranslator] Translating error {ErrorCode} to {Language}", errorCode, userLanguage);

            var httpClient = _httpClientFactory.CreateClient(HttpClientName);
            if (httpClient.Timeout == System.Threading.Timeout.InfiniteTimeSpan)
            {
                httpClient.Timeout = TimeSpan.FromSeconds(10);
            }

            var url = $"{_apiBaseUrl}/api/errortranslation/translate?errorCode={Uri.EscapeDataString(errorCode)}&language={Uri.EscapeDataString(userLanguage)}";
            var response = await httpClient.GetAsync(url).ConfigureAwait(false);

            if (response.IsSuccessStatusCode)
            {
                var responseContent = await response.Content.ReadAsStringAsync().ConfigureAwait(false);
                var result = JsonSerializer.Deserialize<TranslationResponse>(responseContent);

                if (result?.success == true)
                {
                    return result.translatedMessage ?? $"Error: {errorCode}";
                }
            }

            _logger.LogWarning("[RestErrorTranslator] Translation failed for {ErrorCode}, status {Status}", errorCode, (int)response.StatusCode);
            return $"Error: {errorCode}";
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "[RestErrorTranslator] Translation failed for {ErrorCode}", errorCode);
            return $"Error: {errorCode}";
        }
    }
}

/// <summary>
/// Wire shape of the translation endpoint's JSON reply, deserialized by <see cref="RestErrorTranslator"/>.
/// </summary>
public class TranslationResponse
{
    /// <summary>Whether the endpoint resolved a translation; when false, the translator uses its fallback.</summary>
    public bool success { get; set; }

    /// <summary>The localized message when <see cref="success"/> is true; otherwise null.</summary>
    public string? translatedMessage { get; set; }
}
