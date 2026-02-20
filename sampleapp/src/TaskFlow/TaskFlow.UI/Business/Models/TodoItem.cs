// ═══════════════════════════════════════════════════════════════
// Pattern: Client-side TodoItem record — wraps the Kiota-generated wire DTO.
// Immutable record with init properties.
// Provides internal constructor from wire DTO and ToData() for POST/PUT.
//
// This is NOT the domain entity — it's a client-side representation
// optimized for UI binding with display-friendly computed properties.
// ═══════════════════════════════════════════════════════════════

namespace TaskFlow.UI.Business.Models;

/// <summary>
/// Client-side immutable record for TodoItem.
/// Wraps the Kiota-generated wire DTO (TodoItemData).
/// </summary>
public partial record TodoItem : IEntityBase
{
    // Pattern: Internal constructor from Kiota wire DTO.
    // In a real project, TodoItemData comes from the Client/ folder (Kiota-generated).
    // For this sample, we show the mapping pattern with contrived data.
    internal TodoItem(TodoItemData data)
    {
        Id = data.Id ?? Guid.Empty;
        TenantId = data.TenantId ?? Guid.Empty;
        Title = data.Title;
        Description = data.Description;
        Priority = data.Priority ?? 1;
        IsCompleted = data.IsCompleted ?? false;
        CategoryName = data.CategoryName;
        DueDate = data.DueDate;
    }

    // Pattern: Parameterless constructor for create/form scenarios.
    public TodoItem() { }

    public Guid Id { get; init; }
    public Guid TenantId { get; init; }
    public string? Title { get; init; }
    public string? Description { get; init; }
    public int Priority { get; init; } = 1;
    public bool IsCompleted { get; init; }
    public string? CategoryName { get; init; }
    public DateTimeOffset? DueDate { get; init; }

    // Pattern: Computed display properties — expression-bodied for clean XAML binding.
    public string DisplayPriority => Priority switch
    {
        1 => "🔵 Low",
        2 => "🟡 Medium",
        3 => "🟠 High",
        4 => "🔴 Critical",
        _ => $"Priority {Priority}"
    };

    public string DisplayStatus => IsCompleted ? "✅ Done" : "⬜ Open";

    public bool IsOverdue => DueDate.HasValue && DueDate.Value < DateTimeOffset.UtcNow && !IsCompleted;

    /// <summary>
    /// Pattern: Convert back to Kiota wire DTO for POST/PUT requests.
    /// </summary>
    internal TodoItemData ToData() => new()
    {
        Id = Id,
        TenantId = TenantId,
        Title = Title,
        Description = Description,
        Priority = Priority,
        IsCompleted = IsCompleted,
        CategoryName = CategoryName,
        DueDate = DueDate
    };
}

// ═══════════════════════════════════════════════════════════════
// Pattern: Contrived wire DTO — in a real project this is Kiota-generated
// from the Gateway OpenAPI spec. Shown here for pattern completeness.
// ═══════════════════════════════════════════════════════════════
public class TodoItemData
{
    public Guid? Id { get; set; }
    public Guid? TenantId { get; set; }
    public string? Title { get; set; }
    public string? Description { get; set; }
    public int? Priority { get; set; }
    public bool? IsCompleted { get; set; }
    public string? CategoryName { get; set; }
    public DateTimeOffset? DueDate { get; set; }
}
