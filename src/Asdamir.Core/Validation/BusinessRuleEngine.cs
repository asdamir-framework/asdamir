// Copyright (C) 2026 Orhan Özşahin — Asdamir.
// Licensed under the GNU Lesser General Public License v3.0. See LICENSE.
// SPDX-License-Identifier: LGPL-3.0-or-later
//
// This file is part of the Asdamir open core. It is free software: you can redistribute it
// and/or modify it under the terms of the GNU Lesser General Public License as published by the
// Free Software Foundation, either version 3 of the License, or (at your option) any later
// version. It is distributed WITHOUT ANY WARRANTY; without even the implied warranty of
// MERCHANTABILITY or FITNESS FOR A PARTICULAR PURPOSE. See the GNU LGPL for more details.

using IValidationContext = Asdamir.Core.Validation.IValidationContext;
using System.Collections.Immutable;
using Microsoft.Extensions.DependencyInjection;

namespace Asdamir.Core.Validation;


/// <summary>
/// Base implementation for business rules
/// </summary>
public abstract class BusinessRuleBase<T> : IBusinessRule
{
    /// <summary>Stable, unique identifier of the rule; the engine dedups and looks rules up by this value, so it must be constant per rule type.</summary>
    public abstract string Name { get; }
    /// <summary>Human-readable explanation of what the rule enforces (diagnostics / composite-rule descriptions).</summary>
    public abstract string Description { get; }
    /// <summary>When <c>true</c>, a failure of this rule short-circuits the remaining rules in an evaluation pass. Defaults to non-critical.</summary>
    public virtual bool IsCritical => false;
    /// <summary>Ordering weight; the engine evaluates applicable rules ascending by priority (lower runs first). Defaults to 0.</summary>
    public virtual int Priority => 0;

    /// <summary>Type-parameter overload of <c>AppliesTo(Type)</c> — reports whether this rule targets <typeparamref name="TEntity"/>.</summary>
    /// <typeparam name="TEntity">Candidate entity type.</typeparam>
    /// <returns><c>true</c> if the rule applies to <typeparamref name="TEntity"/>.</returns>
    public bool AppliesTo<TEntity>() => AppliesTo(typeof(TEntity));

    /// <summary>Determines whether the rule applies to the given entity type — matches <typeparamref name="T"/> exactly or any subclass of it.</summary>
    /// <param name="entityType">The runtime entity type being evaluated.</param>
    /// <returns><c>true</c> if <paramref name="entityType"/> is <typeparamref name="T"/> or derives from it.</returns>
    public bool AppliesTo(Type entityType)
    {
        if (entityType == typeof(T)) return true;
        return entityType.IsSubclassOf(typeof(T));
    }

    /// <summary>Non-generic entry point: casts the untyped entity to <typeparamref name="T"/> and delegates to <see cref="EvaluateTypedAsync"/>; a type mismatch yields a failure result rather than an exception.</summary>
    /// <param name="entity">The subject to evaluate (must be assignable to <typeparamref name="T"/>).</param>
    /// <param name="context">Ambient data available to the rule during evaluation.</param>
    /// <param name="cancellationToken">Cancels the evaluation.</param>
    /// <returns>The typed rule's outcome, or a failure when <paramref name="entity"/> is not a <typeparamref name="T"/>.</returns>
    public async Task<BusinessRuleResult> EvaluateAsync(object entity, IValidationContext context, CancellationToken cancellationToken = default)
    {
        if (entity is T typedEntity)
        {
            return await EvaluateTypedAsync(typedEntity, context, cancellationToken);
        }

        return BusinessRuleResult.Failure($"Entity type {entity.GetType().Name} is not compatible with rule {Name}");
    }

    /// <summary>The strongly-typed rule body that derived rules implement; invoked only after the entity has been verified to be a <typeparamref name="T"/>.</summary>
    /// <param name="entity">The already type-checked subject.</param>
    /// <param name="context">Ambient data available to the rule during evaluation.</param>
    /// <param name="cancellationToken">Cancels the evaluation.</param>
    /// <returns>The rule's success/failure outcome.</returns>
    protected abstract Task<BusinessRuleResult> EvaluateTypedAsync(T entity, IValidationContext context, CancellationToken cancellationToken);
}

/// <summary>
/// Business rule engine implementation.
///
/// Audit fixes vs. v1:
///  - <c>_businessRules</c> was a mutable <c>List&lt;IBusinessRule&gt;</c> shared across
///    every consumer. <c>RegisterRule</c> mutated it without synchronization while
///    <c>EvaluateAsync</c> iterated → InvalidOperationException ("Collection was
///    modified") under load. Now an <c>ImmutableArray&lt;IBusinessRule&gt;</c> swapped
///    atomically via <c>Interlocked.Exchange</c>.
///  - <c>RegisterRule</c> had no dedup — registering the same rule twice executed
///    it twice. Dedup is now by <c>rule.Name</c>.
///  - Catch-all in <c>EvaluateAsync</c> overwrote prior results with the LATEST
///    failure, hiding earlier failures. Now exception failures are merged via
///    <see cref="BusinessRuleResult.Combine"/> just like normal failures.
/// </summary>
public class BusinessRuleEngine : IBusinessRuleEngine
{
    private readonly IServiceProvider _serviceProvider;
    private System.Collections.Immutable.ImmutableArray<IBusinessRule> _businessRules;

