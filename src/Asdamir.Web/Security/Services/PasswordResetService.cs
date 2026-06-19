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
using System.Net.Http.Json;

namespace Asdamir.Web.Security.Services;

public class PasswordResetService : IPasswordResetService
{
    private readonly IHttpClientFactory _httpClientFactory;
    private readonly ILogger<PasswordResetService> _logger;

    public PasswordResetService(IHttpClientFactory httpClientFactory, ILogger<PasswordResetService> logger)
    {
        _httpClientFactory = httpClientFactory;
        _logger = logger;
    }

    public async Task<(bool Success, string Message)> RequestPasswordResetAsync(string email)
    {
        try
        {
            var client = _httpClientFactory.CreateClient("GatewayNoAuth");
            // Get current culture from thread
            var currentCulture = System.Globalization.CultureInfo.CurrentCulture.Name;
            var request = new ForgotPasswordRequestDto(email, currentCulture);
            var response = await client.PostAsJsonAsync("gateway/auth/forgot-password", request);

            if (!response.IsSuccessStatusCode)
            {
                var errorContent = await response.Content.ReadAsStringAsync();
                _logger.LogWarning("Password reset request failed - Status: {StatusCode}, Response: {Response}", 
                    response.StatusCode, errorContent);
                return (false, "Failed to send password reset email");
            }

            var result = await response.Content.ReadFromJsonAsync<ForgotPasswordResponseDto>();
            return (true, result?.Message ?? "Password reset email sent");
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error requesting password reset for email: {Email}", email);
            return (false, "An error occurred while processing your request");
        }
    }

    public async Task<(bool IsValid, string? Email)> ValidateResetTokenAsync(string token)
    {
        try
        {
            var client = _httpClientFactory.CreateClient("GatewayNoAuth");
            var request = new ValidateResetTokenRequestDto(token);
            var response = await client.PostAsJsonAsync("gateway/auth/validate-reset-token", request);

            if (!response.IsSuccessStatusCode)
            {
                _logger.LogWarning("Token validation failed - Status: {StatusCode}", response.StatusCode);
                return (false, null);
            }

            var result = await response.Content.ReadFromJsonAsync<ValidateResetTokenResponseDto>();
            return (result?.IsValid ?? false, result?.Email);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error validating reset token");
            return (false, null);
        }
    }

    public async Task<(bool Success, string Message)> ResetPasswordAsync(string token, string newPassword)
    {
        try
        {
            var client = _httpClientFactory.CreateClient("GatewayNoAuth");
            var request = new ResetPasswordWithTokenRequestDto(token, newPassword);
            var response = await client.PostAsJsonAsync("gateway/auth/reset-password", request);

            if (!response.IsSuccessStatusCode)
            {
                var errorContent = await response.Content.ReadAsStringAsync();
                _logger.LogWarning("Password reset failed - Status: {StatusCode}, Response: {Response}", 
                    response.StatusCode, errorContent);
                return (false, "Failed to reset password");
            }

            var result = await response.Content.ReadFromJsonAsync<ResetPasswordWithTokenResponseDto>();
            return (result?.Success ?? false, result?.Message ?? "Password reset successful");
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error resetting password");
            return (false, "An error occurred while processing your request");
        }
    }
}
