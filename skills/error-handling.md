# Error Handling

## Purpose

Cross-cutting reference that connects existing error-handling patterns into one readable flow. Not a new pattern — a navigation aid linking authoritative sources.

---

## Error Pipeline Overview

```
[Domain]   DomainResult<T>.Success / .Failure     — business validation, rules, state transitions
               ↓
[Service]  Result<T>.Success / .Failure / .None    — orchestration, tenant boundary, structure validation
               ↓
[Endpoint] result.Match(ok → TypedResults, errors → ProblemDetails, notFound → NotFound)
               ↓
[Global]   DefaultExceptionHandler (IExceptionHandler)  — unexpected exceptions only → ProblemDetails
```

---

## Layer-by-Layer Rules

### Domain Layer
- Entity factory `Create()` returns `DomainResult<T>` — never throws for validation
- Domain rules use `RuleBase<T>.Evaluate()` returning `DomainResult`
- Chain with `.Bind()` and `.Map()` for composed operations
- **Authoritative:** [skills/domain-model.md](domain-model.md), [templates/domain-rules-template.md](../templates/domain-rules-template.md)

### Application Layer
- Services return `Result<T>` wrapping domain results
- `StructureValidator` returns `Result<TDto>` for DTO validation before domain calls
- Tenant boundary checks return `Result` (failure = 403-equivalent)
- **Authoritative:** [skills/application-layer.md](application-layer.md), [templates/service-template.md](../templates/service-template.md), [templates/structure-validator-template.md](../templates/structure-validator-template.md)

### API Layer
- Endpoints use `Result.Match()` to map outcomes to HTTP responses
- Success → `TypedResults.Ok(dto)` / `Created`
- Failure → `TypedResults.Problem(ProblemDetails)` / `ValidationProblem`
- Not Found → `TypedResults.NotFound()`
- **Authoritative:** [skills/api.md](api.md), [templates/endpoint-template.md](../templates/endpoint-template.md)

### Global Exception Handler
- `DefaultExceptionHandler` catches **only** unexpected exceptions
- Maps exception types to `ProblemDetails` with HTTP status codes
- Last-resort safety net — not a control-flow mechanism
- **Authoritative:** [templates/exception-handler-template.md](../templates/exception-handler-template.md)

---

## Error Type Mapping

| Source | Error | HTTP Status |
|---|---|---|
| Domain | `DomainError.Validation` | 400 Bad Request |
| Domain | `DomainError.NotFound` | 404 Not Found |
| Domain | `DomainError.Conflict` | 409 Conflict |
| Domain | `DomainError.Unauthorized` | 403 Forbidden |
| Service | `Result.Failure` (generic) | 422 Unprocessable Entity |
| Service | `Result.None` | 404 Not Found |
| Service | `StructureValidator` failure | 400 Bad Request |
| Global | `DbUpdateConcurrencyException` | 409 Conflict |
| Global | `UnauthorizedAccessException` | 403 Forbidden |
| Global | `OperationCanceledException` | 499 Client Closed |
| Global | Unhandled exception | 500 Internal Server Error |

---

## Anti-Patterns

- **Throwing exceptions for business logic** — Use `DomainResult.Failure()` / `Result.Failure()` instead. Exceptions are for unexpected infrastructure failures.
- **Swallowing errors silently** — Every failure must propagate through the Result chain or be logged explicitly.
- **Returning raw error strings** — Always wrap in `ProblemDetails` at the API boundary.
- **Catching generic `Exception` in services** — Catch only specific exceptions (e.g., `DbUpdateConcurrencyException`). Let `DefaultExceptionHandler` handle the rest.

---

## Testing Error Paths

- **Unit tests:** Assert `DomainResult.IsFailure` and `ErrorMessage` content for domain rule violations.
- **Service tests:** Assert `Result.IsFailure` for invalid DTO/tenant boundary violations.
- **Endpoint/integration tests:** Assert `ProblemDetails` shape — `Status`, `Title`, `Detail` fields present with correct HTTP status code.
- **Exception handler tests:** Throw specific exceptions in test, verify `ProblemDetails` response matches mapping table.

---

## Verification Checklist

- [ ] No `throw` statements in domain entities for business validation
- [ ] All service methods return `Result<T>` (never void for operations that can fail)
- [ ] Endpoints use `Result.Match()` — no manual if/else chains for Result inspection
- [ ] `DefaultExceptionHandler` registered and covers all exception types in mapping table
- [ ] Integration tests verify `ProblemDetails` shape for error responses
