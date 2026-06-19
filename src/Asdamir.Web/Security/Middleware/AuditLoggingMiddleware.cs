// Copyright (C) 2026 Orhan Özşahin — Asdamir.
// Licensed under the GNU Lesser General Public License v3.0. See LICENSE.
// SPDX-License-Identifier: LGPL-3.0-or-later
//
// This file is part of the Asdamir open core. It is free software: you can redistribute it
// and/or modify it under the terms of the GNU Lesser General Public License as published by the
// Free Software Foundation, either version 3 of the License, or (at your option) any later
// version. It is distributed WITHOUT ANY WARRANTY; without even the implied warranty of
// MERCHANTABILITY or FITNESS FOR A PARTICULAR PURPOSE. See the GNU LGPL for more details.

using Asdamir.Web.Security.Options;
using Asdamir.Core.Attributes;
using System.Text.Encodings.Web;
using Asdamir.Core.Contracts;
using Microsoft.AspNetCore.Builder;
using Microsoft.AspNetCore.Http;
using Microsoft.Extensions.Options;
using System.Text.Json;
using System.Net;
using Asdamir.Core.MultiTenancy;
using Microsoft.Extensions.DependencyInjection;
using System.Text;

namespace Asdamir.Web.Security.Middleware;

/// <summary>
/// Automatic audit logging of API requests based on the <see cref="AuditLogAttribute"/>.
///
/// Hardened against the v1 audit findings:
///  - Verbose 🔴 Info logs that echoed raw request bodies are gone. The only
///    body content reachable from logs now flows through <see cref="RemoveSensitiveProperties"/>
///    + the always-redact list and is at Debug level.
///  - EnableBuffering() used to read an unbounded request stream. We now cap the
///    body at <see cref="AuditLoggingOptions.MaxRequestBodyBytes"/> and answer 413
///    if the client exceeds it.
///  - X-Forwarded-For was trusted blindly. We now only honor it when the immediate
///    peer is in <see cref="AuditLoggingOptions.TrustedProxies"/>.
///  - JSON writer used <c>UnsafeRelaxedJsonEscaping</c>, which encodes characters
///    like &lt;, &gt;, &amp; as raw bytes. If audit JSON is ever rendered into HTML
///    (admin viewer), that's an XSS amplifier. We use <see cref="JavaScriptEncoder.Default"/>.
///  - tenantId was hardcoded to "1". We resolve it via <see cref="ITenantContext"/>
///    when registered, falling back to "default".
/// </summary>
public class AuditLoggingMiddleware
{
    private readonly RequestDelegate _next;
    private readonly ILogger<AuditLoggingMiddleware> _logger;
    private readonly AuditLoggingOptions _options;
    private readonly IReadOnlyList<IPAddress> _trustedProxies;
    private readonly HashSet<string> _alwaysRedact;

    public AuditLoggingMiddleware(
        RequestDelegate next,
        ILogger<AuditLoggingMiddleware> logger,
        IOptions<AuditLoggingOptions>? options = null)
    {
        _next = next;
        _logger = logger;
        _options = options?.Value ?? new AuditLoggingOptions();
        _trustedProxies = _options.GetTrustedProxyAddresses();
        _alwaysRedact = new HashSet<string>(_options.AlwaysRedactProperties, StringComparer.OrdinalIgnoreCase);
    }

