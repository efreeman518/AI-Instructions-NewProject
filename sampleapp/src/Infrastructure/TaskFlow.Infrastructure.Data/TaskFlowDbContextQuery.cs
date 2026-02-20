// ═══════════════════════════════════════════════════════════════
// Pattern: Query DbContext — NoTracking by default, optimized for reads.
// Optionally targets a read replica via ApplicationIntent=ReadOnly in conn string.
// Registered as pooled + scoped via DbContextScopedFactory in Bootstrapper.
// NO AuditInterceptor — reads don't need it.
// ═══════════════════════════════════════════════════════════════

using Microsoft.EntityFrameworkCore;

namespace Infrastructure;

/// <summary>
/// Read-optimized DbContext with NoTracking behavior.
/// Used by all *RepositoryQuery classes for Search, Lookup, GetById operations.
/// In production, can target a SQL Server read replica for better performance.
/// </summary>
public class TaskFlowDbContextQuery(DbContextOptions<TaskFlowDbContextQuery> options)
    : TaskFlowDbContextBase(options)
{
    // Pattern: No overrides needed — NoTracking is configured at registration time.
    // See Bootstrapper: AddPooledDbContextFactory with UseQueryTrackingBehavior(NoTracking).
}
