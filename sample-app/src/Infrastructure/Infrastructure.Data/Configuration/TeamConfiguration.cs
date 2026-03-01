namespace Infrastructure.Data.Configuration;

public class TeamConfiguration : EntityBaseConfiguration<Team>
{
    public override void Configure(EntityTypeBuilder<Team> builder)
    {
        base.Configure(builder);

        builder.Property(e => e.Name)
            .IsRequired()
            .HasMaxLength(DomainConstants.NAME_MAX_LENGTH);

        builder.Property(e => e.Description)
            .HasMaxLength(DomainConstants.DESCRIPTION_MAX_LENGTH);

        builder.HasMany(e => e.Members)
            .WithOne(m => m.Team)
            .HasForeignKey(m => m.TeamId)
            .OnDelete(DeleteBehavior.Cascade);

        builder.HasIndex(e => new { e.TenantId, e.Name })
            .IsUnique();
    }
}
