// Copyright (C) 2026 Orhan Özşahin — Asdamir.
// Licensed under the GNU Lesser General Public License v3.0. See LICENSE.
// SPDX-License-Identifier: LGPL-3.0-or-later
//
// This file is part of the Asdamir open core. It is free software: you can redistribute it
// and/or modify it under the terms of the GNU Lesser General Public License as published by the
// Free Software Foundation, either version 3 of the License, or (at your option) any later
// version. It is distributed WITHOUT ANY WARRANTY; without even the implied warranty of
// MERCHANTABILITY or FITNESS FOR A PARTICULAR PURPOSE. See the GNU LGPL for more details.

using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;
using System.Reflection;
using Microsoft.Extensions.Configuration;

namespace Asdamir.Web.Security.Analyzers;

/// <summary>
/// Enterprise-grade code quality and security analyzer
/// Scans the application for security vulnerabilities and code quality issues
/// </summary>
public class SecurityCodeAnalyzer
{
    private readonly ILogger<SecurityCodeAnalyzer> _logger;
    private readonly IServiceProvider _serviceProvider;
    private readonly List<SecurityViolation> _violations = new();

    /// <summary>
    /// Creates the analyzer.
    /// </summary>
    /// <param name="logger">Sink for analysis progress and for surfacing detected violations.</param>
    /// <param name="serviceProvider">Runtime service provider inspected to determine which security services, configuration, and middleware are actually registered.</param>
    public SecurityCodeAnalyzer(ILogger<SecurityCodeAnalyzer> logger, IServiceProvider serviceProvider)
    {
        _logger = logger;
        _serviceProvider = serviceProvider;
    }

    /// <summary>
    /// Runs the full battery of checks (authentication, controller/component authorization,
    /// configuration, dependency injection, and middleware) against the running process and
    /// returns a fresh, aggregated result. Each call clears prior findings, so the result
    /// reflects only this run. Never throws for a failed individual check — per-area errors are
    /// logged as warnings and the scan continues.
    /// </summary>
    /// <returns>The aggregated findings, severity counts, elapsed time, and computed security score.</returns>
    public async Task<SecurityAnalysisResult> AnalyzeAsync()
    {
        _violations.Clear();
        var startTime = DateTime.UtcNow;

        _logger.LogInformation("🔍 Starting enterprise security analysis...");

        // 1. Authentication & Authorization Analysis
        await AnalyzeAuthenticationAsync();
        
        // 2. Controller Security Analysis
        await AnalyzeControllersAsync();
        
        // 3. Component Security Analysis
        await AnalyzeComponentsAsync();
        
        // 4. Configuration Security Analysis
        await AnalyzeConfigurationAsync();
        
        // 5. Dependency Injection Analysis
        await AnalyzeDependencyInjectionAsync();
        
        // 6. Middleware Pipeline Analysis
        await AnalyzeMiddlewarePipelineAsync();

        var analysisTime = DateTime.UtcNow - startTime;
        var result = new SecurityAnalysisResult
        {
            TotalViolations = _violations.Count,
            CriticalViolations = _violations.Count(v => v.Severity == SecuritySeverity.Critical),
            HighViolations = _violations.Count(v => v.Severity == SecuritySeverity.High),
            MediumViolations = _violations.Count(v => v.Severity == SecuritySeverity.Medium),
            LowViolations = _violations.Count(v => v.Severity == SecuritySeverity.Low),
            Violations = _violations.ToList(),
            AnalysisTime = analysisTime,
            SecurityScore = CalculateSecurityScore()
        };

        LogAnalysisResults(result);
        return result;
    }

