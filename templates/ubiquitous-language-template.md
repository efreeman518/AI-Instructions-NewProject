# Ubiquitous Language - {{ProjectName}}

This file records the shared language between developer and AI. Use these terms in code, tests, docs, prompts, API contracts, and comments. Prefer a recorded term over synonyms.

## Purpose

- Business domain:
- Primary users:
- Success criteria:

## Accepted Terms

| Term | Type | Meaning | Code/Naming Guidance |
|---|---|---|---|
| `{{Entity}}` | entity | _Business meaning._ | Use `{{Entity}}` in class, DTO, service, endpoint, and test names. |

Term types: entity, aggregate, value-object, role, command, state, event, policy, external-system, UI-label.

## Rejected Synonyms

| Rejected Term | Use Instead | Reason |
|---|---|---|
| `Task` | `WorkItem` | Avoid `System.Threading.Tasks.Task` collision. |

## Entities And Aggregates

| Entity | Aggregate Role | Tenant Scope | Ownership Notes |
|---|---|---|---|
| `{{Entity}}` | root | tenant-scoped | _Owned children and references._ |

## Value Objects

| Value Object | Meaning | Used By | Equality Boundary |
|---|---|---|---|
| `{{ValueObject}}` | _Business concept._ | `{{Entity}}` | _Fields that define identity._ |

## Roles And Actors

| Role/Actor | Meaning | Permissions Language |
|---|---|---|
| `{{Role}}` | _Who this represents._ | _Allowed actions in business terms._ |

## Commands And Actions

| Command/Action | Actor | Target | Business Meaning | Expected Result |
|---|---|---|---|---|
| `Create{{Entity}}` | `{{Role}}` | `{{Entity}}` | _What the business says happened._ | _State/event/result._ |

## States

| Entity | State | Meaning | Terminal |
|---|---|---|---|
| `{{Entity}}` | `{{State}}` | _Business condition._ | no |

## Events

| Event | Raised By | Meaning | Consumers |
|---|---|---|---|
| `{{Entity}}Created` | `{{Entity}}` | _Business fact after it occurs._ | _Workflow/search/notification/etc._ |

## Policies And Rules

| Policy/Rule | Applies To | Meaning | Decision Source |
|---|---|---|---|
| `{{PolicyName}}` | `{{Entity}}` | _Business constraint._ | `D-###` |

## External Systems

| System | Domain Meaning | Interaction Vocabulary |
|---|---|---|
| `{{ExternalSystem}}` | _Business role of system._ | _Use verbs/nouns from this row._ |

## Naming Notes

- Use terms exactly as recorded unless C# framework collision requires a recorded alternative.
- Do not introduce abbreviations unless listed here.
- API route labels may be lowercase/kebab-case, but source names should use the accepted PascalCase term.
- Tests should use business phrases from this file in test method names where practical.