    /// <summary>Creates the engine and seeds it with the DI-registered rules, deduping by <see cref="IBusinessRule.Name"/> so a rule registered under two interfaces runs only once.</summary>
    /// <param name="serviceProvider">Root provider used to resolve additional rules on demand.</param>
    /// <param name="businessRules">The rules discovered via DI; duplicates by name are collapsed to the first occurrence.</param>
    public BusinessRuleEngine(IServiceProvider serviceProvider, IEnumerable<IBusinessRule> businessRules)
    {
        _serviceProvider = serviceProvider;
        // Dedup on construction too: if DI registers the same rule under two
        // interfaces it would otherwise run twice.
        _businessRules = businessRules
            .GroupBy(r => r.Name, StringComparer.Ordinal)
            .Select(g => g.First())
            .ToImmutableArray();
    }

    /// <summary>Adds a rule at runtime (idempotent — skipped if a rule with the same <see cref="IBusinessRule.Name"/> is already present); the immutable rule array is swapped via a lock-free CAS so concurrent evaluations are never disturbed.</summary>
    /// <typeparam name="T">Present for API symmetry; the rule's own <c>AppliesTo</c> governs which entities it targets.</typeparam>
    /// <param name="rule">The rule to add.</param>
    public void RegisterRule<T>(IBusinessRule rule) where T : class
    {
        // Audit fix v2: ImmutableArray<T> is a struct, so System.Threading.Interlocked.CompareExchange<T>
        // — which constrains T to a reference type — throws NotSupportedException at runtime.
        // The earlier MEDIUM #4 fix shipped with that bug; this test (BusinessRuleEngineTests)
        // caught it. Use System.Collections.Immutable.ImmutableInterlocked.Update, which is the
        // purpose-built CAS helper for ImmutableArray and friends.
        ImmutableInterlocked.Update(ref _businessRules, (current, newRule) =>
        {
            return current.Any(r => string.Equals(r.Name, newRule.Name, StringComparison.Ordinal))
                ? current
                : current.Add(newRule);
        }, rule);
    }

    /// <summary>Returns the currently registered rules that apply to <typeparamref name="T"/>, in registration order (unordered by priority).</summary>
    /// <typeparam name="T">Entity type to filter rules by.</typeparam>
    /// <returns>Lazily filtered sequence of applicable rules.</returns>
    public IEnumerable<IBusinessRule> GetRulesFor<T>() where T : class
    {
        return _businessRules.Where(rule => rule.AppliesTo<T>());
    }

    /// <summary>Runs every applicable rule against the entity in ascending priority order and merges all outcomes into one aggregate result; a failing critical rule stops the pass early. Iterates an immutable snapshot, so concurrent registration is safe.</summary>
    /// <typeparam name="T">Type of the entity being validated.</typeparam>
    /// <param name="entity">The subject to evaluate.</param>
    /// <param name="context">Ambient data available to the rules.</param>
    /// <param name="cancellationToken">Cancels the evaluation.</param>
    /// <returns>A combined result; failures (including those raised as exceptions) from all evaluated rules are merged, not overwritten.</returns>
    public async Task<BusinessRuleResult> EvaluateAsync<T>(T entity, IValidationContext context, CancellationToken cancellationToken = default) where T : class
    {
        var result = BusinessRuleResult.Success();
        var entityType = typeof(T);
        var snapshot = _businessRules; // single read; snapshot is immutable
        var applicableRules = snapshot
            .Where(rule => rule.AppliesTo(entityType))
            .OrderBy(rule => rule.Priority)
            .ToList();

        foreach (var rule in applicableRules)
        {
            try
            {
                var ruleResult = await rule.EvaluateAsync(entity!, context, cancellationToken);
                result.Combine(ruleResult);

                if (!ruleResult.IsValid && rule.IsCritical)
                {
                    break;
                }
            }
            catch (Exception ex)
            {
                // Merge rather than overwrite — earlier failures must remain visible.
                result.Combine(BusinessRuleResult.Failure($"Business rule '{rule.Name}' failed with exception: {ex.Message}"));
                if (rule.IsCritical) break;
            }
        }

        return result;
    }

