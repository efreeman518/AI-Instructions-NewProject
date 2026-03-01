namespace Application.Models;

public record TeamMemberDto : EntityBaseDto, ITenantEntityDto
{
    [Required]
    public Guid TenantId { get; set; }

    [Required]
    public Guid TeamId { get; set; }

    [Required]
    public Guid UserId { get; set; }

    [Required]
    public string DisplayName { get; set; } = null!;

    public TeamMemberRole Role { get; set; } = TeamMemberRole.Member;
    public decimal? HourlyRate { get; set; }
    public DateTimeOffset JoinedAt { get; set; }
}
