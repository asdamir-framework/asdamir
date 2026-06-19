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
/// Represents the result of an operation that can either succeed or fail
/// </summary>
/// <typeparam name="T">Type of the value when successful</typeparam>
public class Result<T>
{
    public bool IsSuccess { get; }
    public T? Value { get; }
    public Error? Error { get; }

    private Result(T value) 
    { 
        IsSuccess = true; 
        Value = value; 
    }
    
    private Result(Error error) 
    { 
        IsSuccess = false; 
        Error = error; 
    }

    public static Result<T> Ok(T value) => new(value);
    public static Result<T> Fail(string code, string messageTr, string messageEn = "", string messageRu = "") => new(new Error(code, messageTr, messageEn, messageRu));
    public static Result<T> Fail(Error error) => new(error);
}

/// <summary>
/// Represents the result of an operation that can either succeed or fail (void return)
/// </summary>
public class Result
{
    public bool IsSuccess { get; }
    public Error? Error { get; }

    private Result() 
    { 
        IsSuccess = true; 
    }
    
    private Result(Error error) 
    { 
        IsSuccess = false; 
        Error = error; 
    }

    public static Result Ok() => new();
    public static Result Fail(string code, string messageTr, string messageEn = "", string messageRu = "") => new(new Error(code, messageTr, messageEn, messageRu));
    public static Result Fail(Error error) => new(error);
}