    /// <summary>Evaluates a single named rule against the entity; returns a failure result (never throws) when the rule is unknown, does not apply to <typeparamref name="T"/>, or itself throws.</summary>
    /// <typeparam name="T">Type of the entity being validated.</typeparam>
    /// <param name="ruleName">The <see cref="IBusinessRule.Name"/> of the rule to run.</param>
    /// <param name="entity">The subject to evaluate.</param>
    /// <param name="context">Ambient data available to the rule.</param>
    /// <param name="cancellationToken">Cancels the evaluation.</param>
    /// <returns>The rule's outcome, or a failure describing why it could not run.</returns>
    public async Task<BusinessRuleResult> EvaluateRuleAsync<T>(string ruleName, T entity, IValidationContext context, CancellationToken cancellationToken = default) where T : class
    {
        var rule = _businessRules.FirstOrDefault(r => r.Name == ruleName);
        if (rule == null)
        {
            return BusinessRuleResult.Failure($"Business rule '{ruleName}' not found");
        }

        if (!rule.AppliesTo(typeof(T)))
        {
            return BusinessRuleResult.Failure($"Business rule '{ruleName}' does not apply to type {typeof(T).Name}");
        }

        try
        {
            return await rule.EvaluateAsync(entity!, context, cancellationToken);
        }
        catch (Exception ex)
        {
            return BusinessRuleResult.Failure($"Business rule '{ruleName}' failed with exception: {ex.Message}");
        }
    }
}

/// <summary>
/// Factory for creating business rules
/// </summary>
public class BusinessRuleFactory : IBusinessRuleFactory
{
    private readonly IServiceProvider _serviceProvider;
    private readonly IEnumerable<IBusinessRule> _registeredRules;

    /// <summary>Creates the factory over the DI-registered rule set, keeping the provider for on-demand resolution of rules not in that set.</summary>
    /// <param name="serviceProvider">Provider used to resolve rules that are registered but not pre-materialized.</param>
    /// <param name="registeredRules">The eagerly materialized rules to search first.</param>
    public BusinessRuleFactory(IServiceProvider serviceProvider, IEnumerable<IBusinessRule> registeredRules)
    {
        _serviceProvider = serviceProvider;
        _registeredRules = registeredRules;
    }

    /// <summary>Resolves a rule by name that applies to <typeparamref name="T"/>, checking the pre-materialized set first, then falling back to a fresh DI lookup.</summary>
    /// <typeparam name="T">Entity type the rule must apply to.</typeparam>
    /// <param name="ruleName">The <see cref="IBusinessRule.Name"/> to find.</param>
    /// <returns>The matching rule instance.</returns>
    /// <exception cref="ArgumentException">No registered rule with that name applies to <typeparamref name="T"/>.</exception>
    public IBusinessRule CreateRule<T>(string ruleName) where T : class
    {
        var fromRegistry = _registeredRules.FirstOrDefault(r => r.Name == ruleName && r.AppliesTo(typeof(T)));
        if (fromRegistry != null) return fromRegistry;

        // Attempt to resolve from DI
        var allRules = _serviceProvider.GetServices<IBusinessRule>();
        var resolved = allRules.FirstOrDefault(r => r.Name == ruleName && r.AppliesTo(typeof(T)));
        if (resolved != null) return resolved;

        throw new ArgumentException($"Unknown or incompatible business rule: {ruleName}");
    }

    /// <summary>Builds a <see cref="CompositeBusinessRule"/> that bundles the named child rules under a single name/description; child rules are resolved via <c>CreateRule</c>.</summary>
    /// <param name="name">Name for the composite rule.</param>
    /// <param name="description">Description for the composite rule.</param>
    /// <param name="ruleNames">Names of the child rules to include; at least one is required.</param>
    /// <returns>A composite rule that evaluates all resolved children.</returns>
    /// <exception cref="ArgumentException">No rule names were supplied, or a named child rule could not be resolved.</exception>
    public IBusinessRule CreateCompositeRule(string name, string description, params string[] ruleNames)
    {
        var rules = ruleNames.Select(rn => CreateRule<object>(rn)).ToArray();
        if (rules.Length == 0)
        {
            throw new ArgumentException("Composite rule requires at least one child rule", nameof(ruleNames));
        }
        return new CompositeBusinessRule(name, description, rules);
    }
}

/// <summary>
/// Composite business rule that combines multiple rules
/// </summary>
public class CompositeBusinessRule : IBusinessRule
{
    private readonly IBusinessRule[] _rules;

    /// <summary>Name of the composite as a whole.</summary>
    public string Name { get; }
    /// <summary>Human-readable description of what the bundled rules collectively enforce.</summary>
    public string Description { get; }
    /// <summary>When <c>true</c>, a failure of the composite short-circuits an outer evaluation pass (does not affect iteration of its own children).</summary>
    public bool IsCritical { get; }
    /// <summary>Ordering weight of the composite within an engine's evaluation pass (lower runs first).</summary>
    public int Priority { get; }

