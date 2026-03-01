namespace Infrastructure.Data.Configuration;

public class CommentConfiguration : EntityBaseConfiguration<Comment>
{
    public override void Configure(EntityTypeBuilder<Comment> builder)
    {
        base.Configure(builder);

        builder.Property(e => e.Text)
            .IsRequired()
            .HasMaxLength(DomainConstants.COMMENT_MAX_LENGTH);

        builder.Property(e => e.AuthorId)
            .IsRequired();

        builder.HasIndex(e => e.TodoItemId);
    }
}
