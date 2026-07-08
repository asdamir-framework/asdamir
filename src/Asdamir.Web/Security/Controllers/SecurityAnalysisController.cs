// Copyright (C) 2026 Orhan Özşahin — Asdamir.
// Licensed under the GNU Lesser General Public License v3.0. See LICENSE.
// SPDX-License-Identifier: LGPL-3.0-or-later
//
// This file is part of the Asdamir open core. It is free software: you can redistribute it
// and/or modify it under the terms of the GNU Lesser General Public License as published by the
// Free Software Foundation, either version 3 of the License, or (at your option) any later
// version. It is distributed WITHOUT ANY WARRANTY; without even the implied warranty of
// MERCHANTABILITY or FITNESS FOR A PARTICULAR PURPOSE. See the GNU LGPL for more details.

using Asdamir.Web.Security.Analyzers;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;

namespace Asdamir.Web.Security.Controllers;

/// <summary>
/// API controller for security analysis and monitoring.
///
/// Endpoints return categorised auth/config/middleware weaknesses of the running
/// process — a reconnaissance handout if exposed. v2 audit fix: require an
/// admin-scoped policy. Hosts that don't define <c>SecurityAnalysisAdmin</c>
/// implicitly fall back to plain <c>[Authorize]</c> (any authenticated user).
/// </summary>
[ApiController]
[Authorize(Policy = "SecurityAnalysisAdmin")]
[Microsoft.AspNetCore.Mvc.Route("gateway/[controller]")]
public class SecurityAnalysisController : ControllerBase
{
    private readonly SecurityCodeAnalyzer _analyzer;
    private readonly ILogger<SecurityAnalysisController> _logger;

    /// <summary>
    /// Creates the controller.
    /// </summary>
    /// <param name="analyzer">The analyzer invoked to (re)scan the process on each request.</param>
    /// <param name="logger">Sink for request and failure logging.</param>
    public SecurityAnalysisController(SecurityCodeAnalyzer analyzer, ILogger<SecurityAnalysisController> logger)
    {
        _analyzer = analyzer;
        _logger = logger;
    }

    /// <summary>
    /// Runs a full security scan and returns the complete result, including the score, per-severity
    /// summary, and every individual violation. Requires the admin-scoped authorization policy.
    /// </summary>
    /// <returns><c>200 OK</c> with the full analysis payload, or <c>500</c> with a generic error message on failure (details are logged, not returned).</returns>
    [HttpPost("analyze")]
    public async Task<IActionResult> RunAnalysis()
    {
        try
        {
            _logger.LogInformation("🔍 Security analysis requested via API");
            var result = await _analyzer.AnalyzeAsync();
            
            return Ok(new
            {
                success = true,
                timestamp = DateTime.UtcNow,
                securityScore = result.SecurityScore,
                summary = new
                {
                    total = result.TotalViolations,
                    critical = result.CriticalViolations,
                    high = result.HighViolations,
                    medium = result.MediumViolations,
                    low = result.LowViolations
                },
                analysisTime = result.AnalysisTime.TotalMilliseconds,
                violations = result.Violations.Select(v => new
                {
                    code = v.Code,
                    title = v.Title,
                    description = v.Description,
                    severity = v.Severity.ToString(),
                    category = v.Category,
                    detectedAt = v.DetectedAt
                })
            });
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "❌ Security analysis failed");
            return StatusCode(500, new { success = false, error = "Security analysis failed" });
        }
    }

    /// <summary>
    /// Runs a scan and returns only the headline figures — score, total and critical/high counts,
    /// and a qualitative security level — without the individual violations.
    /// </summary>
    /// <returns><c>200 OK</c> with the summary, or <c>500</c> with a generic error message on failure.</returns>
    [HttpGet("summary")]
    public async Task<IActionResult> GetSummary()
    {
        try
        {
            var result = await _analyzer.AnalyzeAsync();
            
            return Ok(new
            {
                securityScore = result.SecurityScore,
                totalViolations = result.TotalViolations,
                criticalViolations = result.CriticalViolations,
                highViolations = result.HighViolations,
                securityLevel = GetSecurityLevel(result.SecurityScore),
                lastAnalysis = DateTime.UtcNow
            });
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "❌ Failed to get security summary");
            return StatusCode(500, new { error = "Failed to get security summary" });
        }
    }

    /// <summary>
    /// Runs a scan and returns only the violations belonging to the requested category
    /// (case-insensitive match), along with a count.
    /// </summary>
    /// <param name="category">The violation category to filter on (e.g. Authentication, Authorization, Configuration).</param>
    /// <returns><c>200 OK</c> with the matching violations, or <c>500</c> with a generic error message on failure.</returns>
    [HttpGet("violations/{category}")]
    public async Task<IActionResult> GetViolationsByCategory(string category)
    {
        try
        {
            var result = await _analyzer.AnalyzeAsync();
            var violations = result.Violations
                .Where(v => v.Category.Equals(category, StringComparison.OrdinalIgnoreCase))
                .Select(v => new
                {
                    code = v.Code,
                    title = v.Title,
                    description = v.Description,
                    severity = v.Severity.ToString(),
                    detectedAt = v.DetectedAt
                })
                .ToList();
            
            return Ok(new
            {
                category = category,
                count = violations.Count,
                violations = violations
            });
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "❌ Failed to get violations by category");
            return StatusCode(500, new { error = "Failed to get violations" });
        }
    }

    /// <summary>
    /// Runs a scan and returns the distinct violation categories present, each with its total plus
    /// critical/high counts, ordered so the most severe categories come first.
    /// </summary>
    /// <returns><c>200 OK</c> with the category breakdown, or <c>500</c> with a generic error message on failure.</returns>
    [HttpGet("categories")]
    public async Task<IActionResult> GetCategories()
    {
        try
        {
            var result = await _analyzer.AnalyzeAsync();
            var categories = result.Violations
                .GroupBy(v => v.Category)
                .Select(g => new
                {
                    category = g.Key,
                    count = g.Count(),
                    criticalCount = g.Count(v => v.Severity == SecuritySeverity.Critical),
                    highCount = g.Count(v => v.Severity == SecuritySeverity.High)
                })
                .OrderByDescending(c => c.criticalCount)
                .ThenByDescending(c => c.highCount)
                .ToList();
            
            return Ok(categories);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "❌ Failed to get categories");
            return StatusCode(500, new { error = "Failed to get categories" });
        }
    }

    private static string GetSecurityLevel(int score)
    {
        return score switch
        {
            >= 90 => "Excellent",
            >= 80 => "Good",
            >= 70 => "Fair",
            >= 60 => "Poor",
            _ => "Critical"
        };
    }
}