// Copyright (C) 2026 Orhan Özşahin — Asdamir.
// Licensed under the GNU Lesser General Public License v3.0. See LICENSE.
// SPDX-License-Identifier: LGPL-3.0-or-later
//
// This file is part of the Asdamir open core. It is free software: you can redistribute it
// and/or modify it under the terms of the GNU Lesser General Public License as published by the
// Free Software Foundation, either version 3 of the License, or (at your option) any later
// version. It is distributed WITHOUT ANY WARRANTY; without even the implied warranty of
// MERCHANTABILITY or FITNESS FOR A PARTICULAR PURPOSE. See the GNU LGPL for more details.

namespace Asdamir.Core.Contracts;

/// <summary>
/// Defines the contract for a CLI command that can be executed via dependency injection.
/// This interface enables the CLI framework to discover and execute commands using modern DI patterns.
/// </summary>
/// <remarks>
/// Implementations should be registered with dependency injection as scoped or transient services.
/// The CLI framework will resolve command instances and invoke their Execute method with parsed arguments.
/// 
/// <para><b>Example Implementation:</b></para>
/// <code>
/// public class HashCommand : ICliCommand
/// {
///     private readonly ILogger&lt;HashCommand&gt; _logger;
///     
///     public HashCommand(ILogger&lt;HashCommand&gt; logger)
///     {
///         _logger = logger;
///     }
///     
///     public async Task&lt;int&gt; ExecuteAsync(string[] args, CancellationToken cancellationToken = default)
///     {
///         _logger.LogInformation("Executing hash command with {ArgCount} arguments", args.Length);
///         // Command logic here
///         return 0; // Success
///     }
/// }
/// </code>
/// 
/// <para><b>Registration:</b></para>
/// <code>
/// services.AddScoped&lt;ICliCommand, HashCommand&gt;();
/// </code>
/// </remarks>
public interface ICliCommand
{
    /// <summary>
    /// Executes the CLI command with the provided arguments.
    /// </summary>
    /// <param name="args">Command-line arguments passed to the command (excluding the command name itself).</param>
    /// <param name="cancellationToken">Cancellation token to support command cancellation.</param>
    /// <returns>
    /// A task representing the asynchronous operation with an exit code result.
    /// Return 0 for success, non-zero for failure (following Unix convention).
    /// </returns>
    /// <exception cref="ArgumentException">Thrown when required arguments are missing or invalid.</exception>
    /// <exception cref="OperationCanceledException">Thrown when the operation is cancelled via <paramref name="cancellationToken"/>.</exception>
    Task<int> ExecuteAsync(string[] args, CancellationToken cancellationToken = default);
}
