using EF.Data;
using EF.Domain;
using EF.Domain.Contracts;

namespace Infrastructure.Data;

public abstract class TaskFlowDbContextBase(DbContextOptions options) : DbContextBase<string, Guid?>(options)
{
    protected override void OnModelCreating(ModelBuilder modelBuilder)
    {
        base.OnModelCreating(modelBuilder);

        modelBuilder.HasDefaultSchema("taskflow");

        modelBuilder.ApplyConfigurationsFromAssembly(typeof(TaskFlowDbContextBase).Assembly);

        ConfigureDefaultDataTypes(modelBuilder);
        SetTableNames(modelBuilder);
        ConfigureTenantQueryFilters(modelBuilder);
    }

    private static void SetTableNames(ModelBuilder modelBuilder)
    {
        foreach (var entity in modelBuilder.Model.GetEntityTypes())
        {
            if (entity.IsOwned()) continue;
            var current = entity.GetTableName();
            if (string.IsNullOrWhiteSpace(current))
            {
                entity.SetTableName(entity.DisplayName());
            }
        }
    }

    private static void ConfigureDefaultDataTypes(ModelBuilder modelBuilder)
    {
        var decimalProperties = modelBuilder.Model.GetEntityTypes()
            .SelectMany(t => t.GetProperties())
            .Where(p => p.ClrType == typeof(decimal) || p.ClrType == typeof(decimal?))
            .Where(p => p.GetColumnType() == null);

        foreach (var property in decimalProperties)
        {
            property.SetColumnType("decimal(10,4)");
        }

        var dateProperties = modelBuilder.Model.GetEntityTypes()
            .SelectMany(t => t.GetProperties())
            .Where(p => p.ClrType == typeof(DateTime) || p.ClrType == typeof(DateTime?))
            .Where(p => p.GetColumnType() == null);

        foreach (var property in dateProperties)
        {
            property.SetColumnType("datetime2");
        }
    }

    private void ConfigureTenantQueryFilters(ModelBuilder modelBuilder)
    {
        var tenantEntityClrTypes = modelBuilder.Model.GetEntityTypes()
            .Where(entityType => typeof(ITenantEntity<Guid>).IsAssignableFrom(entityType.ClrType))
            .Select(entityType => entityType.ClrType);

        foreach (var clrType in tenantEntityClrTypes)
        {
            var filter = BuildTenantFilter(clrType);
            modelBuilder.Entity(clrType).HasQueryFilter(filter);
        }
    }

    // DbSets
    public DbSet<TodoItem> TodoItems { get; set; } = null!;
    public DbSet<Category> Categories { get; set; } = null!;
    public DbSet<Tag> Tags { get; set; } = null!;
    public DbSet<Team> Teams { get; set; } = null!;
    public DbSet<TeamMember> TeamMembers { get; set; } = null!;
    public DbSet<Comment> Comments { get; set; } = null!;
    public DbSet<Attachment> Attachments { get; set; } = null!;
    public DbSet<Reminder> Reminders { get; set; } = null!;
    public DbSet<TodoItemHistory> TodoItemHistories { get; set; } = null!;
}
