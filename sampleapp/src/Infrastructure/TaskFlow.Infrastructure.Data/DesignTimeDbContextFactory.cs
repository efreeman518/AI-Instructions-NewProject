// ═══════════════════════════════════════════════════════════════
// Pattern: DesignTimeDbContextFactory — used by EF Core tooling (Add-Migration, etc.)
// Reads connection string from EFCORETOOLSDB environment variable.
// This avoids a dependency on the runtime DI container during migration generation.
// ═══════════════════════════════════════════════════════════════

using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Design;

namespace Infrastructure;

/// <summary>
/// Factory used by EF Core CLI tools to create a DbContext instance at design time.
/// Usage: Set EFCORETOOLSDB environment variable before running migration commands.
/// Example: $env:EFCORETOOLSDB = "Server=localhost;Database=TaskFlowDb;Trusted_Connection=True;TrustServerCertificate=True"
///          dotnet ef migrations add InitialCreate --project Infrastructure --startup-project TaskFlow.Api
/// </summary>
public class DesignTimeDbContextFactory : IDesignTimeDbContextFactory<TaskFlowDbContextTrxn>
{
    public TaskFlowDbContextTrxn CreateDbContext(string[] args)
    {
        // Pattern: Read connection string from environment variable — never hardcoded.
        var connectionString = Environment.GetEnvironmentVariable("EFCORETOOLSDB")
            ?? throw new InvalidOperationException(
                "Set the EFCORETOOLSDB environment variable to a valid SQL Server connection string. " +
                "Example: $env:EFCORETOOLSDB = \"Server=localhost;Database=TaskFlowDb;...\"");

        var optionsBuilder = new DbContextOptionsBuilder<TaskFlowDbContextTrxn>();

        // Pattern: Detect Azure SQL vs local SQL Server for compatibility level.
        if (connectionString.Contains("database.windows.net", StringComparison.OrdinalIgnoreCase))
        {
            optionsBuilder.UseAzureSql(connectionString, sqlOptions =>
            {
                sqlOptions.UseCompatibilityLevel(170);
                sqlOptions.EnableRetryOnFailure(5, TimeSpan.FromSeconds(30), null);
            });
        }
        else
        {
            optionsBuilder.UseSqlServer(connectionString, sqlOptions =>
            {
                sqlOptions.UseCompatibilityLevel(160);
                sqlOptions.EnableRetryOnFailure(5, TimeSpan.FromSeconds(30), null);
            });
        }

        return new TaskFlowDbContextTrxn(optionsBuilder.Options);
    }
}
