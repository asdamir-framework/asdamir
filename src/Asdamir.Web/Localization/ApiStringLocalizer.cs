// Copyright (C) 2026 Orhan Özşahin — Asdamir.
// Licensed under the GNU Lesser General Public License v3.0. See LICENSE.
// SPDX-License-Identifier: LGPL-3.0-or-later
//
// This file is part of the Asdamir open core. It is free software: you can redistribute it
// and/or modify it under the terms of the GNU Lesser General Public License as published by the
// Free Software Foundation, either version 3 of the License, or (at your option) any later
// version. It is distributed WITHOUT ANY WARRANTY; without even the implied warranty of
// MERCHANTABILITY or FITNESS FOR A PARTICULAR PURPOSE. See the GNU LGPL for more details.

using Microsoft.Extensions.Localization;
using Asdamir.Core.MultiTenancy;
using Asdamir.Web.Localization.Abstractions;
using Asdamir.Web.Localization.Caching;
using Microsoft.AspNetCore.Http;
using System.Collections.Concurrent;
using System.Globalization;
using Microsoft.Extensions.DependencyInjection;
using System.Text.RegularExpressions;

namespace Asdamir.Web.Localization;

/// <summary>
/// REST API-backed string localizer.
///
/// Audit fix: `IStringLocalizer` is intrinsically synchronous (the indexer
/// <c>this[name]</c> returns a value, not a task). v1 of this class invoked the
/// async cache + async HTTP client via <c>.GetAwaiter().GetResult()</c> on the hot
/// path — a classic sync-over-async deadlock risk under SynchronizationContext.
///
/// v2 separates the two paths:
///   * <see cref="WarmAsync"/> — explicit async pre-load. Callers (typically an
///     <c>IHostedService</c> on app start, or the first awaited request) fill an
///     in-process <see cref="ConcurrentDictionary{TKey, TValue}"/> per culture.
///   * <c>this[name]</c> — pure sync dictionary read from the warmed map. Cache miss
///     surfaces as <c>resourceNotFound: true</c> rather than triggering an HTTP call
///     from the sync path.
///
/// Production wiring: register <see cref="ApiStringLocalizerFactory"/> as the
/// <see cref="IStringLocalizerFactory"/>, and a background service that calls
/// <see cref="WarmAsync"/> on every supported culture at startup + on
/// <see cref="ILocalizationCacheGeneration"/> bump.
/// </summary>
public sealed class ApiStringLocalizer : IStringLocalizer
{
    private readonly ILocalizationService _localizationService;
    private readonly ILocalizationCacheGeneration _cacheGeneration;
    private readonly ITenantContext? _tenantContext;
    private readonly ILogger<ApiStringLocalizer>? _logger;

    // Process-wide warmed map: (cacheKey -> name -> value). ConcurrentDictionary keeps
    // the indexer fully lock-free.
    private static readonly ConcurrentDictionary<string, Dictionary<string, string>> WarmedMaps =
        new(StringComparer.Ordinal);

    // Missing-key warnings are deduped per (cache-generation, culture, key). A page that renders a
    // missing key on every frame would otherwise flood the log (the raw key silently falls through to
    // the UI — the "raw key on screen" bug class); this warns ONCE so the gap surfaces in Serilog's DB
    // sink → ErrorMonitoring without noise. The cache-generation is part of the key so a post-reseed
    // re-warm re-surfaces a key that is *still* missing.
    private static readonly ConcurrentDictionary<string, byte> WarnedMissingKeys =
        new(StringComparer.Ordinal);

    /// <summary>
    /// Initializes the localizer with the resource source, cache-generation tracker and optional tenant scope.
    /// </summary>
    /// <param name="localizationService">Service that supplies the per-culture resource map.</param>
    /// <param name="cacheGeneration">Generation tracker whose bump invalidates the warmed maps.</param>
    /// <param name="tenantContext">Optional tenant context; when multi-tenant it scopes the warmed map per tenant.</param>
    /// <param name="logger">Optional logger for warm/miss diagnostics.</param>
    public ApiStringLocalizer(
        ILocalizationService localizationService,
        ILocalizationCacheGeneration cacheGeneration,
        ITenantContext? tenantContext = null,
        ILogger<ApiStringLocalizer>? logger = null)
    {
        _localizationService = localizationService;
        _cacheGeneration = cacheGeneration;
        _tenantContext = tenantContext;
        _logger = logger;
    }

    private string CacheKey(string culture)
    {
        var tenantId = _tenantContext?.IsMultiTenant == true ? _tenantContext.TenantId : "global";
        return $"loc:{_cacheGeneration.Current}:{culture}:{tenantId}";
    }

    /// <summary>
    /// Async pre-load for one culture. Call from a hosted service / startup hook;
    /// the sync indexer doesn't trigger HTTP itself.
    /// </summary>
    /// <param name="culture">The culture name to warm (e.g. <c>tr-TR</c>, <c>en-US</c>, <c>ru-RU</c>).</param>
    /// <param name="ct">Token to cancel the fetch.</param>
    /// <returns>A task that completes once the culture's map is loaded into the process-wide cache.</returns>
    public async Task WarmAsync(string culture, CancellationToken ct = default)
    {
        var key = CacheKey(culture);
        var result = await _localizationService.GetResourcesAsync(culture, ct).ConfigureAwait(false);
        var map = result as Dictionary<string, string> ?? new Dictionary<string, string>(result);
        WarmedMaps[key] = map;
        _logger?.LogDebug("Localizer warmed: {Key} ({Count} keys)", key, map.Count);
    }

