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
    public abstract string Name { get; }
    public abstract string Description { get; }
    public virtual bool IsCritical => false;
    public virtual int Priority => 0;

    public bool AppliesTo<TEntity>() => AppliesTo(typeof(TEntity));

    public bool AppliesTo(Type entityType)
    {
        if (entityType == typeof(T)) return true;
        return entityType.IsSubclassOf(typeof(T));
    }

    public async Task<BusinessRuleResult> EvaluateAsync(object entity, IValidationContext context, CancellationToken cancellationToken = default)
    {
        if (entity is T typedEntity)
        {
            return await EvaluateTypedAsync(typedEntity, context, cancellationToken);
        }

        return BusinessRuleResult.Failure($"Entity type {entity.GetType().Name} is not compatible with rule {Name}");
    }

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
///    atomically via <see cref="Interlocked.Exchange"/>.
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

    public IEnumerable<IBusinessRule> GetRulesFor<T>() where T : class
    {
        return _businessRules.Where(rule => rule.AppliesTo<T>());
    }

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

    public BusinessRuleFactory(IServiceProvider serviceProvider, IEnumerable<IBusinessRule> registeredRules)
    {
        _serviceProvider = serviceProvider;
        _registeredRules = registeredRules;
    }

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

    public string Name { get; }
    public string Description { get; }
    public bool IsCritical { get; }
    public int Priority { get; }

    public CompositeBusinessRule(string name, string description, IBusinessRule[] rules, bool isCritical = false, int priority = 0)
    {
        Name = name;
        Description = description;
        _rules = rules;
        IsCritical = isCritical;
        Priority = priority;
    }

    public bool AppliesTo<T>() => AppliesTo(typeof(T));

    public bool AppliesTo(Type entityType)
    {
        return _rules.Any(rule => rule.AppliesTo(entityType));
    }

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
public class CreditLimitRule : BusinessRuleBase<object>
{
    public override string Name => "CreditLimitRule";
    public override string Description => "Validates that order does not exceed customer credit limit";
    public override bool IsCritical => true;

    protected override async Task<BusinessRuleResult> EvaluateTypedAsync(object entity, IValidationContext context, CancellationToken cancellationToken)
    {
        // Example implementation - replace with actual business logic
        await Task.CompletedTask;
        return BusinessRuleResult.Success();
    }
}

public class InventoryAvailabilityRule : BusinessRuleBase<object>
{
    public override string Name => "InventoryAvailabilityRule";
    public override string Description => "Validates that requested items are available in inventory";

    protected override async Task<BusinessRuleResult> EvaluateTypedAsync(object entity, IValidationContext context, CancellationToken cancellationToken)
    {
        // Example implementation - replace with actual business logic
        await Task.CompletedTask;
        return BusinessRuleResult.Success();
    }
}

public class BusinessHoursRule : BusinessRuleBase<object>
{
    public override string Name => "BusinessHoursRule";
    public override string Description => "Validates that operations are performed during business hours";

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
