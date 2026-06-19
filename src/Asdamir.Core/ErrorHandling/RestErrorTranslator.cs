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
    public const string HttpClientName = "ErrorTranslation";

    private readonly string _apiBaseUrl;
    private readonly IHttpClientFactory _httpClientFactory;
    private readonly ILogger<RestErrorTranslator> _logger;

    public RestErrorTranslator(string apiBaseUrl, IHttpClientFactory httpClientFactory, ILogger<RestErrorTranslator> logger)
    {
        _apiBaseUrl = apiBaseUrl;
        _httpClientFactory = httpClientFactory;
        _logger = logger;
    }

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

public class TranslationResponse
{
    public bool success { get; set; }
    public string? translatedMessage { get; set; }
}
