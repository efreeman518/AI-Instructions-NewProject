namespace Infrastructure.Data.Configuration;

public class CategoryConfiguration : EntityBaseConfiguration<Category>
{
    public override void Configure(EntityTypeBuilder<Category> builder)
    {
        base.Configure(builder);

        builder.Property(e => e.Name)
            .IsRequired()
            .HasMaxLength(DomainConstants.NAME_MAX_LENGTH);

        builder.Property(e => e.Description)
            .HasMaxLength(DomainConstants.DESCRIPTION_MAX_LENGTH);

        builder.Property(e => e.ColorHex)
            .HasMaxLength(7);

        builder.HasIndex(e => new { e.TenantId, e.Name })
            .IsUnique();
    }
}
