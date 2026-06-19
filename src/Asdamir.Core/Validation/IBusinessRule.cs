// Copyright (C) 2026 Orhan Özşahin — Asdamir.
// Licensed under the GNU Lesser General Public License v3.0. See LICENSE.
// SPDX-License-Identifier: LGPL-3.0-or-later
//
// This file is part of the Asdamir open core. It is free software: you can redistribute it
// and/or modify it under the terms of the GNU Lesser General Public License as published by the
// Free Software Foundation, either version 3 of the License, or (at your option) any later
// version. It is distributed WITHOUT ANY WARRANTY; without even the implied warranty of
// MERCHANTABILITY or FITNESS FOR A PARTICULAR PURPOSE. See the GNU LGPL for more details.


namespace Asdamir.Core.Validation;

/// <summary>
/// Represents a business rule that can be evaluated
/// </summary>
public interface IBusinessRule
{
    /// <summary>
    /// Gets the name of the business rule
    /// </summary>
    string Name { get; }

    /// <summary>
    /// Gets the description of the business rule
    /// </summary>
    string Description { get; }

    /// <summary>
    /// Gets whether this rule is critical (must pass)
    /// </summary>
    bool IsCritical { get; }

    /// <summary>
    /// Gets the priority of this rule (higher number = higher priority)
    /// </summary>
    int Priority { get; }

    /// <summary>
    /// Determines if this rule applies to the given entity type
    /// </summary>
    bool AppliesTo<TEntity>();

    /// <summary>
    /// Determines if this rule applies to the given entity type (runtime)
    /// </summary>
    bool AppliesTo(Type entityType);

    /// <summary>
    /// Evaluates the business rule asynchronously
    /// </summary>
    Task<BusinessRuleResult> EvaluateAsync(object entity, IValidationContext context, CancellationToken cancellationToken = default);
}

/// <summary>
/// Represents a business rule engine that can evaluate multiple rules
/// </summary>
public interface IBusinessRuleEngine
{
    /// <summary>
    /// Registers a business rule
    /// </summary>
    void RegisterRule<T>(IBusinessRule rule) where T : class;

    /// <summary>
    /// Evaluates all applicable business rules for the given entity
    /// </summary>
    Task<BusinessRuleResult> EvaluateAsync<T>(T entity, IValidationContext context, CancellationToken cancellationToken = default) where T : class;

    /// <summary>
    /// Evaluates a specific business rule by name
    /// </summary>
    Task<BusinessRuleResult> EvaluateRuleAsync<T>(string ruleName, T entity, IValidationContext context, CancellationToken cancellationToken = default) where T : class;

    /// <summary>
    /// Gets all registered rules for the given entity type
    /// </summary>
    IEnumerable<IBusinessRule> GetRulesFor<T>() where T : class;
}
