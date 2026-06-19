// Copyright (C) 2026 Orhan Özşahin — Asdamir.
// Licensed under the GNU Lesser General Public License v3.0. See LICENSE.
// SPDX-License-Identifier: LGPL-3.0-or-later
//
// This file is part of the Asdamir open core. It is free software: you can redistribute it
// and/or modify it under the terms of the GNU Lesser General Public License as published by the
// Free Software Foundation, either version 3 of the License, or (at your option) any later
// version. It is distributed WITHOUT ANY WARRANTY; without even the implied warranty of
// MERCHANTABILITY or FITNESS FOR A PARTICULAR PURPOSE. See the GNU LGPL for more details.

using Asdamir.Core.Models;

namespace Asdamir.Core.Contracts;

public interface IAppLogRepository
{
    Task<long> CreateAsync(AppLogCreateRequest request);
    Task<List<AppLog>> GetRecentAsync(int count = 100);
    Task<List<AppLog>> GetByLevelAsync(string level, int count = 100);
    Task<List<AppLog>> GetBySourceAsync(string source, int count = 100);
    Task<List<AppLog>> GetByDateRangeAsync(DateTime from, DateTime to, int count = 100);
    Task<AppLog?> GetByIdAsync(long id);
    Task DeleteOldLogsAsync(int retentionDays = 30);
}
