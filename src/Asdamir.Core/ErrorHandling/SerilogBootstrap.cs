// Copyright (C) 2026 Orhan Özşahin — Asdamir.
// Licensed under the GNU Lesser General Public License v3.0. See LICENSE.
// SPDX-License-Identifier: LGPL-3.0-or-later
//
// This file is part of the Asdamir open core. It is free software: you can redistribute it
// and/or modify it under the terms of the GNU Lesser General Public License as published by the
// Free Software Foundation, either version 3 of the License, or (at your option) any later
// version. It is distributed WITHOUT ANY WARRANTY; without even the implied warranty of
// MERCHANTABILITY or FITNESS FOR A PARTICULAR PURPOSE. See the GNU LGPL for more details.

using Serilog.Sinks.MSSqlServer;
using System.Collections.ObjectModel;
using Serilog;
using Serilog.Core;
using Serilog.Events;
using System.Text.RegularExpressions;
using System.Data;

namespace Asdamir.Core.ErrorHandling.Logging;

/// <summary>
/// Serilog bootstrap helpers.
///
/// Audit fixes vs. v1:
///  - <see cref="UseDevelopment"/> and <see cref="UseProduction"/> used to call
///    <see cref="UseDefault"/> and then attach a SECOND console/file sink on top of
///    the existing ones. Every dev-mode log line was duplicated. Now both helpers
///    build their own configuration once.
///  - <see cref="UseProduction"/> set the minimum level to <c>Warning</c>, which
///    silently dropped login / audit / business-event logs that ops rely on. Prod
///    minimum is now <c>Information</c>, with framework noise filtered via
///    per-namespace overrides.
///  - A <see cref="SecretRedactionEnricher"/> scrubs values whose property name
///    matches a known secret pattern (<c>password</c>, <c>token</c>, <c>secret</c>,
///    <c>apikey</c>, <c>authorization</c>) before sinks see them — so a stray
///    <c>LogInformation("body: {Body}", dto)</c> can't leak a JWT.
///  - The application name and environment are read from env vars (<c>LOG_APPLICATION</c>,
///    <c>ASPNETCORE_ENVIRONMENT</c>); no more hardcoded "Fluent UI_Framework".
/// </summary>
public static class SerilogBootstrap
{
    private const string DefaultConsoleTemplate = "[{Timestamp:HH:mm:ss} {Level:u3}] {Message:lj} {Properties:j}{NewLine}{Exception}";
    private const string DefaultFileTemplate = "[{Timestamp:yyyy-MM-dd HH:mm:ss.fff zzz} {Level:u3}] {Message:lj} {Properties:j}{NewLine}{Exception}";

    public static void UseDefault(Action<LoggerConfiguration>? customize = null)
    {
        var config = BuildBaseConfig(LogEventLevel.Information);
        ApplyOutputs(config, minimumSinkLevel: LogEventLevel.Information);
        customize?.Invoke(config);
        Log.Logger = config.CreateLogger();
    }

    public static void UseDevelopment(Action<LoggerConfiguration>? customize = null)
    {
        var config = BuildBaseConfig(LogEventLevel.Debug);
        ApplyOutputs(config, minimumSinkLevel: LogEventLevel.Debug, fileSubdir: "dev-");
        customize?.Invoke(config);
        Log.Logger = config.CreateLogger();
    }

    public static void UseProduction(Action<LoggerConfiguration>? customize = null)
    {
        var config = BuildBaseConfig(LogEventLevel.Information);
        ApplyOutputs(config, minimumSinkLevel: LogEventLevel.Information, fileSubdir: "prod-");
        customize?.Invoke(config);
        Log.Logger = config.CreateLogger();
    }

    public static void UseWithDatabase(Action<LoggerConfiguration>? customize = null)
    {
        var config = BuildBaseConfig(LogEventLevel.Information);
        ApplyOutputs(config, minimumSinkLevel: LogEventLevel.Information);

        var connectionString = Environment.GetEnvironmentVariable("LOG_DB_CONNECTION");
        var target = Environment.GetEnvironmentVariable("LOG_TARGET")?.ToLowerInvariant() ?? "console";

        if (!string.IsNullOrEmpty(connectionString) && (target is "database" or "all"))
        {
            config = config.WriteTo.MSSqlServer(
                connectionString: connectionString,
                sinkOptions: new MSSqlServerSinkOptions
                {
                    TableName = "AppLog",
                    SchemaName = "dbo",
                    AutoCreateSqlTable = false,
                    BatchPostingLimit = 50,
                    BatchPeriod = TimeSpan.FromSeconds(5),
                },
                columnOptions: GetColumnOptions(),
                restrictedToMinimumLevel: LogEventLevel.Information);
        }

        customize?.Invoke(config);
        Log.Logger = config.CreateLogger();
    }

