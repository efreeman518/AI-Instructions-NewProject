namespace Infrastructure.Data.Configuration;

public class TeamMemberConfiguration : EntityBaseConfiguration<TeamMember>
{
    public override void Configure(EntityTypeBuilder<TeamMember> builder)
    {
        base.Configure(builder);

        builder.Property(e => e.DisplayName)
            .IsRequired()
            .HasMaxLength(DomainConstants.NAME_MAX_LENGTH);

        builder.Property(e => e.Role)
            .IsRequired();

        builder.HasIndex(e => new { e.TeamId, e.UserId })
            .IsUnique();
    }
}
