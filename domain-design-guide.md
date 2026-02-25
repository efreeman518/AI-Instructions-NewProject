# Domain Design Guide

Design-time guidance for storage selection, relationship modeling, and workflow orchestration hints. This is primarily for domain discovery, not routine scaffolding turns.

---

## Data Store Selection

Set `dataStore` per entity (`sql` default if omitted).

| Signal | `sql` | `cosmosdb` | `table` | `blob` |
|---|---|---|---|---|
| Data shape | fixed relational schema | document/variable schema | flat key-value rows | unstructured file/object |
| Relationships | rich joins/FKs | aggregate-local nesting | none | none |
| Query style | complex filters/joins/reports | partition-aligned point/filtered reads | partition+row key lookups | get/put/list by name/prefix |
| Transaction scope | cross-entity ACID | partition-local atomicity | single row ops | object-level + lease/etag |
| Typical use | business source of truth | denormalized read/event docs | audit/config/counters | attachments/media/exports |

### Quick Choice Rules

- choose `sql` for core business entities, joins, and cross-entity transactions.
- choose `cosmosdb` for self-contained document aggregates and partition-scale reads.
- choose `table` for low-cost append/lookup data keyed by partition/row.
- choose `blob` for large binary/text payloads.

### Common Hybrid Patterns

- SQL metadata + Blob content.
- SQL source of truth + Cosmos read model.
- SQL core + Table audit trail.

### Simplified Decision Flow

1. Binary/file content? -> `blob`.
2. Relational constraints + complex query/reporting? -> `sql`.
3. Simple partition/row key lookups? -> `table`.
4. Otherwise document aggregate with strong partition strategy? -> `cosmosdb`.
5. If uncertain, default to `sql`.

If semantic/vector search is primary across sources, prefer dedicated search infrastructure; if secondary, in-store vector support may be sufficient.

---

## Relationship Modeling

Provide enough input detail for deterministic EF configuration generation.

### One-to-many

```yaml
children:
  - name: Comments
    entity: Comment
    relationship: one-to-many
    cascadeDelete: true
```

```csharp
builder.HasMany(e => e.Comments)
    .WithOne()
    .HasForeignKey("TodoItemId")
    .OnDelete(DeleteBehavior.Cascade);
```

### Reference navigation (restrict delete)

```yaml
navigation:
  - name: Category
    entity: Category
    required: false
    deleteRestrict: true
```

```csharp
builder.HasOne<Category>()
    .WithMany(e => e.TodoItems)
    .HasForeignKey(e => e.CategoryId)
    .OnDelete(DeleteBehavior.Restrict);
```

### Many-to-many via explicit join

```yaml
children:
  - name: Tags
    entity: Tag
    relationship: many-to-many
    joinEntity: TodoItemTag
```

```csharp
builder.HasKey(e => new { e.TodoItemId, e.TagId });
```

### Self-referencing

```yaml
children:
  - name: Children
    entity: TodoItem
    relationship: self-referencing
    selfReferenceKey: ParentId
```

```csharp
builder.HasOne(e => e.Parent)
    .WithMany(e => e.Children)
    .HasForeignKey(e => e.ParentId)
    .OnDelete(DeleteBehavior.Restrict);
```

### Polymorphic join

```yaml
children:
  - name: Attachments
    entity: Attachment
    relationship: polymorphic-join
    polymorphicEntityTypes: [TodoItem, Comment]
```

```csharp
builder.Property(e => e.EntityType).HasConversion<string>().HasMaxLength(50).IsRequired();
builder.Property(e => e.EntityId).IsRequired();
builder.HasIndex(e => new { e.EntityType, e.EntityId });
```

Polymorphic joins resolve parent type at runtime; they do not map as normal FK navigation to multiple parent entity types.

---

## Workflow Hints (Guidance Only)

Complex workflows are not directly code-generated from schema primitives. Use:

- events,
- state machines,
- custom actions,
- scheduled jobs,
- rules,

then compose orchestration in application code.

### Optional `workflows` hint block

```yaml
workflows:
  - name: TodoItemEscalation
    pattern: orchestrator
    involvedEntities: [TodoItem, Team, TeamMember, Reminder, TodoItemHistory]
    compensationRequired: false
    notes: "Escalation thresholds are configurable per tenant"
```

### What AI scaffolding should produce

- orchestrator service shell,
- step method stubs,
- optional compensation stubs when requested,
- DI registration.

No API endpoints are required by default for workflow hints.

---

## When Workflows Are Not Needed

Skip workflow orchestration when CRUD + state transitions fully express the behavior. Add workflows only for multi-entity coordination, retries/compensation, async wait states, or approval chains.