// Copyright (C) 2026 Orhan Özşahin — Asdamir.
// Licensed under the GNU Lesser General Public License v3.0. See LICENSE.
// SPDX-License-Identifier: LGPL-3.0-or-later
//
// This file is part of the Asdamir open core. It is free software: you can redistribute it
// and/or modify it under the terms of the GNU Lesser General Public License as published by the
// Free Software Foundation, either version 3 of the License, or (at your option) any later
// version. It is distributed WITHOUT ANY WARRANTY; without even the implied warranty of
// MERCHANTABILITY or FITNESS FOR A PARTICULAR PURPOSE. See the GNU LGPL for more details.

using Asdamir.Core.Dtos;

namespace Asdamir.Core.Contracts;

public interface IUserRepository
{
    Task<UserDto?> GetByEmailAsync(string email);
    Task<List<UserDto>> ListAsync();
    Task<UserDto?> GetAsync(int id);
    Task<UserDto> CreateAsync(CreateUserRequestDto request);
    Task<UserDto?> UpdateAsync(int id, UpdateUserRequestDto request);
    Task DeleteAsync(int id);
    Task<UserDto> ToggleStatusAsync(int id);
    Task<UserDto> Toggle2FAAsync(int id);
    Task ResetPasswordAsync(int id, ResetPasswordRequestDto request);
    Task<IEnumerable<string>> GetPermissionsAsync(int userId);
    Task UpsertRefreshTokenAsync(int userId, string refreshToken, DateTime expiresAtUtc);
    Task<UserDto?> GetByRefreshTokenAsync(string refreshToken);
    Task UpdateLastLoginAsync(int userId);
    
    // Password Reset
    Task<(bool Success, UserDto? User)> CreatePasswordResetTokenAsync(string email, string token, int expiryHours = 1);
    Task<(bool Success, UserDto? User, string? ErrorCode)> ResetPasswordWithTokenAsync(string token, string newPasswordHash);
    Task<(bool IsValid, UserDto? User)> ValidatePasswordResetTokenAsync(string token);
    
    // Additional methods for management
    Task<List<UserDto>> GetAllAsync(int page = 1, int pageSize = 50, string? search = null);
    Task<UserStatisticsDto> GetStatisticsAsync();
}
