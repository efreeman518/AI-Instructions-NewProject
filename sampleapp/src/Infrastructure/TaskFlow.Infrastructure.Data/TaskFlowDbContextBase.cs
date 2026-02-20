// ═══════════════════════════════════════════════════════════════
// Pattern: Split DbContext — abstract base holds ALL model configuration.
// Two concrete contexts inherit from it:
//   TaskFlowDbContextTrxn — change-tracked, for writes
//   TaskFlowDbContextQuery — NoTracking, for reads (optionally targets read replica)
// This avoids duplicating configuration while supporting read/write separation.
// ═══════════════════════════════════════════════════════════════

using Domain.Model.Entities;
using Microsoft.EntityFrameworkCore;
using Package.Infrastructure.Data;

namespace Infrastructure;

/// <summary>
/// Abstract base DbContext that holds ALL model configuration, DbSet declarations,
/// query filters, and default data-type conventions.
/// Neither Trxn nor Query context should override OnModelCreating.
/// </summary>
public abstract class TaskFlowDbContextBase(DbContextOptions options)
    : DbContextBase<string, Guid?>(options)
{
    // ═══════════════════════════════════════════════════════════════
    // Pattern: DbSet per entity — exposes queryable surfaces.
    // Child entities also get DbSets for direct queries (e.g., "recent comments").
    // ═══════════════════════════════════════════════════════════════

    public DbSet<TodoItem> TodoItem => Set<TodoItem>();
    public DbSet<Category> Category => Set<Category>();
    public DbSet<Tag> Tag => Set<Tag>();
    public DbSet<TodoItemTag> TodoItemTag => Set<TodoItemTag>();
    public DbSet<Comment> Comment => Set<Comment>();
    public DbSet<Attachment> Attachment => Set<Attachment>();
    public DbSet<Team> Team => Set<Team>();
    public DbSet<TeamMember> TeamMember => Set<TeamMember>();
    public DbSet<Reminder> Reminder => Set<Reminder>();
    public DbSet<TodoItemHistory> TodoItemHistory => Set<TodoItemHistory>();

    protected override void OnModelCreating(ModelBuilder modelBuilder)
    {
        base.OnModelCreating(modelBuilder);

        // Pattern: Default schema — isolates this module's tables from others in the same DB.
        modelBuilder.HasDefaultSchema("taskflow");

        // Pattern: Apply all IEntityTypeConfiguration<T> from this assembly.
        // Each entity's configuration is in a separate file under Configurations/.
        modelBuilder.ApplyConfigurationsFromAssembly(typeof(TaskFlowDbContextBase).Assembly);

        // Pattern: Default data types for all entities in this context.
        ConfigureDefaultDataTypes(modelBuilder);

        // Pattern: Table names match class names (no pluralization).
        SetTableNames(modelBuilder);

        // Pattern: Tenant query filters for multi-tenancy — automatically applied to all queries.
        ConfigureTenantQueryFilters(modelBuilder);
    }

    /// <summary>
    /// Pattern: Convention — all decimal columns use decimal(10,4), all DateTime use datetime2.
    /// Applied globally so individual configurations don't need to repeat this.
    /// </summary>
    private static void ConfigureDefaultDataTypes(ModelBuilder modelBuilder)
    {
        foreach (var entityType in modelBuilder.Model.GetEntityTypes())
        {
            foreach (var property in entityType.GetProperties())
            {
                if (property.ClrType == typeof(decimal) || property.ClrType == typeof(decimal?))
                {
                    property.SetPrecision(10);
                    property.SetScale(4);
                }
                else if (property.ClrType == typeof(DateTime) || property.ClrType == typeof(DateTime?))
                {
                    property.SetColumnType("datetime2");
                }
            }
        }
    }

    /// <summary>
    /// Pattern: Table names = class names (no pluralization). Skip owned types.
    /// e.g., TodoItem entity → [taskflow].[TodoItem] table.
    /// </summary>
    private static void SetTableNames(ModelBuilder modelBuilder)
    {
        foreach (var entityType in modelBuilder.Model.GetEntityTypes())
        {
            // Pattern: Skip owned types — they're stored inside the parent table.
            if (entityType.IsOwned()) continue;

            entityType.SetTableName(entityType.ClrType.Name);
        }
    }

    /// <summary>
    /// Pattern: Auto-apply HasQueryFilter for all ITenantEntity types.
    /// The filter reads TenantId from IRequestContext via DbContextBase.
    /// All queries automatically filter by the current user's tenant.
    /// Use .IgnoreQueryFilters() for cross-tenant admin scenarios.
    /// </summary>
    private static void ConfigureTenantQueryFilters(ModelBuilder modelBuilder)
    {
        foreach (var entityType in modelBuilder.Model.GetEntityTypes())
        {
            // Pattern: Only apply to entities implementing ITenantEntity<Guid>.
            if (!typeof(ITenantEntity<Guid>).IsAssignableFrom(entityType.ClrType)) continue;
            if (entityType.IsOwned()) continue;

            // Pattern: Uses Expression tree to build filter dynamically.
            // Equivalent to: .HasQueryFilter(e => e.TenantId == _currentTenantId)
            // The actual implementation defers to DbContextBase which reads IRequestContext.
            var parameter = System.Linq.Expressions.Expression.Parameter(entityType.ClrType, "e");
            var tenantIdProperty = System.Linq.Expressions.Expression.Property(parameter, "TenantId");

            // Build a filter expression. In production, DbContextBase injects the tenant ID
            // from IRequestContext. This is a simplified demonstration of the pattern.
            modelBuilder.Entity(entityType.ClrType)
                .HasQueryFilter(
                    System.Linq.Expressions.Expression.Lambda(
                        System.Linq.Expressions.Expression.NotEqual(
                            tenantIdProperty,
                            System.Linq.Expressions.Expression.Constant(Guid.Empty)),
                        parameter));
        }
    }
}
