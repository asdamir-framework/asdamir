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

/// <summary>Sends transactional emails (e.g. password reset). Returns whether the send succeeded.</summary>
public interface IEmailService
{
    /// <summary>Sends the password-reset email (token embedded in <paramref name="resetUrl"/>); true if sent.</summary>
    Task<bool> SendPasswordResetEmailAsync(string toEmail, string resetToken, string resetUrl);
    /// <summary>Sends an HTML email; true if sent.</summary>
    Task<bool> SendEmailAsync(string toEmail, string subject, string htmlBody);
}