    private async Task AnalyzeAuthenticationAsync()
    {
        _logger.LogDebug("🔐 Analyzing authentication configuration...");

        try
        {
            var authService = _serviceProvider.GetService<Microsoft.AspNetCore.Authentication.IAuthenticationService>();
            if (authService == null)
            {
                AddViolation("AUTH001", "Authentication service not registered", 
                    "Authentication service is not configured. Add builder.Services.AddAuthentication()",
                    SecuritySeverity.Critical, "Authentication");
            }

            var authStateService = _serviceProvider.GetService<Asdamir.Web.Security.Services.AuthState>();
            if (authStateService == null)
            {
                AddViolation("AUTH002", "Framework.Security AuthState not registered",
                    "AuthState service not found. Add builder.Services.AddSecurityAuthenticationState()",
                    SecuritySeverity.High, "Authentication");
            }

            // Check for secure token storage
            var localStorage = _serviceProvider.GetService<Blazored.LocalStorage.ILocalStorageService>();
            var sessionStorage = _serviceProvider.GetService<Blazored.SessionStorage.ISessionStorageService>();
            
            if (localStorage != null && sessionStorage == null)
            {
                AddViolation("AUTH003", "Only localStorage configured",
                    "Using only localStorage for tokens is less secure. Consider adding sessionStorage for sensitive data.",
                    SecuritySeverity.Medium, "Authentication");
            }
        }
        catch (Exception ex)
        {
            _logger.LogWarning(ex, "Error during authentication analysis");
        }

        await Task.CompletedTask;
    }

    private async Task AnalyzeControllersAsync()
    {
        _logger.LogDebug("🎯 Analyzing API controllers...");

        var assemblies = AppDomain.CurrentDomain.GetAssemblies()
            .Where(a => !a.IsDynamic && !a.FullName!.StartsWith("System") && !a.FullName.StartsWith("Microsoft"))
            .ToList();

        foreach (var assembly in assemblies)
        {
            try
            {
                var controllerTypes = assembly.GetTypes()
                    .Where(t => t.IsSubclassOf(typeof(ControllerBase)) && !t.IsAbstract)
                    .ToList();

                foreach (var controllerType in controllerTypes)
                {
                    AnalyzeController(controllerType);
                }
            }
            catch (Exception ex)
            {
                _logger.LogWarning(ex, "Error analyzing assembly {Assembly}", assembly.FullName);
            }
        }

        await Task.CompletedTask;
    }

    private void AnalyzeController(Type controllerType)
    {
        // Check for missing authorization
        var hasAuthorize = controllerType.GetCustomAttribute<AuthorizeAttribute>() != null;
        var hasAllowAnonymous = controllerType.GetCustomAttribute<AllowAnonymousAttribute>() != null;

        if (!hasAuthorize && !hasAllowAnonymous)
        {
            AddViolation("CTRL001", $"Controller {controllerType.Name} lacks authorization",
                $"Controller {controllerType.Name} has no [Authorize] or [AllowAnonymous] attribute. This may expose endpoints to unauthorized access.",
                SecuritySeverity.High, "Authorization");
        }

        // Check public methods without authorization
        var publicMethods = controllerType.GetMethods(BindingFlags.Public | BindingFlags.Instance)
            .Where(m => m.IsPublic && !m.IsSpecialName && m.DeclaringType == controllerType)
            .ToList();

        foreach (var method in publicMethods)
        {
            var methodHasAuthorize = method.GetCustomAttribute<AuthorizeAttribute>() != null;
            var methodHasAllowAnonymous = method.GetCustomAttribute<AllowAnonymousAttribute>() != null;

            if (!hasAuthorize && !methodHasAuthorize && !methodHasAllowAnonymous)
            {
                AddViolation("CTRL002", $"Method {controllerType.Name}.{method.Name} lacks authorization",
                    $"Public method {method.Name} in {controllerType.Name} has no authorization attributes.",
                    SecuritySeverity.Medium, "Authorization");
            }

            // Check for rate limiting on sensitive endpoints
            var hasRateLimit = method.GetCustomAttribute<Asdamir.Web.Security.Attributes.RateLimitAttribute>() != null ||
                              controllerType.GetCustomAttribute<Asdamir.Web.Security.Attributes.RateLimitAttribute>() != null;

            if (method.Name.ToLower().Contains("login") || method.Name.ToLower().Contains("register"))
            {
                if (!hasRateLimit)
                {
                    AddViolation("CTRL003", $"Sensitive endpoint {controllerType.Name}.{method.Name} lacks rate limiting",
                        $"Authentication endpoints should have rate limiting to prevent brute force attacks. Add [RateLimit] attribute.",
                        SecuritySeverity.High, "Rate Limiting");
                }
            }
        }
    }

