// Copyright (C) 2026 Orhan Özşahin — Asdamir.
// Licensed under the GNU Lesser General Public License v3.0. See LICENSE.
// SPDX-License-Identifier: LGPL-3.0-or-later
//
// This file is part of the Asdamir open core. It is free software: you can redistribute it
// and/or modify it under the terms of the GNU Lesser General Public License as published by the
// Free Software Foundation, either version 3 of the License, or (at your option) any later
// version. It is distributed WITHOUT ANY WARRANTY; without even the implied warranty of
// MERCHANTABILITY or FITNESS FOR A PARTICULAR PURPOSE. See the GNU LGPL for more details.

namespace Asdamir.Core.ErrorHandling.Domain;

/// <summary>
/// Represents a domain-specific exception with an error code
/// </summary>
public class DomainException : Exception
{
    public string Code { get; }
    
    public DomainException(string code, string message) : base(message) 
    { 
        Code = code; 
    }
    
    public DomainException(string code, string message, Exception innerException) : base(message, innerException) 
    { 
        Code = code; 
    }
}
