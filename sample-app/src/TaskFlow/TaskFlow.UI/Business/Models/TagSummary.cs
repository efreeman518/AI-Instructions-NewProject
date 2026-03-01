namespace TaskFlow.UI.Business.Models;

/// <summary>
/// UI model for tags.
/// </summary>
public partial record TagSummary
{
    public Guid Id { get; init; }
    public string Name { get; init; } = string.Empty;
    public string? Description { get; init; }
}