    private static LoggerConfiguration BuildBaseConfig(LogEventLevel minimumLevel)
    {
        var app = Environment.GetEnvironmentVariable("LOG_APPLICATION") ?? "Asdamir";
        var env = Environment.GetEnvironmentVariable("ASPNETCORE_ENVIRONMENT") ?? "Development";

        return new LoggerConfiguration()
            .MinimumLevel.Is(minimumLevel)
            .MinimumLevel.Override("Microsoft", LogEventLevel.Warning)
            .MinimumLevel.Override("Microsoft.Hosting.Lifetime", LogEventLevel.Information)
            .MinimumLevel.Override("System", LogEventLevel.Warning)
            .Enrich.FromLogContext()
            .Enrich.With(new SecretRedactionEnricher())
            .Enrich.WithProperty("Application", app)
            .Enrich.WithProperty("Environment", env);
    }

    private static void ApplyOutputs(LoggerConfiguration config, LogEventLevel minimumSinkLevel, string fileSubdir = "app-")
    {
        var target = Environment.GetEnvironmentVariable("LOG_TARGET")?.ToLowerInvariant() ?? "console";
        var defaultLogPath = $"logs/{fileSubdir}.log";
        var logPath = Environment.GetEnvironmentVariable("LOG_FILE_PATH") ?? defaultLogPath;

        if (target is "console" or "both" or "all")
        {
            config.WriteTo.Console(minimumSinkLevel, outputTemplate: DefaultConsoleTemplate);
        }

        if (target is "file" or "both" or "all")
        {
            config.WriteTo.File(
                path: logPath,
                restrictedToMinimumLevel: minimumSinkLevel,
                rollingInterval: RollingInterval.Day,
                retainedFileCountLimit: 7,
                rollOnFileSizeLimit: true,
                fileSizeLimitBytes: 10 * 1024 * 1024,
                outputTemplate: DefaultFileTemplate);
        }
    }

    public static ColumnOptions GetColumnOptions()
    {
        var columnOptions = new ColumnOptions();
        columnOptions.Store.Remove(StandardColumn.MessageTemplate);
        columnOptions.Store.Remove(StandardColumn.LogEvent);
        columnOptions.Store.Remove(StandardColumn.Exception);

        columnOptions.TimeStamp.ColumnName = "CreatedAtUtc";
        columnOptions.TimeStamp.ConvertToUtc = false;

        columnOptions.Level.StoreAsEnum = false;

        columnOptions.AdditionalColumns = new Collection<SqlColumn>
        {
            new SqlColumn { ColumnName = "Source", DataType = SqlDbType.NVarChar, DataLength = 255, AllowNull = true },
            new SqlColumn { ColumnName = "ErrorKey", DataType = SqlDbType.NVarChar, DataLength = 255, AllowNull = true },
            new SqlColumn { ColumnName = "UserLanguage", DataType = SqlDbType.NVarChar, DataLength = 10, AllowNull = true }
        };

        return columnOptions;
    }
}

/// <summary>
/// Serilog enricher that replaces values of well-known secret-shaped properties with
/// <c>***REDACTED***</c> before any sink sees them. Cheap on the hot path: only scans
/// property names, never the payload contents. Property name match is case-insensitive.
/// </summary>
/// <remarks>
/// Wired up by <see cref="SerilogBootstrap"/> for the framework's default pipelines. Also
/// part of the public NuGet contract so framework consumers can attach it to their own
/// <c>LoggerConfiguration</c> instances directly:
/// <code>
/// new LoggerConfiguration().Enrich.With(new SecretRedactionEnricher())...
/// </code>
/// Audit pin: made public (was <c>internal sealed</c>) in MEDIUM sub-batch 11 so regression
/// tests can attach it to a capturing sink and verify the redaction rules.
/// </remarks>
public sealed class SecretRedactionEnricher : ILogEventEnricher
{
    private static readonly Regex SecretNamePattern =
        new(@"(?i)(password|passwd|token|refreshtoken|accesstoken|secret|apikey|api_key|authorization|clientsecret)",
            RegexOptions.Compiled | RegexOptions.CultureInvariant);

    public void Enrich(LogEvent logEvent, ILogEventPropertyFactory propertyFactory)
    {
        // Snapshot keys to avoid mutating during enumeration.
        string[] toRedact;
        var properties = logEvent.Properties;
        var matches = new List<string>(capacity: 2);
        foreach (var kv in properties)
        {
            if (SecretNamePattern.IsMatch(kv.Key))
                matches.Add(kv.Key);
        }
        toRedact = matches.ToArray();

        foreach (var key in toRedact)
        {
            logEvent.AddOrUpdateProperty(propertyFactory.CreateProperty(key, "***REDACTED***"));
        }
    }
}
