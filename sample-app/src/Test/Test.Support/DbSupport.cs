using Infrastructure.Data;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Infrastructure;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.DependencyInjection.Extensions;

namespace Test.Support;

/// <summary>
/// Swaps out DbContext registrations for testing — supports InMemory, TestContainer, or real SQL Server.
/// Pattern from https://github.com/efreeman518/EF.DemoApp1.net/blob/main/Test.Support/DbSupport.cs
/// </summary>
public static class DbSupport
{
    /// <summary>
    /// Swaps out the DbContexts based on connection string for testing.
    /// </summary>
    /// <typeparam name="TTrxn">Transaction DbContext type</typeparam>
    /// <typeparam name="TQuery">Query (read-only) DbContext type</typeparam>
    /// <param name="services">Service collection to modify</param>
    /// <param name="dbConnectionString">Connection string or "UseInMemoryDatabase"</param>
    /// <param name="dbName">Used to name in-memory dbContext</param>
    public static void ConfigureServicesTestDB<TTrxn, TQuery>(IServiceCollection services,
        string? dbConnectionString, string dbName = "TestDB")
        where TTrxn : DbContext
        where TQuery : DbContext
    {
        if (string.IsNullOrEmpty(dbConnectionString)) return;

        // Remove existing registrations
        services.RemoveAll<DbContextOptions<TTrxn>>();
        services.RemoveAll<IDbContextOptionsConfiguration<TTrxn>>();
        services.RemoveAll<TTrxn>();

        services.RemoveAll<DbContextOptions<TQuery>>();
        services.RemoveAll<IDbContextOptionsConfiguration<TQuery>>();
        services.RemoveAll<TQuery>();

        // Also remove pool/factory registrations
        var typesToRemove = services
            .Where(d =>
            {
                var typeName = d.ServiceType.FullName ?? string.Empty;
                return typeName.Contains("TaskFlowDbContext")
                    || typeName.Contains("DbContextPool")
                    || typeName.Contains("DbContextScopedFactory")
                    || typeName.Contains("IDbContextFactory");
            })
            .ToList();

        foreach (var descriptor in typesToRemove)
            services.Remove(descriptor);

        // Re-register with test database — use Scoped lifetime to match production
        // and allow proper audit context resolution per scope.
        services.AddDbContext<TTrxn>((sp, opt) =>
        {
            if (dbConnectionString == "UseInMemoryDatabase")
            {
                opt.UseInMemoryDatabase(dbName);
            }
            else
            {
                opt.UseSqlServer(dbConnectionString, sqlOptions =>
                {
                    sqlOptions.EnableRetryOnFailure(maxRetryCount: 5,
                        maxRetryDelay: TimeSpan.FromSeconds(30),
                        errorNumbersToAdd: null);
                });
            }
        });

        services.AddDbContext<TQuery>((sp, opt) =>
        {
            if (dbConnectionString == "UseInMemoryDatabase")
            {
                opt.UseInMemoryDatabase(dbName);
            }
            else
            {
                opt.UseSqlServer(dbConnectionString, sqlOptions =>
                {
                    sqlOptions.EnableRetryOnFailure(maxRetryCount: 5,
                        maxRetryDelay: TimeSpan.FromSeconds(30),
                        errorNumbersToAdd: null);
                });
            }
        });
    }

    private static readonly Random _R = new();

    /// <summary>
    /// Returns a random enum value of the specified type.
    /// </summary>
    public static TEnum? RandomEnumValue<TEnum>()
    {
        var v = Enum.GetValues(typeof(TEnum));
        return (TEnum?)v.GetValue(_R.Next(v.Length));
    }

    /// <summary>
    /// InMemory provider does not understand RowVersion like SQL EF Provider.
    /// Use this to set a RowVersion on entities for InMemory testing.
    /// </summary>
    public static byte[] GetRandomByteArray(int sizeInBytes)
    {
        byte[] b = new byte[sizeInBytes];
        Random.Shared.NextBytes(b);
        return b;
    }
}
