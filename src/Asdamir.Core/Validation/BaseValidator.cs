// Copyright (C) 2026 Orhan Özşahin — Asdamir.
// Licensed under the GNU Lesser General Public License v3.0. See LICENSE.
// SPDX-License-Identifier: LGPL-3.0-or-later
//
// This file is part of the Asdamir open core. It is free software: you can redistribute it
// and/or modify it under the terms of the GNU Lesser General Public License as published by the
// Free Software Foundation, either version 3 of the License, or (at your option) any later
// version. It is distributed WITHOUT ANY WARRANTY; without even the implied warranty of
// MERCHANTABILITY or FITNESS FOR A PARTICULAR PURPOSE. See the GNU LGPL for more details.

using FluentValidation;
using FluentValidation.Results;

namespace Asdamir.Core.Validation;

/// <summary>
/// Base class for enterprise validators
/// </summary>
public abstract class BaseValidator<T> : AbstractValidator<T>
{
    /// <summary>
    /// Invokes <see cref="ConfigureRules"/> so derived validators declare their rules in one place.
    /// </summary>
    protected BaseValidator()
    {
        ConfigureRules();
    }

    /// <summary>
    /// Configure validation rules
    /// </summary>
    protected abstract void ConfigureRules();
}

/// <summary>
/// Base class for validators with context support
/// </summary>
public abstract class ContextualValidator<T> : BaseValidator<T>
{
    /// <summary>
    /// Validates with additional context. The base implementation ignores the context and runs the
    /// standard rules; override to consult the context.
    /// </summary>
    /// <param name="instance">The object to validate.</param>
    /// <param name="context">Ambient key/value context for context-dependent rules.</param>
    /// <returns>The validation result.</returns>
    public virtual ValidationResult ValidateWithContext(T instance, IDictionary<string, object> context)
    {
        return Validate(instance);
    }
}

/// <summary>
/// Base class for validators with full context support
/// </summary>
public abstract class Validator<T> : AbstractValidator<T>, IValidator<T>
{
    /// <summary>
    /// Invokes <see cref="ConfigureRules"/> so derived validators declare their rules in one place.
    /// </summary>
    protected Validator()
    {
        ConfigureRules();
    }

    /// <summary>
    /// Configure validation rules
    /// </summary>
    protected abstract void ConfigureRules();

    /// <summary>
    /// Runs the full pipeline: applies context-specific rules, performs standard validation, then
    /// applies post-validation business rules — returning the merged result.
    /// </summary>
    /// <param name="instance">The object to validate.</param>
    /// <param name="context">The validation context consulted by the contextual and business-rule steps.</param>
    /// <param name="cancellationToken">Token to cancel the async validation.</param>
    /// <returns>The validation result after all pipeline stages.</returns>
    public virtual async Task<ValidationResult> ValidateAsync(T instance, IValidationContext context, CancellationToken cancellationToken = default)
    {
        // Apply context-specific rules
        ApplyContextualRules(context);
        
        // Perform standard validation
        var result = await ValidateAsync(instance, cancellationToken);
        
        // Apply post-validation business rules
        await ApplyBusinessRulesAsync(instance, context, result, cancellationToken);
        
        return result;
    }

    /// <summary>
    /// Apply rules based on validation context. No-op by default; override to add context-driven checks.
    /// </summary>
    /// <param name="context">The validation context to consult when deciding which rules apply.</param>
    /// <remarks>
    /// ⚠️ AUDIT WARNING: implementations of this method MUST NOT call <c>RuleFor(...)</c> on
    /// the validator instance — FluentValidation accumulates rules on the
    /// <see cref="AbstractValidator{T}"/> instance, so registering new rules per-call grows the
    /// rule set without bound and changes ordering between calls. The validator instance is
    /// scoped (per request) but the pipeline is still mutated across method calls within that
    /// scope.
    ///
    /// Correct patterns:
    ///   - Use <c>RuleFor(...).When(predicate)</c> inside <c>ConfigureRules</c> so the predicate
    ///     consults the <paramref name="context"/> at run time.
    ///   - Or build a fresh <c>InlineValidator&lt;T&gt;</c> here and call its <c>Validate</c>
    ///     method directly, merging the result into the outer <see cref="ValidationResult"/>.
    /// </remarks>
    protected virtual void ApplyContextualRules(IValidationContext context)
    {
        // Override in derived classes — see remarks for safe patterns.
    }

    /// <summary>
    /// Apply complex business rules after standard validation. No-op by default; override to add
    /// failures to <paramref name="result"/> based on cross-field or external checks.
    /// </summary>
    /// <param name="instance">The validated object.</param>
    /// <param name="context">The validation context.</param>
    /// <param name="result">The result so far; add failures here.</param>
    /// <param name="cancellationToken">Token to cancel the async work.</param>
    /// <returns>A task that completes when the business rules have run.</returns>
    protected virtual Task ApplyBusinessRulesAsync(T instance, IValidationContext context, ValidationResult result, CancellationToken cancellationToken)
    {
        // Override in derived classes for complex business validation
        return Task.CompletedTask;
    }

    /// <summary>
    /// Helper to group rules that apply only to a named operation (e.g. "create" vs "update").
    /// </summary>
    /// <param name="operation">The operation name the rules are scoped to.</param>
    /// <param name="action">The rule-registration callback to run for that operation.</param>
    protected void WhenOperation(string operation, Action action)
    {
        // This would be checked during validation
        action();
    }
}
