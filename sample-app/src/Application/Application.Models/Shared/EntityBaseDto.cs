namespace Application.Models.Shared;

/// <summary>
/// Base record for all DTOs with a Guid identifier.
/// Auditing is handled by EF interceptor (AuditEntry), not entity properties.
/// </summary>
public record EntityBaseDto
{
    public Guid Id { get; set; }
}
