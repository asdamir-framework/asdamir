// Copyright (C) 2026 Orhan Özşahin — Asdamir.
// Licensed under the GNU Lesser General Public License v3.0. See LICENSE.
// SPDX-License-Identifier: LGPL-3.0-or-later
//
// This file is part of the Asdamir open core. It is free software: you can redistribute it
// and/or modify it under the terms of the GNU Lesser General Public License as published by the
// Free Software Foundation, either version 3 of the License, or (at your option) any later
// version. It is distributed WITHOUT ANY WARRANTY; without even the implied warranty of
// MERCHANTABILITY or FITNESS FOR A PARTICULAR PURPOSE. See the GNU LGPL for more details.

using Polly;
using Polly.Retry;
using Polly.CircuitBreaker;
using System.Net;

namespace Asdamir.Core.ErrorHandling.Resilience;

/// <summary>
/// HTTP client specific resilience policies
/// </summary>
public static class HttpPolicies
{
    /// <summary>
    /// Creates a retry policy with exponential backoff for HTTP operations
    /// </summary>
    /// <param name="retries">Number of retry attempts (default: 3)</param>
    /// <returns>Async retry policy for HTTP operations</returns>
    public static AsyncRetryPolicy<HttpResponseMessage> WithExponentialBackoff(int retries = 3)
    {
        return Policy
            .Handle<HttpRequestException>()
            .Or<TaskCanceledException>()
            .OrResult<HttpResponseMessage>(response => IsTransientHttpResponse(response))
            .WaitAndRetryAsync(
                retries,
                retryAttempt => TimeSpan.FromMilliseconds(200 * Math.Pow(2, retryAttempt)) +
                               TimeSpan.FromMilliseconds(Random.Shared.Next(0, 200)));
    }

    /// <summary>
    /// Creates a circuit breaker policy for HTTP operations
    /// </summary>
    /// <param name="exceptionsAllowedBeforeBreaking">Number of exceptions before breaking (default: 5)</param>
    /// <param name="durationOfBreak">Duration to keep circuit open (default: 30 seconds)</param>
    /// <returns>Circuit breaker policy for HTTP operations</returns>
    public static AsyncCircuitBreakerPolicy<HttpResponseMessage> WithCircuitBreaker(
        int exceptionsAllowedBeforeBreaking = 5,
        TimeSpan? durationOfBreak = null)
    {
        return Policy
            .Handle<HttpRequestException>()
            .Or<TaskCanceledException>()
            .OrResult<HttpResponseMessage>(response => IsTransientHttpResponse(response))
            .CircuitBreakerAsync(
                exceptionsAllowedBeforeBreaking,
                durationOfBreak ?? TimeSpan.FromSeconds(30));
    }

    /// <summary>
    /// Creates a combined policy with retry and circuit breaker
    /// </summary>
    /// <param name="retries">Number of retry attempts (default: 3)</param>
    /// <param name="exceptionsAllowedBeforeBreaking">Number of exceptions before breaking (default: 5)</param>
    /// <param name="durationOfBreak">Duration to keep circuit open (default: 30 seconds)</param>
    /// <returns>Combined retry and circuit breaker policy</returns>
    public static IAsyncPolicy<HttpResponseMessage> WithRetryAndCircuitBreaker(
        int retries = 3,
        int exceptionsAllowedBeforeBreaking = 5,
        TimeSpan? durationOfBreak = null)
    {
        var retryPolicy = WithExponentialBackoff(retries);
        var circuitBreakerPolicy = WithCircuitBreaker(exceptionsAllowedBeforeBreaking, durationOfBreak);
        
        return retryPolicy.WrapAsync(circuitBreakerPolicy);
    }

    private static bool IsTransientHttpResponse(HttpResponseMessage response)
    {
        return response.StatusCode switch
        {
            HttpStatusCode.RequestTimeout => true,
            HttpStatusCode.TooManyRequests => true,
            HttpStatusCode.InternalServerError => true,
            HttpStatusCode.BadGateway => true,
            HttpStatusCode.ServiceUnavailable => true,
            HttpStatusCode.GatewayTimeout => true,
            _ => false
        };
    }
}