    private async Task AnalyzeComponentsAsync()
    {
        _logger.LogDebug("🧩 Analyzing Blazor components...");

        var assemblies = AppDomain.CurrentDomain.GetAssemblies()
            .Where(a => !a.IsDynamic && !a.FullName!.StartsWith("System") && !a.FullName.StartsWith("Microsoft"))
            .ToList();

        foreach (var assembly in assemblies)
        {
            try
            {
                var componentTypes = assembly.GetTypes()
                    .Where(t => t.IsSubclassOf(typeof(ComponentBase)) && !t.IsAbstract)
                    .ToList();

                foreach (var componentType in componentTypes)
                {
                    AnalyzeComponent(componentType);
                }
            }
            catch (Exception ex)
            {
                _logger.LogWarning(ex, "Error analyzing assembly {Assembly}", assembly.FullName);
            }
        }

        await Task.CompletedTask;
    }

    private void AnalyzeComponent(Type componentType)
    {
        // Check for page components without authorization
        var pageAttribute = componentType.GetCustomAttribute<Microsoft.AspNetCore.Components.RouteAttribute>();
        var authorizePageAttribute = componentType.GetCustomAttribute<Asdamir.Web.Security.Attributes.AuthorizePageAttribute>();
        var authorizeAttribute = componentType.GetCustomAttribute<AuthorizeAttribute>();

        if (pageAttribute != null) // It's a page component
        {
            var route = pageAttribute.Template;
            
            // Skip public routes
            if (route == "/" || route == "/login" || route == "/register" || route == "/error")
                return;

            if (authorizePageAttribute == null && authorizeAttribute == null)
            {
                AddViolation("COMP001", $"Page component {componentType.Name} lacks authorization",
                    $"Page component {componentType.Name} with route '{route}' has no authorization. Add [AuthorizePage] or [Authorize] attribute.",
                    SecuritySeverity.High, "Authorization");
            }
        }

        // Check for IJSRuntime usage without validation
        var jsRuntimeFields = componentType.GetFields(BindingFlags.NonPublic | BindingFlags.Instance)
            .Where(f => f.FieldType == typeof(Microsoft.JSInterop.IJSRuntime))
            .ToList();

        var jsRuntimeProperties = componentType.GetProperties(BindingFlags.Public | BindingFlags.NonPublic | BindingFlags.Instance)
            .Where(p => p.PropertyType == typeof(Microsoft.JSInterop.IJSRuntime))
            .ToList();

        if (jsRuntimeFields.Any() || jsRuntimeProperties.Any())
        {
            AddViolation("COMP002", $"Component {componentType.Name} uses IJSRuntime",
                $"Component {componentType.Name} uses IJSRuntime. Ensure proper input validation and XSS prevention for any JavaScript calls.",
                SecuritySeverity.Low, "XSS Prevention");
        }
    }

    private async Task AnalyzeConfigurationAsync()
    {
        _logger.LogDebug("⚙️ Analyzing configuration security...");

        try
        {
            var configuration = _serviceProvider.GetService<Microsoft.Extensions.Configuration.IConfiguration>();
            if (configuration != null)
            {
                // Check for hardcoded secrets
                var connectionString = configuration.GetConnectionString("Default");
                if (!string.IsNullOrEmpty(connectionString))
                {
                    if (connectionString.Contains("password=") && !connectionString.Contains("Trusted_Connection=true"))
                    {
                        if (connectionString.ToLower().Contains("password=admin") || 
                            connectionString.ToLower().Contains("password=123") ||
                            connectionString.ToLower().Contains("password=password"))
                        {
                            AddViolation("CFG001", "Weak database password detected",
                                "Database connection string contains a weak password. Use strong passwords and consider using managed identity.",
                                SecuritySeverity.Critical, "Configuration");
                        }
                    }
                }

                // Check for HTTPS configuration
                var httpsPort = configuration["HTTPS_PORT"];
                var urls = configuration["URLS"];
                if (string.IsNullOrEmpty(httpsPort) && (string.IsNullOrEmpty(urls) || !urls.Contains("https")))
                {
                    AddViolation("CFG002", "HTTPS not properly configured",
                        "Application should enforce HTTPS in production. Configure HTTPS_PORT or ensure URLs contain https.",
                        SecuritySeverity.High, "Configuration");
                }

                // Check for development settings in production
                var environment = configuration["ASPNETCORE_ENVIRONMENT"];
                if (environment != "Development")
                {
                    var detailedErrors = configuration["DetailedErrors"];
                    if (detailedErrors == "true")
                    {
                        AddViolation("CFG003", "Detailed errors enabled in production",
                            "Detailed errors should be disabled in production to prevent information leakage.",
                            SecuritySeverity.Medium, "Configuration");
                    }
                }
            }
        }
        catch (Exception ex)
        {
            _logger.LogWarning(ex, "Error during configuration analysis");
        }

        await Task.CompletedTask;
    }

