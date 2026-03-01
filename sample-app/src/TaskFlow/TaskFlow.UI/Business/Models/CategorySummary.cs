namespace TaskFlow.UI.Business.Models;

/// <summary>
/// UI model for categories.
/// </summary>
public partial record CategorySummary
{
    public Guid Id { get; init; }
    public string Name { get; init; } = string.Empty;
    public string? Description { get; init; }
    public string? ColorHex { get; init; }
    public int DisplayOrder { get; init; }
    public bool IsActive { get; init; } = true;
}
