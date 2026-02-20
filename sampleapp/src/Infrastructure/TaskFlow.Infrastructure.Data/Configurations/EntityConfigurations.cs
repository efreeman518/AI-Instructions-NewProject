// ═══════════════════════════════════════════════════════════════
// Pattern: Entity Configuration — each entity gets its own IEntityTypeConfiguration<T>.
// All configuration files live in Infrastructure/Configurations/ and are discovered
// by ApplyConfigurationsFromAssembly in the DbContext.
// ═══════════════════════════════════════════════════════════════

using Domain.Model.Entities;
using Domain.Model.Enums;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;
using Package.Infrastructure.Data;

namespace Infrastructure.Configurations;

/// <summary>
/// Pattern: Rich entity configuration — demonstrates ALL EF configuration patterns:
/// - Non-clustered PK (false to EntityBaseConfiguration) with clustered composite index
/// - Owned value object (DateRange)
/// - Self-referencing hierarchy (ParentId → TodoItem)
/// - Enum with default value
/// - Relationship cascade behaviors
/// - Named indexes with TenantId leading
/// - String max lengths on every string property
/// </summary>
public class TodoItemConfiguration : EntityBaseConfiguration<TodoItem>
{
    public TodoItemConfiguration() : base(pkClusteredIndex: false) { }

    public override void Configure(EntityTypeBuilder<TodoItem> builder)
    {
        // Pattern: Always call base first — configures Id, RowVersion, audit fields.
        base.Configure(builder);

        // Pattern: Explicit table name matching class name.
        builder.ToTable("TodoItem");

        // ═══════════════════════════════════════════════════════════════
        // Pattern: Clustered composite index on (TenantId, Id) — this is the
        // physical sort order on disk. PK is NOT clustered (set via base ctor).
        // This dramatically improves tenant-scoped query performance.
        // ═══════════════════════════════════════════════════════════════
        builder.HasIndex(e => new { e.TenantId, e.Id })
            .HasDatabaseName("CIX_TodoItem_TenantId_Id")
            .IsUnique()
            .IsClustered();

        // ═══════════════════════════════════════════════════════════════
        // String max lengths — REQUIRED for every string property.
        // ═══════════════════════════════════════════════════════════════
        builder.Property(e => e.Title).HasMaxLength(200).IsRequired();
        builder.Property(e => e.Description).HasMaxLength(2000);
        builder.Property(e => e.CreatedBy).HasMaxLength(200);
        builder.Property(e => e.UpdatedBy).HasMaxLength(200);

        // ═══════════════════════════════════════════════════════════════
        // Pattern: Enum with default value — store as int, set default.
        // Optional: HasConversion<string>() for human-readable DB values.
        // ═══════════════════════════════════════════════════════════════
        builder.Property(e => e.Status)
            .HasDefaultValue(TodoItemStatus.None);

        // ═══════════════════════════════════════════════════════════════
        // Pattern: Owned value object — stored as columns in the parent table.
        // DateRange becomes TodoItem.DateRange_StartDate, TodoItem.DateRange_EndDate.
        // ═══════════════════════════════════════════════════════════════
        builder.OwnsOne(e => e.DateRange, dr =>
        {
            dr.Property(d => d.StartDate).HasColumnName("DateRange_StartDate");
            dr.Property(d => d.EndDate).HasColumnName("DateRange_EndDate");
        });

        // ═══════════════════════════════════════════════════════════════
        // Pattern: Self-referencing hierarchy — ParentId → TodoItem.
        // Restrict delete prevents orphaning children.
        // ═══════════════════════════════════════════════════════════════
        builder.HasOne(e => e.Parent)
            .WithMany(e => e.SubTasks)
            .HasForeignKey(e => e.ParentId)
            .OnDelete(DeleteBehavior.Restrict);

        // ═══════════════════════════════════════════════════════════════
        // Pattern: 1:Many relationships — Cascade for owned children.
        // Comments and Reminders can't exist without their parent TodoItem.
        // ═══════════════════════════════════════════════════════════════
        builder.HasMany(e => e.Comments)
            .WithOne()
            .HasForeignKey(c => c.TodoItemId)
            .OnDelete(DeleteBehavior.Cascade);

        builder.HasMany<Reminder>()
            .WithOne()
            .HasForeignKey(r => r.TodoItemId)
            .OnDelete(DeleteBehavior.Cascade);

        // Pattern: Reference relationships — Restrict delete.
        builder.HasOne<Category>()
            .WithMany()
            .HasForeignKey(e => e.CategoryId)
            .OnDelete(DeleteBehavior.Restrict);

        builder.HasOne<Team>()
            .WithMany()
            .HasForeignKey(e => e.TeamId)
            .OnDelete(DeleteBehavior.Restrict);

        // ═══════════════════════════════════════════════════════════════
        // Pattern: Named indexes — always include TenantId as leading column.
        // ═══════════════════════════════════════════════════════════════
        builder.HasIndex(e => new { e.TenantId, e.Status })
            .HasDatabaseName("IX_TodoItem_TenantId_Status");

        builder.HasIndex(e => new { e.TenantId, e.CategoryId })
            .HasDatabaseName("IX_TodoItem_TenantId_CategoryId");

        builder.HasIndex(e => new { e.TenantId, e.AssignedToId })
            .HasDatabaseName("IX_TodoItem_TenantId_AssignedToId");

        builder.HasIndex(e => new { e.TenantId, e.ParentId })
            .HasDatabaseName("IX_TodoItem_TenantId_ParentId");
    }
}

