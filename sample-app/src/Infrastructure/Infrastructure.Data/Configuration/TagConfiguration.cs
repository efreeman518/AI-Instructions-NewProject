namespace Infrastructure.Data.Configuration;

public class TagConfiguration : EntityBaseConfiguration<Tag>
{
    public override void Configure(EntityTypeBuilder<Tag> builder)
    {
        base.Configure(builder);

        builder.Property(e => e.Name)
            .IsRequired()
            .HasMaxLength(DomainConstants.NAME_MAX_LENGTH);

        builder.Property(e => e.Description)
            .HasMaxLength(DomainConstants.DESCRIPTION_MAX_LENGTH);

        builder.HasIndex(e => e.Name)
            .IsUnique();
    }
}
