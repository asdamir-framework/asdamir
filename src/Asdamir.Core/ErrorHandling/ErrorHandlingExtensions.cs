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

namespace Asdamir.Core.ErrorHandling.Extensions;

/// <summary>
/// Extension methods for easy error handling in Framework projects
/// </summary>
public static class ErrorHandlingExtensions
{
    /// <summary>
    /// Executes an action with automatic error logging using ILogger
    /// </summary>
    /// <param name="action">The action to execute</param>
    /// <param name="logger">The logger instance</param>
    /// <param name="context">Context information (e.g., "UserService.CreateUser")</param>
    /// <param name="errorKey">Error key for translation lookup</param>
    /// <param name="language">User language for error translation</param>
    /// <param name="rethrow">Whether to rethrow the exception after logging</param>
    /// <returns>True if successful, false if error occurred and rethrow=false</returns>
    public static async Task<bool> ExecuteWithErrorHandlingAsync(
        this Func<Task> action,
        ILogger logger,
        string context,
        string errorKey = "error.generic",
        string language = "en",
        bool rethrow = true)
    {
        try
        {
            await action();
            return true;
        }
        catch (Exception ex)
        {
            logger.LogError(ex,
                "Error in {Context}. ErrorKey: {ErrorKey}, Language: {Language}, Source: {Source}",
                context, errorKey, language, context);

            if (rethrow)
                throw;

            return false;
        }
    }

    /// <summary>
    /// Executes a function with automatic error logging using ILogger
    /// </summary>
    /// <typeparam name="T">Return type</typeparam>
    /// <param name="func">The function to execute</param>
    /// <param name="logger">The logger instance</param>
    /// <param name="context">Context information</param>
    /// <param name="errorKey">Error key for translation lookup</param>
    /// <param name="language">User language for error translation</param>
    /// <param name="defaultValue">Default value to return on error (if rethrow=false)</param>
    /// <param name="rethrow">Whether to rethrow the exception after logging</param>
    /// <returns>Function result or default value on error</returns>
    public static async Task<T?> ExecuteWithErrorHandlingAsync<T>(
        this Func<Task<T>> func,
        ILogger logger,
        string context,
        string errorKey = "error.generic",
        string language = "en",
        T? defaultValue = default,
        bool rethrow = true)
    {
        try
        {
            return await func();
        }
        catch (Exception ex)
        {
            logger.LogError(ex,
                "Error in {Context}. ErrorKey: {ErrorKey}, Language: {Language}, Source: {Source}",
                context, errorKey, language, context);

            if (rethrow)
                throw;

            return defaultValue;
        }
    }

    /// <summary>
    /// Executes a synchronous action with automatic error logging using ILogger
    /// </summary>
    public static bool ExecuteWithErrorHandling(
        this Action action,
        ILogger logger,
        string context,
        string errorKey = "error.generic",
        string language = "en",
        bool rethrow = true)
    {
        try
        {
            action();
            return true;
        }
        catch (Exception ex)
        {
            logger.LogError(ex,
                "Error in {Context}. ErrorKey: {ErrorKey}, Language: {Language}, Source: {Source}",
                context, errorKey, language, context);

            if (rethrow)
                throw;

            return false;
        }
    }

    /// <summary>
    /// Executes a synchronous function with automatic error logging using ILogger
    /// </summary>
    public static T? ExecuteWithErrorHandling<T>(
        this Func<T> func,
        ILogger logger,
        string context,
        string errorKey = "error.generic",
        string language = "en",
        T? defaultValue = default,
        bool rethrow = true)
    {
        try
        {
            return func();
        }
        catch (Exception ex)
        {
            logger.LogError(ex,
                "Error in {Context}. ErrorKey: {ErrorKey}, Language: {Language}, Source: {Source}",
                context, errorKey, language, context);

            if (rethrow)
                throw;

            return defaultValue;
        }
    }
}