    private Dictionary<string, string>? TryGetWarmedMap(CultureInfo culture)
        => WarmedMaps.TryGetValue(CacheKey(culture.Name), out var map) ? map : null;

    /// <inheritdoc/>
    public LocalizedString this[string name] => Resolve(name, Array.Empty<object>());

    /// <inheritdoc/>
    public LocalizedString this[string name, params object[] arguments] => Resolve(name, arguments);

    private LocalizedString Resolve(string name, object[] args)
    {
        var culture = CultureInfo.CurrentUICulture;
        var map = TryGetWarmedMap(culture);

        if (map is null)
        {
            _logger?.LogWarning(
                "Localizer not warmed yet for culture {Culture}; call WarmAsync at startup. Returning key as fallback.",
                culture.Name);
            return new LocalizedString(name, name, resourceNotFound: true);
        }

        if (map.TryGetValue(name, out var template))
        {
            var formatted = Format(template, args, culture);
            return new LocalizedString(name, formatted, resourceNotFound: false);
        }

        WarnMissingKeyOnce(name, culture);
        return new LocalizedString(name, name, resourceNotFound: true);
    }

    // Logs a "key not found" warning at most once per (cache-generation, culture, key) so a missing key
    // rendered every frame doesn't flood the log. See <see cref="WarnedMissingKeys"/>.
    private void WarnMissingKeyOnce(string name, CultureInfo culture)
    {
        if (_logger is null) return;
        var dedupKey = $"{_cacheGeneration.Current}␟{culture.Name}␟{name}";
        if (WarnedMissingKeys.TryAdd(dedupKey, 0))
            _logger.LogWarning("Localization key not found: {Key} for culture {Culture}", name, culture.Name);
    }

    /// <inheritdoc/>
    public IEnumerable<LocalizedString> GetAllStrings(bool includeParentCultures)
    {
        var map = TryGetWarmedMap(CultureInfo.CurrentUICulture);
        return map is null
            ? Enumerable.Empty<LocalizedString>()
            : map.Select(kv => new LocalizedString(kv.Key, kv.Value, resourceNotFound: false));
    }

    /// <summary>
    /// Format template with arguments
    /// Supports: Standard format (L["Key", arg1, arg2]) and Named placeholders (L["Key", new { name="Ali", count=3 }])
    /// </summary>
    private static string Format(string template, object[] args, CultureInfo culture)
    {
        if (args.Length == 0)
            return template;

        // Named placeholder support: {name}, {count}, etc.
        if (args.Length == 1 && args[0] is not string && args[0] is not IFormattable)
        {
            var properties = args[0].GetType().GetProperties();
            var dict = properties.ToDictionary(
                p => p.Name,
                p => p.GetValue(args[0])?.ToString() ?? string.Empty,
                StringComparer.OrdinalIgnoreCase);

            return Regex.Replace(template, @"\{(\w+)\}", match =>
            {
                var key = match.Groups[1].Value;
                return dict.TryGetValue(key, out var value) ? value : match.Value;
            });
        }

        // Standard string.Format
        try
        {
            return string.Format(culture, template, args);
        }
        catch (FormatException)
        {
            return template; // Return original if format fails
        }
    }
}

/// <summary>
/// Factory for creating ApiStringLocalizer instances
/// </summary>
public sealed class ApiStringLocalizerFactory : IStringLocalizerFactory
{
    private readonly ILocalizationService _localizationService;
    private readonly ILocalizationCacheGeneration _cacheGeneration;
    private readonly IHttpContextAccessor _httpContextAccessor;
    private readonly ILogger<ApiStringLocalizer>? _logger;

    /// <summary>
    /// Initializes the factory with the services shared by every localizer it creates.
    /// </summary>
    /// <param name="localizationService">Service that supplies the per-culture resource map.</param>
    /// <param name="cacheGeneration">Generation tracker used to key/invalidate the warmed maps.</param>
    /// <param name="httpContextAccessor">Accessor used to resolve the per-request tenant context.</param>
    /// <param name="logger">Optional logger passed to the created localizers.</param>
    public ApiStringLocalizerFactory(
        ILocalizationService localizationService,
        ILocalizationCacheGeneration cacheGeneration,
        IHttpContextAccessor httpContextAccessor,
        ILogger<ApiStringLocalizer>? logger = null)
    {
        _localizationService = localizationService;
        _cacheGeneration = cacheGeneration;
        _httpContextAccessor = httpContextAccessor;
        _logger = logger;
    }

    /// <inheritdoc/>
    public IStringLocalizer Create(Type resourceSource)
        => Create(resourceSource.FullName!, null);

    /// <inheritdoc/>
    public IStringLocalizer Create(string? baseName, string? location)
    {
        ITenantContext? tenantContext = null;
        var httpContext = _httpContextAccessor.HttpContext;
        if (httpContext != null)
        {
            tenantContext = httpContext.RequestServices.GetService<ITenantContext>();
        }

        return new ApiStringLocalizer(_localizationService, _cacheGeneration, tenantContext, _logger);
    }
}
