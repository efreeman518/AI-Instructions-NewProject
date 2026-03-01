namespace Infrastructure.Data.Configuration;

public class AttachmentConfiguration : EntityBaseConfiguration<Attachment>
{
    public override void Configure(EntityTypeBuilder<Attachment> builder)
    {
        base.Configure(builder);

        builder.Property(e => e.FileName)
            .IsRequired()
            .HasMaxLength(DomainConstants.NAME_MAX_LENGTH);

        builder.Property(e => e.ContentType)
            .IsRequired()
            .HasMaxLength(100);

        builder.Property(e => e.BlobUri)
            .IsRequired()
            .HasMaxLength(DomainConstants.URL_MAX_LENGTH);

        builder.HasIndex(e => new { e.EntityType, e.EntityId });
    }
}
