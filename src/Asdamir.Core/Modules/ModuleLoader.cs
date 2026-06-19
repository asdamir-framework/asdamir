// Copyright (C) 2026 Orhan Özşahin — Asdamir.
// Licensed under the GNU Lesser General Public License v3.0. See LICENSE.
// SPDX-License-Identifier: LGPL-3.0-or-later
//
// This file is part of the Asdamir open core. It is free software: you can redistribute it
// and/or modify it under the terms of the GNU Lesser General Public License as published by the
// Free Software Foundation, either version 3 of the License, or (at your option) any later
// version. It is distributed WITHOUT ANY WARRANTY; without even the implied warranty of
// MERCHANTABILITY or FITNESS FOR A PARTICULAR PURPOSE. See the GNU LGPL for more details.

using System.Runtime.Loader;
using Asdamir.Core.Contracts;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;
using Microsoft.AspNetCore.Builder;
using Microsoft.Extensions.Hosting;
using System.Reflection;

namespace Asdamir.Core.Modules;

/// <summary>
/// Provides dynamic loading and lifecycle management for application modules.
///
/// Audit fixes vs. v1:
///  - <see cref="DiscoverModulesAsync"/> now receives the live <see cref="IServiceCollection"/> as
///    a parameter and is intended to be called BEFORE <c>BuildServiceProvider</c>. The previous
///    implementation tried <c>GetRequiredService&lt;IServiceCollection&gt;()</c> from a built
///    provider — this always threw, was caught by the broad catch, and silently dropped
///    every module's <c>ConfigureServices</c> phase. Only <c>Configure</c> ever ran.
///  - <c>DiscoverModulesAsync</c> failures during service registration now rethrow. A misbehaving
///    module is a startup defect — failing fast surfaces the bug, the v1 swallow hid it.
///  - Configure/InitializeAsync failures are still logged-and-continued; runtime errors in one
///    module should not block the rest of the app from starting.
/// </summary>
public sealed class ModuleLoader
{
    private readonly List<IModule> _loaded = new();
    private readonly ILogger<ModuleLoader> _logger;

    public ModuleLoader(ILogger<ModuleLoader> logger)
    {
        _logger = logger;
    }

    public IReadOnlyList<IModule> GetLoadedModules() => _loaded;

    /// <summary>
    /// Phase 1 — discover modules and let them register services into the supplied
    /// <see cref="IServiceCollection"/>. Call this before <c>BuildServiceProvider</c>.
    /// </summary>
    /// <exception cref="InvalidOperationException">Wraps any module's ConfigureServices failure.</exception>
    public async Task DiscoverModulesAsync(string folder, IServiceCollection services, IConfiguration configuration, CancellationToken ct = default)
    {
        ArgumentNullException.ThrowIfNull(services);
        ArgumentNullException.ThrowIfNull(configuration);

        if (!Directory.Exists(folder))
        {
            _logger.LogInformation("Modules folder not found: {Folder}", folder);
            return;
        }

        foreach (var dll in Directory.EnumerateFiles(folder, "*.dll", SearchOption.TopDirectoryOnly))
        {
            Assembly asm;
            try
            {
                using var stream = File.OpenRead(dll);
                var bytes = new byte[stream.Length];
                _ = await stream.ReadAsync(bytes.AsMemory(0, bytes.Length), ct);
                asm = AssemblyLoadContext.Default.LoadFromStream(new MemoryStream(bytes));
            }
            catch (Exception ex)
            {
                // Loading the assembly itself failed — log and continue with the next file.
                _logger.LogError(ex, "Failed to load module assembly {Dll}", Path.GetFileName(dll));
                continue;
            }

            Type[] moduleTypes;
            try
            {
                moduleTypes = asm.GetTypes()
                    .Where(t => typeof(IModule).IsAssignableFrom(t) && !t.IsAbstract && t.IsClass)
                    .ToArray();
            }
            catch (ReflectionTypeLoadException ex)
            {
                _logger.LogError(ex, "Failed to enumerate types in {Dll}", asm.FullName);
                continue;
            }

            foreach (var t in moduleTypes)
            {
                if (Activator.CreateInstance(t) is not IModule m) continue;

                _logger.LogInformation("Registering module {Name} {Version}", m.Name, m.Version);

                try
                {
                    m.ConfigureServices(services, configuration);
                }
                catch (Exception ex)
                {
                    // A module that cannot register its services is a startup defect — fail fast.
                    throw new InvalidOperationException(
                        $"Module '{m.Name}' failed during ConfigureServices. See inner exception.", ex);
                }

                _loaded.Add(m);
            }
        }
    }

    /// <summary>
    /// Phase 2 — call <c>Configure</c> and <c>InitializeAsync</c> on each previously-discovered module.
    /// Failures here are logged and the remaining modules still run.
    /// </summary>
    public async Task InitialiseModulesAsync(IServiceProvider root, CancellationToken ct = default)
    {
        foreach (var m in _loaded)
        {
            using var scope = root.CreateScope();
            try
            {
                var env = scope.ServiceProvider.GetRequiredService<IHostEnvironment>();
                var app = scope.ServiceProvider.GetRequiredService<IApplicationBuilder>();
                m.Configure(app, env);
                await m.InitializeAsync(scope.ServiceProvider, ct);
                _logger.LogInformation("Module initialised: {Name}", m.Name);
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Module initialise failed: {Name}", m.Name);
            }
        }
    }

    /// <summary>
    /// Backwards-compatible overload — discovers + initialises. Prefer the two-phase API.
    /// </summary>
    [Obsolete("Use DiscoverModulesAsync (before BuildServiceProvider) + InitialiseModulesAsync (after) to avoid the v1 silent-failure pattern.")]
    public async Task LoadModulesAsync(string folder, IServiceCollection services, IConfiguration configuration, IServiceProvider root, CancellationToken ct = default)
    {
        await DiscoverModulesAsync(folder, services, configuration, ct);
        await InitialiseModulesAsync(root, ct);
    }
}
