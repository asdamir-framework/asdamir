// Copyright (C) 2026 Orhan Özşahin — Asdamir.
// Licensed under the GNU Lesser General Public License v3.0. See LICENSE.
// SPDX-License-Identifier: LGPL-3.0-or-later
//
// This file is part of the Asdamir open core. It is free software: you can redistribute it
// and/or modify it under the terms of the GNU Lesser General Public License as published by the
// Free Software Foundation, either version 3 of the License, or (at your option) any later
// version. It is distributed WITHOUT ANY WARRANTY; without even the implied warranty of
// MERCHANTABILITY or FITNESS FOR A PARTICULAR PURPOSE. See the GNU LGPL for more details.

using Asdamir.Core.Contracts;
using Asdamir.Core.Dtos;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.Logging;
using System.Net;
using System.Net.Http.Json;

namespace Asdamir.Core.Services;

/// <summary>
/// Service for managing SMS WhiteList operations via SMS Gateway API
/// </summary>
public sealed class WhiteListService : IWhiteListService
{
    private readonly HttpClient _httpClient;
    private readonly IConfiguration _configuration;
    private readonly ILogger<WhiteListService> _logger;
    private readonly string _addWhiteListUrl;
    private readonly string _deactivateWhiteListUrl;
    private readonly string _staticKey;

    public WhiteListService(
        HttpClient httpClient,
        IConfiguration configuration,
        ILogger<WhiteListService> logger)
    {
        _httpClient = httpClient ?? throw new ArgumentNullException(nameof(httpClient));
        _configuration = configuration ?? throw new ArgumentNullException(nameof(configuration));
        _logger = logger ?? throw new ArgumentNullException(nameof(logger));

        _addWhiteListUrl = _configuration["Sms:SmsGatewayUrlAddWhiteList"] 
            ?? throw new InvalidOperationException("Sms:SmsGatewayUrlAddWhiteList configuration is missing");
        _deactivateWhiteListUrl = _configuration["Sms:SmsGatewayUrlDeactivateWhiteList"] 
            ?? throw new InvalidOperationException("Sms:SmsGatewayUrlDeactivateWhiteList configuration is missing");
        _staticKey = _configuration["Sms:SmsStaticKey"] ?? string.Empty;
    }

    // Audit fix: requests respect a single timeout source. v1 imposed a 30 s CTS on
    // each call AND the injected HttpClient may have its own Timeout; whichever fired
    // first triggered cancellation, producing OperationCanceledException with no clue
    // which one fired. We now link the caller's token with a per-call CTS only if the
    // injected client doesn't already have a finite timeout — single source of truth.
    private static readonly TimeSpan DefaultPerCallTimeout = TimeSpan.FromSeconds(30);

