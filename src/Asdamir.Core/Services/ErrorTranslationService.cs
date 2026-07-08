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

namespace Asdamir.Core.Contracts;

/// <summary>
/// Default <c>IErrorTranslationService</c> — resolves a stable error key to a localized user-facing
/// message (the user channel of the two-channel error model). Looks the key up per language via
/// <c>IErrorTranslationRepository</c> (backed by <c>LocalizationResource</c>), substitutes
/// <c>{name}</c> placeholders, then degrades gracefully: requested language → English → a built-in
/// generic fallback chosen by exception type / key heuristics. Never throws — a repository failure
/// is logged and a generic message is returned so the user always sees text, never a raw key.
/// </summary>
public class ErrorTranslationService : IErrorTranslationService
{
    private readonly IErrorTranslationRepository _repository;
    private readonly ILogger<ErrorTranslationService> _logger;

    /// <summary>Creates the service over the translation repository and the logger used for lookup failures.</summary>
    /// <param name="repository">Repository that reads localized error strings (from <c>LocalizationResource</c>).</param>
    /// <param name="logger">Logger for translation-lookup failures (which trigger the generic fallback).</param>
    public ErrorTranslationService(IErrorTranslationRepository repository, ILogger<ErrorTranslationService> logger)
    {
        _repository = repository;
        _logger = logger;
    }

    /// <inheritdoc/>
    public async Task<string> GetTranslatedMessageAsync(string errorKey, string language, Dictionary<string, object>? parameters = null, CancellationToken cancellationToken = default)
    {
        try
        {
            var translation = await _repository.GetTranslationAsync(errorKey, language);
            
            if (translation != null)
            {
                var message = translation.Message;
                
                // Parametreleri mesaja yerleştir
                if (parameters != null && parameters.Any())
                {
                    message = ReplaceParameters(message, parameters);
                }
                
                return message;
            }

            // Fallback: İngilizce çeviriyi dene
            if (language != "en")
            {
                var englishTranslation = await _repository.GetTranslationAsync(errorKey, "en");
                if (englishTranslation != null)
                {
                    var message = englishTranslation.Message;
                    if (parameters != null && parameters.Any())
                    {
                        message = ReplaceParameters(message, parameters);
                    }
                    return message;
                }
            }

            // Son fallback: Generic hata mesajı
            return await GetFallbackMessageAsync(errorKey, language, null, cancellationToken);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error getting translation for key: {ErrorKey}, language: {Language}", errorKey, language);
            return await GetFallbackMessageAsync(errorKey, language, ex, cancellationToken);
        }
    }

    /// <inheritdoc/>
    public async Task<Dictionary<string, string>> GetTranslatedMessagesAsync(string errorKey, Dictionary<string, object>? parameters = null, CancellationToken cancellationToken = default)
    {
        try
        {
            var translations = await _repository.GetTranslationsAsync(errorKey);
            
            if (translations.Any())
            {
                var result = new Dictionary<string, string>();
                
                foreach (var translation in translations)
                {
                    var message = translation.Value;
                    
                    // Parametreleri mesaja yerleştir
                    if (parameters != null && parameters.Any())
                    {
                        message = ReplaceParameters(message, parameters);
                    }
                    
                    result[translation.Key] = message;
                }
                
                return result;
            }

            // Fallback: Generic hata mesajları
            return GetGenericFallbackMessages(errorKey);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error getting translations for key: {ErrorKey}", errorKey);
            return GetGenericFallbackMessages(errorKey);
        }
    }

    /// <inheritdoc/>
    public Task<string> GetFallbackMessageAsync(string errorKey, string language, Exception? exception = null, CancellationToken cancellationToken = default)
    {
        // Generic fallback mesajları
        var fallbackMessages = new Dictionary<string, Dictionary<string, string>>
        {
            ["tr"] = new Dictionary<string, string>
            {
                ["error.generic"] = "Beklenmeyen bir hata oluştu",
                ["error.internal.server"] = "Sunucu hatası",
                ["error.bad.request"] = "Geçersiz istek",
                ["error.not.found"] = "Kaynak bulunamadı",
                ["error.unauthorized"] = "Yetkisiz erişim",
                ["error.forbidden"] = "Erişim reddedildi"
            },
            ["en"] = new Dictionary<string, string>
            {
                ["error.generic"] = "An unexpected error occurred",
                ["error.internal.server"] = "Server error",
                ["error.bad.request"] = "Bad request",
                ["error.not.found"] = "Resource not found",
                ["error.unauthorized"] = "Unauthorized access",
                ["error.forbidden"] = "Access forbidden"
            },
            ["ru"] = new Dictionary<string, string>
            {
                ["error.generic"] = "Произошла неожиданная ошибка",
                ["error.internal.server"] = "Ошибка сервера",
                ["error.bad.request"] = "Неверный запрос",
                ["error.not.found"] = "Ресурс не найден",
                ["error.unauthorized"] = "Несанкционированный доступ",
                ["error.forbidden"] = "Доступ запрещен"
            }
        };

        // Hata tipine göre uygun fallback mesajını seç
        var fallbackKey = GetFallbackKey(errorKey, exception);
        
        if (fallbackMessages.TryGetValue(language, out var languageMessages) && 
            languageMessages.TryGetValue(fallbackKey, out var message))
        {
            return Task.FromResult(message);
        }

        // Son çare: İngilizce generic mesaj
        return Task.FromResult(fallbackMessages["en"]["error.generic"]);
    }

    private string ReplaceParameters(string message, Dictionary<string, object> parameters)
    {
        if (parameters.Count == 0) return message;

        // Audit fix: v1 reassigned `message` once per parameter, allocating a new string
        // each time. For 5 parameters that's 5 immutable copies. We build the result
        // into a StringBuilder and replace in place; the final ToString() allocates once.
        var sb = new System.Text.StringBuilder(message);
        foreach (var parameter in parameters)
        {
            var placeholder = $"{{{parameter.Key}}}";
            var value = parameter.Value?.ToString() ?? string.Empty;
            sb.Replace(placeholder, value);
        }
        return sb.ToString();
    }

    private Dictionary<string, string> GetGenericFallbackMessages(string errorKey)
    {
        return new Dictionary<string, string>
        {
            ["tr"] = "Beklenmeyen bir hata oluştu",
            ["en"] = "An unexpected error occurred",
            ["ru"] = "Произошла неожиданная ошибка"
        };
    }

    private string GetFallbackKey(string errorKey, Exception? exception)
    {
        // Exception tipine göre fallback key belirle
        if (exception != null)
        {
            return exception switch
            {
                UnauthorizedAccessException => "error.unauthorized",
                ArgumentException => "error.bad.request",
                FileNotFoundException => "error.not.found",
                TimeoutException => "error.timeout",
                _ => "error.generic"
            };
        }

        // Error key'e göre fallback key belirle
        if (errorKey.Contains("auth", StringComparison.OrdinalIgnoreCase))
            return "error.unauthorized";
        if (errorKey.Contains("validation", StringComparison.OrdinalIgnoreCase))
            return "error.bad.request";
        if (errorKey.Contains("not.found", StringComparison.OrdinalIgnoreCase))
            return "error.not.found";

        return "error.generic";
    }
}
