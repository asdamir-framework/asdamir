---
name: asdamir-validation
description: Use when adding input validation or business rules — FluentValidation validators, validation attributes, or the business-rule engine. This owns request-SHAPE checking even for auth requests (a validator for a login/sign-in request = field/format validation, NOT the auth logic — that's asdamir-security). Trigger on "validate", "validation rule", "FluentValidation", "required / format check", "business rule", "validator for a request (incl. a login/auth request)".
---

# Asdamir validation

Deep reference: `docs/fundamentals/validation.md`, `CLAUDE.md` → DI entry points.

- Register with `AddValidation()` / `AddValidationWithFluentValidation()` / `AddFullValidation()` (the
  fullest wires FluentValidation + the custom attributes + the business-rule engine).
- **Input validation** = FluentValidation validators (one `AbstractValidator<T>` per request/DTO) +
  framework custom attributes (e.g. phone numbers validated via libphonenumber). API controllers return
  `ValidationProblem(ModelState)` (RFC-7807) on invalid input — pairs with `asdamir-error-handling`.
- **Business rules** (cross-field / stateful invariants) go in the **business-rule engine**, not scattered
  `if`s — so they're testable and reusable.

## DON'T
- **Don't hand-roll ad-hoc validation** scattered across controllers — put it in a validator / the rule engine.
- **Don't return raw 500s for bad input** — invalid input is a 400 `ValidationProblem`, not an exception.
- **Don't duplicate a framework attribute** (e.g. re-implement phone validation) — reuse the provided ones.
