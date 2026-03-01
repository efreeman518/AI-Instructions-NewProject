namespace Infrastructure.Data.Configuration;

public class TodoItemConfiguration : EntityBaseConfiguration<TodoItem>
{
    public override void Configure(EntityTypeBuilder<TodoItem> builder)
    {
        base.Configure(builder);

        builder.Property(e => e.Title)
            .IsRequired()
            .HasMaxLength(DomainConstants.TITLE_MAX_LENGTH);

        builder.Property(e => e.Description)
            .HasMaxLength(DomainConstants.DESCRIPTION_MAX_LENGTH);

        builder.Property(e => e.Status)
            .IsRequired();

        builder.Property(e => e.Priority)
            .IsRequired();

        builder.OwnsOne(e => e.Schedule, schedule =>
        {
            schedule.Property(d => d.StartDate).HasColumnName("StartDate");
            schedule.Property(d => d.DueDate).HasColumnName("DueDate");
        });

        builder.HasOne(e => e.Category)
            .WithMany()
            .HasForeignKey(e => e.CategoryId)
            .OnDelete(DeleteBehavior.SetNull);

        builder.HasOne(e => e.Team)
            .WithMany()
            .HasForeignKey(e => e.TeamId)
            .OnDelete(DeleteBehavior.SetNull);

        builder.HasOne(e => e.Parent)
            .WithMany(e => e.Children)
            .HasForeignKey(e => e.ParentId)
            .OnDelete(DeleteBehavior.Restrict);

        builder.HasOne(e => e.AssignedTo)
            .WithMany()
            .HasForeignKey(e => e.AssignedToId)
            .OnDelete(DeleteBehavior.ClientSetNull);

        builder.HasMany(e => e.Comments)
            .WithOne(c => c.TodoItem)
            .HasForeignKey(c => c.TodoItemId)
            .OnDelete(DeleteBehavior.Cascade);

        builder.HasMany(e => e.Attachments)
            .WithOne()
            .HasForeignKey(a => a.EntityId)
            .OnDelete(DeleteBehavior.Cascade);

        builder.HasMany(e => e.Reminders)
            .WithOne(r => r.TodoItem)
            .HasForeignKey(r => r.TodoItemId)
            .OnDelete(DeleteBehavior.Cascade);

        builder.HasMany(e => e.History)
            .WithOne(h => h.TodoItem)
            .HasForeignKey(h => h.TodoItemId)
            .OnDelete(DeleteBehavior.Cascade);

        builder.HasMany(e => e.Tags)
            .WithMany(t => t.TodoItems);

        builder.HasIndex(e => e.TenantId);
        builder.HasIndex(e => e.Status);
        builder.HasIndex(e => e.AssignedToId);
        builder.HasIndex(e => e.CategoryId);
    }
}