    private async Task AnalyzeDependencyInjectionAsync()
    {
        _logger.LogDebug("💉 Analyzing dependency injection configuration...");

        try
        {
            // Check for Framework.Security services
            var securityServices = new[]
            {
                typeof(Asdamir.Web.Security.Services.AuthState),
                typeof(Asdamir.Web.Security.Services.IRateLimitService),
                typeof(Asdamir.Web.Security.Services.ICspNonceProvider)
            };

            foreach (var serviceType in securityServices)
            {
                var service = _serviceProvider.GetService(serviceType);
                if (service == null)
                {
                    AddViolation("DI001", $"Security service {serviceType.Name} not registered",
                        $"Important security service {serviceType.Name} is not registered. This may impact application security.",
                        SecuritySeverity.Medium, "Dependency Injection");
                }
            }

            // Check for data protection
            var dataProtection = _serviceProvider.GetService<Microsoft.AspNetCore.DataProtection.IDataProtectionProvider>();
            if (dataProtection == null)
            {
                AddViolation("DI002", "Data protection not configured",
                    "Data protection services are not configured. Add builder.Services.AddDataProtection() for secure data handling.",
                    SecuritySeverity.High, "Data Protection");
            }
        }
        catch (Exception ex)
        {
            _logger.LogWarning(ex, "Error during dependency injection analysis");
        }

        await Task.CompletedTask;
    }

    private async Task AnalyzeMiddlewarePipelineAsync()
    {
        _logger.LogDebug("🔄 Analyzing middleware pipeline...");

        // This would require more complex analysis of the middleware pipeline
        // For now, we'll check if certain middleware types are registered
        
        try
        {
            var hostEnvironment = _serviceProvider.GetService<IHostEnvironment>();
            if (hostEnvironment != null && !hostEnvironment.IsDevelopment())
            {
                AddViolation("MW001", "Production middleware analysis needed",
                    "Ensure security middleware (HSTS, Security Headers, Rate Limiting) are properly configured for production.",
                    SecuritySeverity.Medium, "Middleware");
            }
        }
        catch (Exception ex)
        {
            _logger.LogWarning(ex, "Error during middleware analysis");
        }

        await Task.CompletedTask;
    }

    private void AddViolation(string code, string title, string description, SecuritySeverity severity, string category)
    {
        _violations.Add(new SecurityViolation
        {
            Code = code,
            Title = title,
            Description = description,
            Severity = severity,
            Category = category,
            DetectedAt = DateTime.UtcNow
        });
    }

    private int CalculateSecurityScore()
    {
        var totalIssues = _violations.Count;
        if (totalIssues == 0) return 100;

        var weightedScore = 100;
        weightedScore -= _violations.Count(v => v.Severity == SecuritySeverity.Critical) * 20;
        weightedScore -= _violations.Count(v => v.Severity == SecuritySeverity.High) * 10;
        weightedScore -= _violations.Count(v => v.Severity == SecuritySeverity.Medium) * 5;
        weightedScore -= _violations.Count(v => v.Severity == SecuritySeverity.Low) * 1;

        return Math.Max(0, weightedScore);
    }

