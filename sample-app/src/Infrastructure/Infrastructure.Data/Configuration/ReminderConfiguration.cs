namespace Infrastructure.Data.Configuration;

public class ReminderConfiguration : EntityBaseConfiguration<Reminder>
{
    public override void Configure(EntityTypeBuilder<Reminder> builder)
    {
        base.Configure(builder);

        builder.Property(e => e.Type)
            .IsRequired();

        builder.Property(e => e.CronExpression)
            .HasMaxLength(100);

        builder.HasIndex(e => e.TodoItemId);
        builder.HasIndex(e => e.RemindAt);
    }
}
