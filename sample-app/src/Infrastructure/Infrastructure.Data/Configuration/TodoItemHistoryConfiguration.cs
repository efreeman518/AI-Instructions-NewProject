namespace Infrastructure.Data.Configuration;

public class TodoItemHistoryConfiguration : EntityBaseConfiguration<TodoItemHistory>
{
    public override void Configure(EntityTypeBuilder<TodoItemHistory> builder)
    {
        base.Configure(builder);

        builder.Property(e => e.Action)
            .IsRequired()
            .HasMaxLength(DomainConstants.NAME_MAX_LENGTH);

        builder.Property(e => e.ChangeDescription)
            .HasMaxLength(DomainConstants.DESCRIPTION_MAX_LENGTH);

        builder.Property(e => e.ChangedBy)
            .IsRequired();

        builder.Property(e => e.ChangedAt)
            .IsRequired();

        builder.HasIndex(e => e.TodoItemId);
        builder.HasIndex(e => e.ChangedAt);
    }
}
