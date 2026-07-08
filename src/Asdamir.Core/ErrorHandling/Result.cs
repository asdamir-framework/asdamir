// Copyright (C) 2026 Orhan Özşahin — Asdamir.
// Licensed under the GNU Lesser General Public License v3.0. See LICENSE.
// SPDX-License-Identifier: LGPL-3.0-or-later
//
// This file is part of the Asdamir open core. It is free software: you can redistribute it
// and/or modify it under the terms of the GNU Lesser General Public License as published by the
// Free Software Foundation, either version 3 of the License, or (at your option) any later
// version. It is distributed WITHOUT ANY WARRANTY; without even the implied warranty of
// MERCHANTABILITY or FITNESS FOR A PARTICULAR PURPOSE. See the GNU LGPL for more details.

namespace Asdamir.Core.ErrorHandling.Abstractions;

/// <summary>
/// A no-throw outcome carrying a value on success or an <see cref="Error"/> on failure. Use it to model
/// EXPECTED failures as data (the two-channel model) instead of exceptions, so callers branch on
/// <see cref="IsSuccess"/> rather than catching. Construct via the <c>Ok</c>/<c>Fail</c> factories.
/// </summary>
/// <typeparam name="T">Type of the value when successful</typeparam>
public class Result<T>
{
    /// <summary>True when the operation succeeded and <see cref="Value"/> is populated; false means <see cref="Error"/> is set and <see cref="Value"/> must not be used.</summary>
    public bool IsSuccess { get; }

    /// <summary>The produced value on success; <c>null</c>/default when <see cref="IsSuccess"/> is false.</summary>
    public T? Value { get; }

    /// <summary>The failure detail (code + localized messages) when <see cref="IsSuccess"/> is false; <c>null</c> on success.</summary>
    public Error? Error { get; }

    /// <summary>Creates a successful result holding <paramref name="value"/>.</summary>
    /// <param name="value">The value produced by the operation.</param>
    private Result(T value)
    {
        IsSuccess = true;
        Value = value;
    }

    /// <summary>Creates a failed result holding <paramref name="error"/>.</summary>
    /// <param name="error">The failure detail.</param>
    private Result(Error error)
    {
        IsSuccess = false;
        Error = error;
    }

    /// <summary>Creates a successful result wrapping the given value.</summary>
    /// <param name="value">The value produced by the operation.</param>
    /// <returns>A result whose <see cref="IsSuccess"/> is true and <see cref="Value"/> is <paramref name="value"/>.</returns>
    public static Result<T> Ok(T value) => new(value);

    /// <summary>Creates a failed result from an error code and localized messages; no exception is thrown.</summary>
    /// <param name="code">Stable error key (see <see cref="Asdamir.Core.ErrorHandling.Domain.ErrorCodes"/>) used to map to a localized message.</param>
    /// <param name="messageTr">Turkish (tr-TR) message.</param>
    /// <param name="messageEn">English (en-US) message; optional.</param>
    /// <param name="messageRu">Russian (ru-RU) message; optional.</param>
    /// <returns>A result whose <see cref="IsSuccess"/> is false and <see cref="Error"/> carries the code/messages.</returns>
    public static Result<T> Fail(string code, string messageTr, string messageEn = "", string messageRu = "") => new(new Error(code, messageTr, messageEn, messageRu));

    /// <summary>Creates a failed result from an already-built <see cref="Error"/>; no exception is thrown.</summary>
    /// <param name="error">The failure detail to carry.</param>
    /// <returns>A result whose <see cref="IsSuccess"/> is false and <see cref="Error"/> is <paramref name="error"/>.</returns>
    public static Result<T> Fail(Error error) => new(error);
}

/// <summary>
/// The valueless counterpart of <see cref="Result{T}"/>: a no-throw outcome for operations that return
/// nothing on success. Models EXPECTED failures as data (the two-channel model) instead of exceptions;
/// callers branch on <see cref="IsSuccess"/>. Construct via the <c>Ok</c>/<c>Fail</c> factories.
/// </summary>
public class Result
{
    /// <summary>True when the operation succeeded; false means <see cref="Error"/> is populated with the failure detail.</summary>
    public bool IsSuccess { get; }

    /// <summary>The failure detail (code + localized messages) when <see cref="IsSuccess"/> is false; <c>null</c> on success.</summary>
    public Error? Error { get; }

    /// <summary>Creates a successful (no-value) result.</summary>
    private Result()
    {
        IsSuccess = true;
    }

    /// <summary>Creates a failed result holding <paramref name="error"/>.</summary>
    /// <param name="error">The failure detail.</param>
    private Result(Error error)
    {
        IsSuccess = false;
        Error = error;
    }

    /// <summary>Creates a successful result for an operation that produces no value.</summary>
    /// <returns>A result whose <see cref="IsSuccess"/> is true.</returns>
    public static Result Ok() => new();

    /// <summary>Creates a failed result from an error code and localized messages; no exception is thrown.</summary>
    /// <param name="code">Stable error key (see <see cref="Asdamir.Core.ErrorHandling.Domain.ErrorCodes"/>) used to map to a localized message.</param>
    /// <param name="messageTr">Turkish (tr-TR) message.</param>
    /// <param name="messageEn">English (en-US) message; optional.</param>
    /// <param name="messageRu">Russian (ru-RU) message; optional.</param>
    /// <returns>A result whose <see cref="IsSuccess"/> is false and <see cref="Error"/> carries the code/messages.</returns>
    public static Result Fail(string code, string messageTr, string messageEn = "", string messageRu = "") => new(new Error(code, messageTr, messageEn, messageRu));

    /// <summary>Creates a failed result from an already-built <see cref="Error"/>; no exception is thrown.</summary>
    /// <param name="error">The failure detail to carry.</param>
    /// <returns>A result whose <see cref="IsSuccess"/> is false and <see cref="Error"/> is <paramref name="error"/>.</returns>
    public static Result Fail(Error error) => new(error);
}