    /// <summary>
    /// Adds a phone number to the SMS WhiteList asynchronously.
    /// </summary>
    public async Task<InsertWhiteListResponse> InsertWhiteListAsync(SendInsertWhiteListRequest request, CancellationToken cancellationToken = default)
    {
        try
        {
            if (string.IsNullOrWhiteSpace(request.Phone))
            {
                _logger.LogWarning("[WhiteListService] InsertWhiteListAsync - Phone number is empty");
                return new InsertWhiteListResponse
                {
                    Result = InsertWhiteListResult.Failed,
                    Message = "Telefon numarası boş olamaz"
                };
            }

            _logger.LogInformation("[WhiteListService] InsertWhiteListAsync - Phone: {Phone}, Email: {Email}, CreateUser: {CreateUser}",
                request.Phone, request.Email, request.CreateUser);

            var payload = new
            {
                Phone = request.Phone,
                Email = request.Email,
                CreateUser = request.CreateUser
            };

            using var requestMessage = new HttpRequestMessage(HttpMethod.Post, _addWhiteListUrl);
            
            // Add StaticKey header if configured
            if (!string.IsNullOrWhiteSpace(_staticKey))
            {
                requestMessage.Headers.Add("x-api-key", _staticKey);
            }

            requestMessage.Content = JsonContent.Create(payload);

            using var cts = CreateCallScopedCts(cancellationToken);
            var response = await _httpClient.SendAsync(requestMessage, cts.Token).ConfigureAwait(false);
            var responseContent = await response.Content.ReadAsStringAsync(cts.Token).ConfigureAwait(false);

            if (response.IsSuccessStatusCode)
            {
                _logger.LogInformation("[WhiteListService] Successfully added to whitelist - Phone: {Phone}", request.Phone);
                return new InsertWhiteListResponse
                {
                    Result = InsertWhiteListResult.Created,
                    Message = "Whitelist'e başarıyla eklendi"
                };
            }
            else if (response.StatusCode == HttpStatusCode.Conflict)
            {
                _logger.LogInformation("[WhiteListService] Phone already exists in whitelist - Phone: {Phone}", request.Phone);
                return new InsertWhiteListResponse
                {
                    Result = InsertWhiteListResult.AlreadyExists,
                    Message = "Bu telefon zaten whitelist'te mevcut"
                };
            }
            else
            {
                _logger.LogWarning("[WhiteListService] Failed to add to whitelist - Phone: {Phone}, Status: {StatusCode}, Response: {Response}",
                    request.Phone, response.StatusCode, responseContent);
                return new InsertWhiteListResponse
                {
                    Result = InsertWhiteListResult.Failed,
                    Message = $"API hatası: {response.StatusCode} - {responseContent}"
                };
            }
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "[WhiteListService] Exception in InsertWhiteListAsync - Phone: {Phone}", request.Phone);
            return new InsertWhiteListResponse
            {
                Result = InsertWhiteListResult.Failed,
                Message = $"Hata: {ex.Message}"
            };
        }
    }

    /// <summary>
    /// Deactivates a phone number from the SMS WhiteList asynchronously.
    /// </summary>
    public async Task<bool> DeactivateWhiteListAsync(DeactivateWhiteListRequest request, CancellationToken cancellationToken = default)
    {
        try
        {
            if (string.IsNullOrWhiteSpace(request.Phone))
            {
                _logger.LogWarning("[WhiteListService] DeactivateWhiteListAsync - Phone number is empty");
                return false;
            }

            _logger.LogInformation("[WhiteListService] DeactivateWhiteListAsync - Phone: {Phone}, UpdateUser: {UpdateUser}",
                request.Phone, request.UpdateUser);

            var payload = new
            {
                Phone = request.Phone,
                UpdateUser = request.UpdateUser
            };

            using var requestMessage = new HttpRequestMessage(HttpMethod.Post, _deactivateWhiteListUrl);
            
            // Add StaticKey header if configured
            if (!string.IsNullOrWhiteSpace(_staticKey))
            {
                requestMessage.Headers.Add("x-api-key", _staticKey);
            }

            requestMessage.Content = JsonContent.Create(payload);

            using var cts = CreateCallScopedCts(cancellationToken);
            var response = await _httpClient.SendAsync(requestMessage, cts.Token).ConfigureAwait(false);

            if (response.IsSuccessStatusCode)
            {
                _logger.LogInformation("[WhiteListService] Successfully deactivated from whitelist - Phone: {Phone}", request.Phone);
                return true;
            }
            else
            {
                var responseContent = await response.Content.ReadAsStringAsync(cts.Token).ConfigureAwait(false);
                _logger.LogWarning("[WhiteListService] Failed to deactivate from whitelist - Phone: {Phone}, Status: {StatusCode}, Response: {Response}",
                    request.Phone, response.StatusCode, responseContent);
                return false;
            }
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "[WhiteListService] Exception in DeactivateWhiteListAsync - Phone: {Phone}", request.Phone);
            return false;
        }
    }

    private CancellationTokenSource CreateCallScopedCts(CancellationToken caller)
    {
        // If the injected HttpClient already has a finite Timeout, do not impose a
        // second one — let HttpClient win. Otherwise link the caller's token with
        // our default to bound the wait. Caller cancellation propagates either way.
        var clientHasFiniteTimeout = _httpClient.Timeout != Timeout.InfiniteTimeSpan;
        if (clientHasFiniteTimeout)
        {
            return CancellationTokenSource.CreateLinkedTokenSource(caller);
        }
        var cts = CancellationTokenSource.CreateLinkedTokenSource(caller);
        cts.CancelAfter(DefaultPerCallTimeout);
        return cts;
    }

    // Audit fix: the synchronous InsertWhiteListSync / DeactivateWhiteListSync overloads were
    // removed. They wrapped HttpClient.SendAsync(...).GetAwaiter().GetResult(), which causes
    // thread-pool starvation under load (Blazor Server circuit, Hangfire job, ASP.NET request).
    // All call sites must use the async API.
}