    public async Task InvokeAsync(HttpContext context)
    {
        var auditLogService = context.RequestServices.GetService<IAuditLogService>();
        var endpoint = context.GetEndpoint();
        var auditAttribute = endpoint?.Metadata.GetMetadata<AuditLogAttribute>();

        if (auditAttribute == null || auditLogService == null)
        {
            await _next(context);
            return;
        }

        if (context.Request.Path.StartsWithSegments("/gateway/audit"))
        {
            await _next(context);
            return;
        }

        if (auditAttribute.RequireAuthentication && context.User.Identity?.IsAuthenticated != true)
        {
            await _next(context);
            return;
        }

        string? requestBody = null;
        if (auditAttribute.CaptureRequestBody && context.Request.Body.CanRead)
        {
            var (ok, body) = await TryReadBoundedRequestBodyAsync(context);
            if (!ok)
            {
                // Client exceeded the cap. Refuse without invoking the pipeline so a
                // single multi-GB upload can't pin the process.
                context.Response.StatusCode = StatusCodes.Status413PayloadTooLarge;
                return;
            }
            requestBody = body;
        }

        var originalResponseBody = context.Response.Body;
        using var responseBodyStream = new MemoryStream();
        context.Response.Body = responseBodyStream;

        try
        {
            await _next(context);

            if (context.Response.StatusCode >= 200 && context.Response.StatusCode < 300)
            {
                responseBodyStream.Position = 0;
                var responseBody = await ReadResponseBodyAsync(responseBodyStream);
                await LogAuditAsync(context, auditLogService, auditAttribute, requestBody, responseBody);
            }
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "[AuditLoggingMiddleware] Pipeline error during audited request");
            throw;
        }
        finally
        {
            responseBodyStream.Position = 0;
            await responseBodyStream.CopyToAsync(originalResponseBody);
            context.Response.Body = originalResponseBody;
        }
    }

    private async Task<(bool Ok, string? Body)> TryReadBoundedRequestBodyAsync(HttpContext context)
    {
        var max = _options.MaxRequestBodyBytes;
        if (context.Request.ContentLength is long declared && declared > max)
        {
            _logger.LogWarning(
                "[AuditLoggingMiddleware] Rejected request: declared Content-Length {Declared} > limit {Limit}",
                declared, max);
            return (false, null);
        }

        // EnableBuffering with explicit byte threshold + cap. The cap is the same
        // as our read budget, so the framework cannot silently stream more to disk.
        context.Request.EnableBuffering(bufferThreshold: 64 * 1024, bufferLimit: max);

        try
        {
            var buffer = new byte[Math.Min(max, 64 * 1024)];
            using var ms = new MemoryStream();
            int read;
            int total = 0;
            while ((read = await context.Request.Body.ReadAsync(buffer.AsMemory(0, buffer.Length))) > 0)
            {
                total += read;
                if (total > max)
                {
                    _logger.LogWarning(
                        "[AuditLoggingMiddleware] Rejected request: streamed body exceeded {Limit} bytes",
                        max);
                    return (false, null);
                }
                ms.Write(buffer, 0, read);
            }

            context.Request.Body.Position = 0;
            if (ms.Length == 0) return (true, null);
            var body = Encoding.UTF8.GetString(ms.ToArray());
            return (true, string.IsNullOrWhiteSpace(body) ? null : body);
        }
        catch (Exception ex)
        {
            _logger.LogWarning(ex, "[AuditLoggingMiddleware] Failed to read request body; continuing without it");
            return (true, null);
        }
    }

    private async Task<string?> ReadResponseBodyAsync(Stream responseBody)
    {
        try
        {
            using var reader = new StreamReader(responseBody, Encoding.UTF8, leaveOpen: true);
            var body = await reader.ReadToEndAsync();
            return string.IsNullOrWhiteSpace(body) ? null : body;
        }
        catch (Exception ex)
        {
            _logger.LogWarning(ex, "[AuditLoggingMiddleware] Failed to read response body");
            return null;
        }
    }

    private async Task LogAuditAsync(
        HttpContext context,
        IAuditLogService auditLogService,
        AuditLogAttribute auditAttribute,
        string? requestBody,
        string? responseBody)
    {
        try
        {
            var (entity, action) = DetermineEntityAndAction(context, auditAttribute);

            if (string.IsNullOrEmpty(entity))
            {
                return;
            }

            var entityId = ExtractEntityId(context, entity);
            var userId = context.User?.FindFirst("sub")?.Value ??
                        context.User?.FindFirst("userId")?.Value ??
                        context.User?.FindFirst(System.Security.Claims.ClaimTypes.NameIdentifier)?.Value;
            var userName = context.User?.Identity?.Name ??
                          context.User?.FindFirst(System.Security.Claims.ClaimTypes.Email)?.Value ??
                          context.User?.FindFirst("name")?.Value;

            var tenantContext = context.RequestServices.GetService<ITenantContext>();
            var tenantId = tenantContext?.TenantId
                ?? context.User?.FindFirst("tenantId")?.Value
                ?? "default";

            // Always strip the always-redact list FIRST, then attribute extras on top.
            // Order matters: if we ran attribute exclusion first against a missing list,
            // a forgotten attribute would leak the password.
            var cleanedRequestBody = RedactRequestBody(requestBody, auditAttribute.ExcludeProperties);

            // For Login endpoint, pull the username from the (already-redacted) body.
            if (entity.Contains("/auth/", StringComparison.OrdinalIgnoreCase) && action == "Login" && string.IsNullOrEmpty(userName) && !string.IsNullOrEmpty(cleanedRequestBody))
            {
                try
                {
                    using var loginRequest = JsonDocument.Parse(cleanedRequestBody);
                    if (loginRequest.RootElement.TryGetProperty("username", out var usernameProp))
                        userName = usernameProp.GetString();
                    else if (loginRequest.RootElement.TryGetProperty("email", out var emailProp))
                        userName = emailProp.GetString();
                }
                catch (JsonException)
                {
                    // Non-JSON body; ignore.
                }
            }

            var ipAddress = GetClientIpAddress(context);

            userName ??= "Anonymous";
            userId ??= "0";

            if (entity.Contains("/auth/", StringComparison.OrdinalIgnoreCase) && action == "Login" && !string.IsNullOrEmpty(responseBody))
            {
                try
                {
                    using var responseJson = JsonDocument.Parse(responseBody);
                    if (responseJson.RootElement.TryGetProperty("userId", out var userIdProp))
                        userId = userIdProp.GetInt32().ToString();
                }
                catch (JsonException)
                {
                    // Non-JSON response; ignore.
                }
            }

            string extraJson;
            if (entity.Contains("auth", StringComparison.OrdinalIgnoreCase) && action == "Login")
            {
                var twoFactorUsed = false;
                if (!string.IsNullOrEmpty(responseBody))
                {
                    try
                    {
                        using var responseJson = JsonDocument.Parse(responseBody);
                        twoFactorUsed = responseJson.RootElement.TryGetProperty("requiresTwoFactor", out var prop) && prop.GetBoolean();
                    }
                    catch (JsonException) { /* tolerate non-JSON */ }
                }

                var extraData = new
                {
                    Username = userName,
                    LoginTime = DateTime.UtcNow,
                    IpAddress = ipAddress,
                    UserAgent = context.Request.Headers["User-Agent"].ToString(),
                    TwoFactorUsed = twoFactorUsed
                };
                extraJson = JsonSerializer.Serialize(extraData);
            }
            else if (entity.Contains("auth", StringComparison.OrdinalIgnoreCase) && action == "Logout")
            {
                var extraData = new
                {
                    Username = userName,
                    LogoutTime = DateTime.UtcNow,
                    IpAddress = ipAddress,
                    UserAgent = context.Request.Headers["User-Agent"].ToString()
                };
                extraJson = JsonSerializer.Serialize(extraData);
            }
            else
            {
                using var stream = new MemoryStream();
                // Safe encoder: do not allow raw <, >, &, ' in audit JSON. An admin viewer
                // could otherwise render attacker-controlled fields and execute script.
                using var writer = new Utf8JsonWriter(stream, new JsonWriterOptions { Encoder = JavaScriptEncoder.Default });
                writer.WriteStartObject();
                writer.WriteString("Method", context.Request.Method);
                writer.WriteString("Path", context.Request.Path.Value);
                if (auditAttribute.IncludeQueryString && !string.IsNullOrEmpty(context.Request.QueryString.Value))
                {
                    writer.WriteString("QueryString", context.Request.QueryString.Value);
                }
                writer.WriteNumber("StatusCode", context.Response.StatusCode);
                writer.WriteString("IpAddress", ipAddress);
                writer.WriteString("UserAgent", context.Request.Headers["User-Agent"].ToString());

                if (!string.IsNullOrEmpty(cleanedRequestBody))
                {
                    try
                    {
                        using var requestDoc = JsonDocument.Parse(cleanedRequestBody);
                        writer.WritePropertyName("RequestBody");
                        requestDoc.RootElement.WriteTo(writer);
                    }
                    catch (JsonException)
                    {
                        writer.WriteString("RequestBody", cleanedRequestBody);
                    }
                }
                else
                {
                    writer.WriteNull("RequestBody");
                }

                writer.WriteEndObject();
                writer.Flush();
                extraJson = Encoding.UTF8.GetString(stream.ToArray());
            }

            // Login/Logout: never persist the request body — even after redaction the
            // payload structure leaks attempted-username for failed-login attempts on
            // valid endpoints. Audit row keeps the username field separately.
            var newValuesForAudit = (entity.Contains("/auth/", StringComparison.OrdinalIgnoreCase) && (action == "Login" || action == "Logout"))
                ? null
                : cleanedRequestBody;

            var description = context.Items.ContainsKey("AuditDescription")
                ? context.Items["AuditDescription"]?.ToString()
                : null;

            if (string.IsNullOrEmpty(description))
            {
                description = GenerateDescription(entity, action, entityId, userName, requestBody, responseBody);
            }

            await auditLogService.LogActionAsync(
                entity: entity,
                action: action,
                entityId: entityId,
                tenantId: tenantId,
                oldValues: null,
                newValues: newValuesForAudit,
                extra: extraJson,
                userId: userId,
                userName: userName,
                description: description
            );

            _logger.LogDebug("[AuditLoggingMiddleware] Logged audit Entity={Entity} Action={Action} User={User}",
                entity, action, userName);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "[AuditLoggingMiddleware] Error logging audit entry");
            // Swallow — audit must never break the request.
        }
    }

    private (string Entity, string Action) DetermineEntityAndAction(HttpContext context, AuditLogAttribute? attribute)
    {
        var path = context.Request.Path.Value ?? "";
        var method = context.Request.Method;
        var segments = path.Split('/', StringSplitOptions.RemoveEmptyEntries);

        if (segments.Length < 2)
            return (string.Empty, string.Empty);

        var fullUrl = $"{context.Request.Scheme}://{context.Request.Host}{context.Request.Path}";

        if (!string.IsNullOrEmpty(attribute?.Action))
        {
            return (fullUrl, attribute.Action);
        }

        var entitySegment = segments[1];

        if (entitySegment.Equals("auth", StringComparison.OrdinalIgnoreCase) && segments.Length > 2)
        {
            var authAction = segments[2];
            return (fullUrl, char.ToUpperInvariant(authAction[0]) + authAction.Substring(1));
        }

        var action = method switch
        {
            "POST" => "Create",
            "PUT" => "Update",
            "PATCH" => "Update",
            "DELETE" => "Delete",
            "GET" => "View",
            _ => "Action"
        };

        return (fullUrl, action);
    }

    private string? ExtractEntityId(HttpContext context, string entity)
    {
        if (context.Items.TryGetValue("AuditEntityId", out var explicitEntityId) && explicitEntityId != null)
        {
            return explicitEntityId.ToString();
        }

        var path = context.Request.Path.Value ?? "";
        var segments = path.Split('/', StringSplitOptions.RemoveEmptyEntries);

        if (segments.Length > 2 && segments[1].Equals("auth", StringComparison.OrdinalIgnoreCase))
        {
            return segments[2].ToLowerInvariant();
        }

        if (segments.Length >= 3 && (int.TryParse(segments[^1], out _) || Guid.TryParse(segments[^1], out _)))
        {
            return segments[^1];
        }

        if (context.Request.Query.ContainsKey("id"))
        {
            return context.Request.Query["id"];
        }

        if (segments.Length > 0 && segments[^1].Equals("list", StringComparison.OrdinalIgnoreCase))
        {
            return "list";
        }

        if (context.Request.Method == "GET")
        {
            return "list";
        }

        return null;
    }

    private string? RedactRequestBody(string? json, string? attributeExcludeProperties)
    {
        if (string.IsNullOrEmpty(json)) return json;

        var properties = new HashSet<string>(_alwaysRedact, StringComparer.OrdinalIgnoreCase);
        if (!string.IsNullOrEmpty(attributeExcludeProperties))
        {
            foreach (var name in attributeExcludeProperties.Split(',', StringSplitOptions.RemoveEmptyEntries))
                properties.Add(name.Trim());
        }

        try
        {
            using var stream = new MemoryStream();
            using var document = JsonDocument.Parse(json);
            using var writer = new Utf8JsonWriter(stream, new JsonWriterOptions { Encoder = JavaScriptEncoder.Default });

            WriteElementWithExclusions(document.RootElement, writer, properties);
            writer.Flush();

            return Encoding.UTF8.GetString(stream.ToArray());
        }
        catch (JsonException)
        {
            // Body isn't JSON; we cannot redact field-by-field. Don't gamble — drop it.
            return "***REDACTED (non-json body)***";
        }
    }

    private void WriteElementWithExclusions(JsonElement element, Utf8JsonWriter writer, HashSet<string> excludeProperties)
    {
        switch (element.ValueKind)
        {
            case JsonValueKind.Object:
                writer.WriteStartObject();
                foreach (var property in element.EnumerateObject())
                {
                    if (excludeProperties.Contains(property.Name))
                    {
                        writer.WriteString(property.Name, "***REDACTED***");
                    }
                    else
                    {
                        writer.WritePropertyName(property.Name);
                        WriteElementWithExclusions(property.Value, writer, excludeProperties);
                    }
                }
                writer.WriteEndObject();
                break;

            case JsonValueKind.Array:
                writer.WriteStartArray();
                foreach (var item in element.EnumerateArray())
                {
                    WriteElementWithExclusions(item, writer, excludeProperties);
                }
                writer.WriteEndArray();
                break;

            default:
                element.WriteTo(writer);
                break;
        }
    }

    private string GetClientIpAddress(HttpContext context)
    {
        // Only honor X-Forwarded-For when the immediate peer is a trusted proxy.
        // Otherwise any direct client can set any IP and our audit log lies.
        var peer = context.Connection?.RemoteIpAddress;
        if (peer is not null && _trustedProxies.Any(p => p.Equals(peer)))
        {
            var xff = context.Request.Headers["X-Forwarded-For"].FirstOrDefault();
            if (!string.IsNullOrEmpty(xff))
            {
                // Left-most non-proxy IP per RFC 7239.
                var ips = xff.Split(',', StringSplitOptions.RemoveEmptyEntries);
                if (ips.Length > 0)
                {
                    var ip = ips[0].Trim();
                    return Normalize(ip);
                }
            }
            var xRealIp = context.Request.Headers["X-Real-IP"].FirstOrDefault();
            if (!string.IsNullOrEmpty(xRealIp))
                return Normalize(xRealIp);
        }

        return Normalize(peer?.ToString() ?? "Unknown");

        static string Normalize(string raw) => raw == "::1" ? "127.0.0.1" : raw;
    }

    private string GenerateDescription(string entity, string action, string? entityId, string userName, string? requestBody, string? responseBody)
    {
        try
        {
            var entityName = ExtractEntityNameFromUrl(entity);

            if (entityName.Contains("auth", StringComparison.OrdinalIgnoreCase) ||
                entityName.Contains("authentication", StringComparison.OrdinalIgnoreCase))
            {
                return action switch
                {
                    "Login" => $"{userName} logged in successfully",
                    "Logout" => $"{userName} logged out",
                    "Refresh" => $"{userName} refreshed authentication token",
                    "TwoFactor" => $"{userName} completed two-factor authentication",
                    _ => $"{userName} performed {action} on authentication"
                };
            }

            if (entityName.Contains("workorder", StringComparison.OrdinalIgnoreCase) &&
                !entityName.Contains("stats", StringComparison.OrdinalIgnoreCase))
            {
                if (action == "List")
                {
                    if (!string.IsNullOrEmpty(responseBody))
                    {
                        try
                        {
                            using var responseDoc = JsonDocument.Parse(responseBody);
                            if (responseDoc.RootElement.TryGetProperty("total", out var totalProp))
                            {
                                var total = totalProp.GetInt32();
                                return $"{userName} viewed {total} work orders";
                            }
                        }
                        catch (JsonException) { /* tolerate */ }
                    }
                    return $"{userName} viewed work order list";
                }

                return action switch
                {
                    "View" => $"{userName} viewed work order {entityId ?? "details"}",
                    "Create" => $"{userName} created a new work order",
                    "Update" => $"{userName} updated work order {entityId}",
                    "Delete" => $"{userName} deleted work order {entityId}",
                    _ => $"{userName} performed {action} on work order"
                };
            }

            if (entityName.Contains("workorderstats", StringComparison.OrdinalIgnoreCase) ||
                entityName.Contains("workorder-stats", StringComparison.OrdinalIgnoreCase))
            {
                return $"{userName} viewed work order statistics {(string.IsNullOrEmpty(entityId) ? "" : $"for {entityId}")}".Trim();
            }

            if (entityName.Contains("user", StringComparison.OrdinalIgnoreCase))
            {
                return action switch
                {
                    "List" => $"{userName} viewed user list",
                    "View" => $"{userName} viewed user details {entityId}",
                    "Create" => $"{userName} created a new user",
                    "Update" => $"{userName} updated user {entityId}",
                    "Delete" => $"{userName} deleted user {entityId}",
                    _ => $"{userName} performed {action} on user"
                };
            }

            if (entityName.Contains("menu", StringComparison.OrdinalIgnoreCase))
            {
                return action switch
                {
                    "List" => $"{userName} viewed menu list",
                    "View" => $"{userName} viewed menu details",
                    "Update" => $"{userName} updated menu configuration",
                    _ => $"{userName} performed {action} on menu"
                };
            }

            var displayName = string.IsNullOrWhiteSpace(entityId) ? entityName : $"{entityName} {entityId}";
            return action switch
            {
                "List" => $"{userName} viewed {entityName} list",
                "View" => $"{userName} viewed {displayName}",
                "Create" => $"{userName} created a new {entityName}",
                "Update" => $"{userName} updated {displayName}",
                "Delete" => $"{userName} deleted {displayName}",
                _ => $"{userName} performed {action} on {entityName}"
            };
        }
        catch (Exception ex)
        {
            _logger.LogWarning(ex, "[AuditLoggingMiddleware] Failed to generate description");
            return $"{userName} performed {action}";
        }
    }

    private string ExtractEntityNameFromUrl(string entity)
    {
        try
        {
            var uri = new Uri(entity);
            var segments = uri.AbsolutePath.Split('/', StringSplitOptions.RemoveEmptyEntries);

            if (segments.Length == 0)
                return "resource";

            for (int i = segments.Length - 1; i >= 0; i--)
            {
                var segment = segments[i];
                if (!Guid.TryParse(segment, out _) && !int.TryParse(segment, out _))
                {
                    return segment.Replace("-management", "").Replace("-", " ");
                }
            }

            return segments[^1];
        }
        catch (Exception ex)
        {
            _logger.LogDebug(ex, "[AuditLoggingMiddleware] ExtractEntityNameFromUrl failed for entity='{Entity}'", entity);
            return "resource";
        }
    }
}

public static class AuditLoggingMiddlewareExtensions
{
    public static IApplicationBuilder UseAuditLogging(this IApplicationBuilder builder)
    {
        return builder.UseMiddleware<AuditLoggingMiddleware>();
    }
}
