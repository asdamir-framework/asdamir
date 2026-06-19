# Validation

**Package:** `Asdamir.Core` · **Namespace:** `Asdamir.Core.Validation`

## Introduction

Asdamir layers three complementary validation styles:

1. **FluentValidation** validators for request/command objects.
2. **Data-annotation attributes**, including framework-specific ones (e.g. phone validation backed by libphonenumber).
3. A **business-rule engine** for cross-cutting, runtime-evaluated rules.

## Registration

```csharp
builder.Services.AddValidation();                    // core validation services
builder.Services.AddValidationWithFluentValidation(); // + FluentValidation assembly scanning
// or the umbrella:
builder.Services.AddFullValidation();
```

`AddValidationWithFluentValidation` registers all `AbstractValidator<T>` implementations found in your assemblies.

## FluentValidation

```csharp
public sealed class CreateUserValidator : AbstractValidator<CreateUserRequest>
{
    public CreateUserValidator()
    {
        RuleFor(x => x.Email).NotEmpty().EmailAddress();
        RuleFor(x => x.Name).NotEmpty().MaximumLength(200);
    }
}
```

## Attributes

Use standard `System.ComponentModel.DataAnnotations` attributes plus the framework's additions:

```csharp
public sealed record ContactDto(
    [property: Required] string Name,
    [property: Phone] string PhoneNumber);   // Asdamir.Core.Validation.PhoneAttribute (libphonenumber)
```

The `[Phone]` attribute validates real, dialable numbers (not just a regex) via libphonenumber.

## Business-rule engine

For rules that depend on runtime state (time windows, tenant policy, cross-entity checks), implement `IBusinessRule` and evaluate them through the engine. This keeps imperative "can this happen right now?" logic out of your controllers and testable in isolation.

## See also

- [Error Handling](error-handling.md) — how validation failures map to RFC-7807 responses
