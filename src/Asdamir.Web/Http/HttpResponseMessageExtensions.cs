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
using Microsoft.Extensions.Localization;

namespace Asdamir.Web.Http;

/// <summary>
/// Turns a failed <see cref="HttpResponseMessage"/> into the message that should be shown to the
/// user — the SINGLE place the UI tier decides "what does the user see when an API call fails".
///
/// Contract (see CLAUDE.md "User-facing error display"): the API/Gateway tier runs the global
/// exception engine (<c>GlobalExceptionMiddleware</c>), which logs the REAL exception to its sinks
/// (Console + File + DB <c>AppLog</c>) and returns a <c>ProblemDetails</c> whose <c>Title</c> is a
/// localized, user-safe message resolved from the <c>LocalizationResource</c> table
/// (<c>Category='ERROR'</c>) by the request culture. The UI must surface THAT title — it must NEVER
/// show the raw HTTP status code (no "Server error (500)").
/// </summary>
public static class HttpResponseMessageExtensions
{
    /// <summary>
    /// Reads the localized, user-safe error message for a non-success response. Prefers the engine's
    /// <c>ProblemDetails.Title</c>; falls back to the generic localized key
    /// <c>Common.UnexpectedError</c> (and, only if that is unseeded, a neutral string). Never exposes
    /// the HTTP status code or a raw error key.
    /// </summary>
    public static async Task<string> ToUserMessageAsync(
        this HttpResponseMessage response,
        IStringLocalizer localizer,
        CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(response);
        ArgumentNullException.ThrowIfNull(localizer);

        try
        {
            var mediaType = response.Content.Headers.ContentType?.MediaType;
            if (mediaType is "application/problem+json" or "application/json")
            {
                var problem = await response.Content
                    .ReadFromJsonAsync<ProblemPayload>(cancellationToken)
                    .ConfigureAwait(false);
                var title = problem?.Title;
                if (!string.IsNullOrWhiteSpace(title) && !LooksLikeRawErrorKey(title))
                {
                    return title;
                }
            }
        }
        catch
        {
            // Body was not problem+json, was empty, or could not be parsed — fall back to the
            // generic localized message below. We deliberately swallow: the real error is already
            // logged server-side by the global exception engine.
        }

        var generic = localizer["Common.UnexpectedError"];
        return generic.ResourceNotFound ? "An unexpected error occurred. Please try again." : generic.Value;
    }

    // The engine falls back to a bare error key (e.g. "database.connection.error") only when its own
    // localization lookup misses; guard so that internal key never reaches the user.
    private static bool LooksLikeRawErrorKey(string value)
        => !value.Contains(' ')
           && value.Contains('.')
           && value.All(c => char.IsLetterOrDigit(c) || c is '.' or '_' or '-');

    private sealed class ProblemPayload
    {
        public string? Title { get; set; }
        public string? Detail { get; set; }
        public int? Status { get; set; }
    }
}
