using Microsoft.EntityFrameworkCore.Design;
using System.Diagnostics.CodeAnalysis;

namespace Infrastructure.Data;

[ExcludeFromCodeCoverage]
public class DesignTimeDbContextFactoryTrxn : IDesignTimeDbContextFactory<TaskFlowDbContextTrxn>
{
    public TaskFlowDbContextTrxn CreateDbContext(string[] args)
    {
        string? connString = Environment.GetEnvironmentVariable("EFCORETOOLSDB");

        if (string.IsNullOrEmpty(connString))
            throw new InvalidOperationException("The connection string was not set in the 'EFCORETOOLSDB' environment variable.");

        Console.WriteLine(connString);

        var optionsBuilder = new DbContextOptionsBuilder<TaskFlowDbContextTrxn>();
        optionsBuilder.UseSqlServer(connString);
        return new TaskFlowDbContextTrxn(optionsBuilder.Options) { AuditId = "DesignTimeAuditId", TenantId = Guid.NewGuid() };
    }
}

[ExcludeFromCodeCoverage]
public class DesignTimeDbContextFactoryQuery : IDesignTimeDbContextFactory<TaskFlowDbContextQuery>
{
    public TaskFlowDbContextQuery CreateDbContext(string[] args)
    {
        string? connString = Environment.GetEnvironmentVariable("EFCORETOOLSDB");

        if (string.IsNullOrEmpty(connString))
            throw new InvalidOperationException("The connection string was not set in the 'EFCORETOOLSDB' environment variable.");

        Console.WriteLine(connString);

        var optionsBuilder = new DbContextOptionsBuilder<TaskFlowDbContextQuery>();
        optionsBuilder.UseSqlServer(connString);
        return new TaskFlowDbContextQuery(optionsBuilder.Options) { AuditId = "DesignTimeAuditId", TenantId = Guid.NewGuid() };
    }
}