/// <summary>
/// Pattern: Simple tenant entity configuration — Category is cacheable static data.
/// Demonstrates basic configuration with clustered composite index.
/// </summary>
public class CategoryConfiguration : EntityBaseConfiguration<Category>
{
    public CategoryConfiguration() : base(pkClusteredIndex: false) { }

    public override void Configure(EntityTypeBuilder<Category> builder)
    {
        base.Configure(builder);
        builder.ToTable("Category");

        builder.HasIndex(e => new { e.TenantId, e.Id })
            .HasDatabaseName("CIX_Category_TenantId_Id")
            .IsUnique()
            .IsClustered();

        builder.Property(e => e.Name).HasMaxLength(100).IsRequired();
        builder.Property(e => e.Description).HasMaxLength(500);
        builder.Property(e => e.ColorHex).HasMaxLength(7);

        // Pattern: Unique name per tenant — prevents duplicate categories.
        builder.HasIndex(e => new { e.TenantId, e.Name })
            .HasDatabaseName("UX_Category_TenantId_Name")
            .IsUnique();
    }
}

/// <summary>
/// Pattern: Non-tenant (global) entity — Tag has no TenantId.
/// PK IS clustered because there's no composite tenant index.
/// </summary>
public class TagConfiguration : EntityBaseConfiguration<Tag>
{
    public TagConfiguration() : base(pkClusteredIndex: true) { }

    public override void Configure(EntityTypeBuilder<Tag> builder)
    {
        base.Configure(builder);
        builder.ToTable("Tag");

        builder.Property(e => e.Name).HasMaxLength(50).IsRequired();

        // Pattern: Global unique constraint — tags are shared across all tenants.
        builder.HasIndex(e => e.Name)
            .HasDatabaseName("UX_Tag_Name")
            .IsUnique();
    }
}

/// <summary>
/// Pattern: Many-to-many junction entity — composite PK, no separate Id.
/// Uses HasKey on (TodoItemId, TagId) — EF will not generate an Id column.
/// </summary>
public class TodoItemTagConfiguration : IEntityTypeConfiguration<TodoItemTag>
{
    public void Configure(EntityTypeBuilder<TodoItemTag> builder)
    {
        builder.ToTable("TodoItemTag");

        // Pattern: Composite primary key for junction tables.
        builder.HasKey(e => new { e.TodoItemId, e.TagId })
            .IsClustered();

        builder.HasOne<TodoItem>()
            .WithMany(t => t.TodoItemTags)
            .HasForeignKey(e => e.TodoItemId)
            .OnDelete(DeleteBehavior.Cascade);

        builder.HasOne<Tag>()
            .WithMany()
            .HasForeignKey(e => e.TagId)
            .OnDelete(DeleteBehavior.Cascade);
    }
}

/// <summary>
/// Pattern: Append-only child entity — Comments are never updated, only added.
/// </summary>
public class CommentConfiguration : EntityBaseConfiguration<Comment>
{
    public CommentConfiguration() : base(pkClusteredIndex: false) { }

    public override void Configure(EntityTypeBuilder<Comment> builder)
    {
        base.Configure(builder);
        builder.ToTable("Comment");

        builder.HasIndex(e => new { e.TodoItemId, e.Id })
            .HasDatabaseName("CIX_Comment_TodoItemId_Id")
            .IsUnique()
            .IsClustered();

        builder.Property(e => e.Text).HasMaxLength(2000).IsRequired();
        builder.Property(e => e.AuthorId).IsRequired();
        builder.Property(e => e.AuthorName).HasMaxLength(200).IsRequired();
    }
}

/// <summary>
/// Pattern: Polymorphic entity via EntityType discriminator — Attachment can belong
/// to TodoItem, Comment, or Team. Uses EntityType enum + EntityId (Guid) columns.
/// This avoids multiple nullable FK columns.
/// </summary>
public class AttachmentConfiguration : EntityBaseConfiguration<Attachment>
{
    public AttachmentConfiguration() : base(pkClusteredIndex: false) { }

