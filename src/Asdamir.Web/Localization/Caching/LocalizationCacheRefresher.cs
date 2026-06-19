// Copyright (C) 2026 Orhan Özşahin — Asdamir.
// Licensed under the GNU Lesser General Public License v3.0. See LICENSE.
// SPDX-License-Identifier: LGPL-3.0-or-later
//
// This file is part of the Asdamir open core. It is free software: you can redistribute it
// and/or modify it under the terms of the GNU Lesser General Public License as published by the
// Free Software Foundation, either version 3 of the License, or (at your option) any later
// version. It is distributed WITHOUT ANY WARRANTY; without even the implied warranty of
// MERCHANTABILITY or FITNESS FOR A PARTICULAR PURPOSE. See the GNU LGPL for more details.

using Asdamir.Web.Localization.Api;
using Microsoft.Extensions.Hosting;

namespace Asdamir.Web.Localization.Caching;

/// <summary>
/// Background service that polls for localization version changes and bumps the
/// cache generation when a change is detected.
///
/// Audit fixes vs. v1:
///  - The catch-all <c>catch (Exception ex)</c> trapped <see cref="OperationCanceledException"/>
///    from the stopping token and logged it as <c>LogError</c> with "❌ Error" — every
///    graceful shutdown produced a noisy error in the logs and an alarm on dashboards
///    that watched the error rate. Cancellation is now handled separately and exits
///    the loop quietly.
///  - On transient API failure the loop slept the full 10 s with no backoff. We now
///    use a shorter delay after consecutive failures so the service recovers faster
///    when the API comes back up, with a ceiling so we don't poll-hammer.
/// </summary>
public sealed class LocalizationCacheRefresher : BackgroundService
{
    private static readonly TimeSpan PollInterval = TimeSpan.FromSeconds(10);
    private static readonly TimeSpan MinBackoff = TimeSpan.FromSeconds(2);
    private static readonly TimeSpan MaxBackoff = TimeSpan.FromMinutes(2);

    private readonly ILocalizationApiClient _apiClient;
    private readonly ILocalizationCacheGeneration _cacheGeneration;
    private readonly ILogger<LocalizationCacheRefresher> _logger;
    private long _lastVersion;

    public LocalizationCacheRefresher(
        ILocalizationApiClient apiClient,
        ILocalizationCacheGeneration cacheGeneration,
        ILogger<LocalizationCacheRefresher> logger)
    {
        _apiClient = apiClient;
        _cacheGeneration = cacheGeneration;
        _logger = logger;
    }

    protected override async Task ExecuteAsync(CancellationToken stoppingToken)
    {
        _logger.LogInformation("Localization cache refresher started");

        var failureBackoff = MinBackoff;

        while (!stoppingToken.IsCancellationRequested)
        {
            TimeSpan nextDelay = PollInterval;
            try
            {
                var currentVersion = await _apiClient.GetVersionAsync(stoppingToken).ConfigureAwait(false);

                if (_lastVersion != 0 && currentVersion != _lastVersion)
                {
                    _cacheGeneration.Bump();
                    _logger.LogInformation("Localization version changed: {OldVersion} → {NewVersion}, cache invalidated",
                        _lastVersion, currentVersion);
                }

                _lastVersion = currentVersion;
                failureBackoff = MinBackoff; // success — reset
            }
            catch (OperationCanceledException) when (stoppingToken.IsCancellationRequested)
            {
                break; // graceful shutdown
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error checking localization version; backing off {Backoff}", failureBackoff);
                nextDelay = failureBackoff;
                failureBackoff = TimeSpan.FromTicks(Math.Min(MaxBackoff.Ticks, failureBackoff.Ticks * 2));
            }

            try
            {
                await Task.Delay(nextDelay, stoppingToken).ConfigureAwait(false);
            }
            catch (OperationCanceledException)
            {
                break;
            }
        }

        _logger.LogInformation("Localization cache refresher stopped");
    }
}
