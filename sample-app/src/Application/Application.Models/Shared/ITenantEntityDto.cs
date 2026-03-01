namespace Application.Models.Shared;

/// <summary>
/// Marker interface for tenant-scoped DTOs.
/// </summary>
public interface ITenantEntityDto
{
    Guid TenantId { get; set; }
}
