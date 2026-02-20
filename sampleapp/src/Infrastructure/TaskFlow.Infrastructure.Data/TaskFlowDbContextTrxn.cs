// ═══════════════════════════════════════════════════════════════
// Pattern: Transactional DbContext — change-tracked, used for writes.
// Registered as pooled + scoped via DbContextScopedFactory in Bootstrapper.
// AuditInterceptor is added to auto-populate CreatedBy/UpdatedBy fields.
// ═══════════════════════════════════════════════════════════════

using Microsoft.EntityFrameworkCore;

namespace Infrastructure;

/// <summary>
/// Write-optimized DbContext with change tracking enabled (default EF behavior).
/// Used by all *RepositoryTrxn classes for Create, Update, Delete operations.
/// </summary>
public class TaskFlowDbContextTrxn(DbContextOptions<TaskFlowDbContextTrxn> options)
    : TaskFlowDbContextBase(options)
{
    // Pattern: No overrides needed — inherits all configuration from base.
    // Change tracking is enabled by default.
    // AuditInterceptor is injected at registration time (see Bootstrapper).
}