    private void LogAnalysisResults(SecurityAnalysisResult result)
    {
        _logger.LogInformation("🔍 Security Analysis Complete:");
        _logger.LogInformation("   📊 Security Score: {Score}/100", result.SecurityScore);
        _logger.LogInformation("   🚨 Total Violations: {Total}", result.TotalViolations);
        
        if (result.CriticalViolations > 0)
            _logger.LogError("   💥 Critical: {Count}", result.CriticalViolations);
        if (result.HighViolations > 0)
            _logger.LogWarning("   🔥 High: {Count}", result.HighViolations);
        if (result.MediumViolations > 0)
            _logger.LogInformation("   ⚠️ Medium: {Count}", result.MediumViolations);
        if (result.LowViolations > 0)
            _logger.LogDebug("   ℹ️ Low: {Count}", result.LowViolations);

        _logger.LogInformation("   ⏱️ Analysis Time: {Time}ms", result.AnalysisTime.TotalMilliseconds);

        // Log critical violations
        foreach (var violation in result.Violations.Where(v => v.Severity == SecuritySeverity.Critical))
        {
            _logger.LogError("💥 {Code}: {Title} - {Description}", violation.Code, violation.Title, violation.Description);
        }

        // Log high severity violations  
        foreach (var violation in result.Violations.Where(v => v.Severity == SecuritySeverity.High))
        {
            _logger.LogWarning("🔥 {Code}: {Title} - {Description}", violation.Code, violation.Title, violation.Description);
        }
    }
}

/// <summary>
/// Aggregated outcome of a single <c>AnalyzeAsync</c> run: the full list of findings, per-severity
/// counts, how long the scan took, and the derived security score.
/// </summary>
public class SecurityAnalysisResult
{
    /// <summary>Total number of violations detected across every category.</summary>
    public int TotalViolations { get; set; }

    /// <summary>Count of <see cref="SecuritySeverity.Critical"/> violations — exploitable weaknesses that demand immediate action (e.g. weak DB password, missing authentication).</summary>
    public int CriticalViolations { get; set; }

    /// <summary>Count of <see cref="SecuritySeverity.High"/> violations — serious gaps such as an unauthorized controller or a missing rate limit on an auth endpoint.</summary>
    public int HighViolations { get; set; }

    /// <summary>Count of <see cref="SecuritySeverity.Medium"/> violations — hardening gaps worth fixing but not immediately exploitable.</summary>
    public int MediumViolations { get; set; }

    /// <summary>Count of <see cref="SecuritySeverity.Low"/> violations — advisory findings and best-practice reminders.</summary>
    public int LowViolations { get; set; }

    /// <summary>The individual findings, each with its code, title, description, severity, and category.</summary>
    public List<SecurityViolation> Violations { get; set; } = new();

    /// <summary>Wall-clock time the analysis took to complete.</summary>
    public TimeSpan AnalysisTime { get; set; }

    /// <summary>Overall score from 0 (worst) to 100 (clean), reduced per finding weighted by severity.</summary>
    public int SecurityScore { get; set; }
}

/// <summary>
/// A single security finding produced by the analyzer, describing one detected weakness.
/// </summary>
public class SecurityViolation
{
    /// <summary>Stable machine-readable identifier for the rule that fired (e.g. <c>AUTH001</c>, <c>CTRL003</c>, <c>CFG001</c>).</summary>
    public string Code { get; set; } = "";

    /// <summary>Short human-readable headline for the finding.</summary>
    public string Title { get; set; } = "";

    /// <summary>Detailed explanation of the weakness and the recommended remediation.</summary>
    public string Description { get; set; } = "";

    /// <summary>How serious the finding is; drives alerting and the score deduction.</summary>
    public SecuritySeverity Severity { get; set; }

    /// <summary>The area the finding belongs to (e.g. Authentication, Authorization, Configuration, Rate Limiting).</summary>
    public string Category { get; set; } = "";

    /// <summary>UTC timestamp at which the finding was detected during the scan.</summary>
    public DateTime DetectedAt { get; set; }
}

/// <summary>
/// Severity ranking of a <see cref="SecurityViolation"/>, ordered from least to most serious.
/// The numeric values are ascending so a higher value means a more severe finding.
/// </summary>
public enum SecuritySeverity
{
    /// <summary>Advisory / best-practice finding; minimal risk. Deducts the least from the score.</summary>
    Low = 1,

    /// <summary>Hardening gap worth fixing but not immediately exploitable.</summary>
    Medium = 2,

    /// <summary>Serious weakness that could enable unauthorized access or abuse.</summary>
    High = 3,

    /// <summary>Exploitable weakness requiring immediate remediation; triggers critical alerting.</summary>
    Critical = 4
}