    /// <summary>Creates a composite rule that fans an evaluation out to the supplied child rules.</summary>
    /// <param name="name">Name of the composite.</param>
    /// <param name="description">Description of the composite.</param>
    /// <param name="rules">The child rules to evaluate.</param>
    /// <param name="isCritical">Whether a failure of this composite should short-circuit an outer pass.</param>
    /// <param name="priority">Ordering weight of the composite.</param>
    public CompositeBusinessRule(string name, string description, IBusinessRule[] rules, bool isCritical = false, int priority = 0)
    {
        Name = name;
        Description = description;
        _rules = rules;
        IsCritical = isCritical;
        Priority = priority;
    }

    /// <summary>Type-parameter overload of <c>AppliesTo(Type)</c> — the composite applies if any child rule applies to <typeparamref name="T"/>.</summary>
    /// <typeparam name="T">Candidate entity type.</typeparam>
    /// <returns><c>true</c> if at least one child rule applies to <typeparamref name="T"/>.</returns>
    public bool AppliesTo<T>() => AppliesTo(typeof(T));

    /// <summary>The composite applies to a type when any of its child rules applies to that type.</summary>
    /// <param name="entityType">The runtime entity type being evaluated.</param>
    /// <returns><c>true</c> if at least one child rule applies to <paramref name="entityType"/>.</returns>
    public bool AppliesTo(Type entityType)
    {
        return _rules.Any(rule => rule.AppliesTo(entityType));
    }

    /// <summary>Evaluates each applicable child rule against the entity and merges their outcomes; a failing critical child stops the remaining children.</summary>
    /// <param name="entity">The subject to evaluate.</param>
    /// <param name="context">Ambient data available to the child rules.</param>
    /// <param name="cancellationToken">Cancels the evaluation.</param>
    /// <returns>The merged outcome of all evaluated child rules.</returns>
    public async Task<BusinessRuleResult> EvaluateAsync(object entity, IValidationContext context, CancellationToken cancellationToken = default)
    {
        var result = new BusinessRuleResult();

        var entityType = entity.GetType();
        foreach (var rule in _rules.Where(r => r.AppliesTo(entityType)))
        {
            var ruleResult = await rule.EvaluateAsync(entity, context, cancellationToken);
            result.Combine(ruleResult);

            if (!ruleResult.IsValid && rule.IsCritical)
            {
                break;
            }
        }

        return result;
    }
}

// Example business rule implementations
/// <summary>Sample critical rule sketching a customer credit-limit check; ships as a stub returning success — replace the body with real domain logic.</summary>
public class CreditLimitRule : BusinessRuleBase<object>
{
    /// <inheritdoc/>
    public override string Name => "CreditLimitRule";
    /// <inheritdoc/>
    public override string Description => "Validates that order does not exceed customer credit limit";
    /// <inheritdoc/>
    public override bool IsCritical => true;

    /// <inheritdoc/>
    protected override async Task<BusinessRuleResult> EvaluateTypedAsync(object entity, IValidationContext context, CancellationToken cancellationToken)
    {
        // Example implementation - replace with actual business logic
        await Task.CompletedTask;
        return BusinessRuleResult.Success();
    }
}

/// <summary>Sample rule sketching an inventory-availability check; ships as a stub returning success — replace the body with real domain logic.</summary>
public class InventoryAvailabilityRule : BusinessRuleBase<object>
{
    /// <inheritdoc/>
    public override string Name => "InventoryAvailabilityRule";
    /// <inheritdoc/>
    public override string Description => "Validates that requested items are available in inventory";

    /// <inheritdoc/>
    protected override async Task<BusinessRuleResult> EvaluateTypedAsync(object entity, IValidationContext context, CancellationToken cancellationToken)
    {
        // Example implementation - replace with actual business logic
        await Task.CompletedTask;
        return BusinessRuleResult.Success();
    }
}

/// <summary>Sample rule enforcing that an operation runs during 9 AM–5 PM local server time; a working example (not a stub) of a non-critical rule.</summary>
public class BusinessHoursRule : BusinessRuleBase<object>
{
    /// <inheritdoc/>
    public override string Name => "BusinessHoursRule";
    /// <inheritdoc/>
    public override string Description => "Validates that operations are performed during business hours";

    /// <inheritdoc/>
    protected override Task<BusinessRuleResult> EvaluateTypedAsync(object entity, IValidationContext context, CancellationToken cancellationToken)
    {
        var currentHour = DateTime.Now.Hour;
        if (currentHour < 9 || currentHour > 17)
        {
            return Task.FromResult(BusinessRuleResult.Failure("Operation can only be performed during business hours (9 AM - 5 PM)"));
        }

        return Task.FromResult(BusinessRuleResult.Success());
    }
}