    public override void Configure(EntityTypeBuilder<Attachment> builder)
    {
        base.Configure(builder);
        builder.ToTable("Attachment");

        builder.HasIndex(e => new { e.TenantId, e.Id })
            .HasDatabaseName("CIX_Attachment_TenantId_Id")
            .IsUnique()
            .IsClustered();

        builder.Property(e => e.FileName).HasMaxLength(255).IsRequired();
        builder.Property(e => e.ContentType).HasMaxLength(100).IsRequired();
        builder.Property(e => e.StorageUrl).HasMaxLength(2000).IsRequired();

        // Pattern: Enum stored as string for readability in polymorphic discriminator.
        builder.Property(e => e.EntityType)
            .HasConversion<string>()
            .HasMaxLength(50);

        // Pattern: Composite index on discriminator columns for efficient lookups.
        builder.HasIndex(e => new { e.EntityType, e.EntityId })
            .HasDatabaseName("IX_Attachment_EntityType_EntityId");
    }
}

/// <summary>
/// Pattern: Parent entity with child collection management (Team → TeamMembers).
/// </summary>
public class TeamConfiguration : EntityBaseConfiguration<Team>
{
    public TeamConfiguration() : base(pkClusteredIndex: false) { }

    public override void Configure(EntityTypeBuilder<Team> builder)
    {
        base.Configure(builder);
        builder.ToTable("Team");

        builder.HasIndex(e => new { e.TenantId, e.Id })
            .HasDatabaseName("CIX_Team_TenantId_Id")
            .IsUnique()
            .IsClustered();

        builder.Property(e => e.Name).HasMaxLength(100).IsRequired();
        builder.Property(e => e.Description).HasMaxLength(500);

        builder.HasMany(e => e.Members)
            .WithOne()
            .HasForeignKey(m => m.TeamId)
            .OnDelete(DeleteBehavior.Cascade);
    }
}

/// <summary>
/// Pattern: Child entity of Team — TeamMember.
/// </summary>
public class TeamMemberConfiguration : EntityBaseConfiguration<TeamMember>
{
    public TeamMemberConfiguration() : base(pkClusteredIndex: false) { }

    public override void Configure(EntityTypeBuilder<TeamMember> builder)
    {
        base.Configure(builder);
        builder.ToTable("TeamMember");

        builder.HasIndex(e => new { e.TeamId, e.Id })
            .HasDatabaseName("CIX_TeamMember_TeamId_Id")
            .IsUnique()
            .IsClustered();

        builder.Property(e => e.UserName).HasMaxLength(200).IsRequired();
        builder.Property(e => e.Email).HasMaxLength(200);

        // Pattern: Enum default value.
        builder.Property(e => e.Role)
            .HasDefaultValue(MemberRole.Member);

        // Pattern: Unique member per team — a user can only be in a team once.
        builder.HasIndex(e => new { e.TeamId, e.UserId })
            .HasDatabaseName("UX_TeamMember_TeamId_UserId")
            .IsUnique();
    }
}

/// <summary>
/// Pattern: Time-based entity — Reminder has DueDate for scheduler processing.
/// </summary>
public class ReminderConfiguration : EntityBaseConfiguration<Reminder>
{
    public ReminderConfiguration() : base(pkClusteredIndex: false) { }

    public override void Configure(EntityTypeBuilder<Reminder> builder)
    {
        base.Configure(builder);
        builder.ToTable("Reminder");

        builder.HasIndex(e => new { e.TodoItemId, e.Id })
            .HasDatabaseName("CIX_Reminder_TodoItemId_Id")
            .IsUnique()
            .IsClustered();

        builder.Property(e => e.Message).HasMaxLength(500);

        builder.Property(e => e.Type)
            .HasDefaultValue(ReminderType.InApp);

        // Pattern: Index for scheduler queries — find unsent reminders by due date.
        builder.HasIndex(e => new { e.DueDate, e.IsSent })
            .HasDatabaseName("IX_Reminder_DueDate_IsSent")
            .HasFilter("[IsSent] = 0");
    }
}

/// <summary>
/// Pattern: Event-driven read-only entity — TodoItemHistory is created by message handlers
/// in response to domain events. Never updated or deleted via normal operations.
/// </summary>
public class TodoItemHistoryConfiguration : EntityBaseConfiguration<TodoItemHistory>
{
    public TodoItemHistoryConfiguration() : base(pkClusteredIndex: false) { }

    public override void Configure(EntityTypeBuilder<TodoItemHistory> builder)
    {
        base.Configure(builder);
        builder.ToTable("TodoItemHistory");

        builder.HasIndex(e => new { e.TodoItemId, e.Id })
            .HasDatabaseName("CIX_TodoItemHistory_TodoItemId_Id")
            .IsUnique()
            .IsClustered();

        builder.Property(e => e.Action).HasMaxLength(50).IsRequired();
        builder.Property(e => e.PreviousStatus).HasMaxLength(50);
        builder.Property(e => e.NewStatus).HasMaxLength(50);
        builder.Property(e => e.ChangeDescription).HasMaxLength(1000);
        builder.Property(e => e.ChangedBy).HasMaxLength(200).IsRequired();

        // Pattern: Index for timeline queries.
        builder.HasIndex(e => new { e.TodoItemId, e.ChangedAt })
            .HasDatabaseName("IX_TodoItemHistory_TodoItemId_ChangedAt");
    }
}
