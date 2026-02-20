// ═══════════════════════════════════════════════════════════════
// Pattern: MockHttpMessageHandler — returns contrived JSON responses for
// offline/mock-mode HTTP testing without a running Gateway.
//
// Registered in App.xaml.host.cs via:
//   services.AddTransient<MockHttpMessageHandler>();
//   clientBuilder.ConfigurePrimaryAndInnerHttpMessageHandler<MockHttpMessageHandler>();
//
// Activated when Features:UseMocks = true in appsettings.
// Routes are matched by URL path to return appropriate mock data.
// ═══════════════════════════════════════════════════════════════

using System.Net;
using System.Text;
using System.Text.Json;

namespace TaskFlow.UI.Infrastructure;

/// <summary>
/// Pattern: DelegatingHandler for offline/mock mode — intercepts all HTTP requests
/// from the Kiota client and returns contrived JSON.
/// Useful for UI development without a running backend.
/// </summary>
public class MockHttpMessageHandler : DelegatingHandler
{
    // Pattern: Contrived tenant ID — matches the mock data in TodoItemService.
    private static readonly string TenantId = "00000000-0000-0000-0000-000000000099";

    protected override Task<HttpResponseMessage> SendAsync(
        HttpRequestMessage request, CancellationToken cancellationToken)
    {
        var path = request.RequestUri?.AbsolutePath?.ToLowerInvariant() ?? string.Empty;

        // Pattern: Route matching — returns appropriate mock JSON per endpoint.
        var response = path switch
        {
            _ when path.Contains("/api/todoitems") && request.Method == HttpMethod.Get
                => CreateJsonResponse(GetMockTodoItems()),

            _ when path.Contains("/api/todoitems/") && request.Method == HttpMethod.Get
                => CreateJsonResponse(GetMockTodoItemById(path)),

            _ when path.Contains("/api/todoitems") && request.Method == HttpMethod.Post
                => CreateJsonResponse(new { Id = Guid.NewGuid() }, HttpStatusCode.Created),

            _ when path.Contains("/api/todoitems/") && request.Method == HttpMethod.Put
                => new HttpResponseMessage(HttpStatusCode.NoContent),

            _ when path.Contains("/api/todoitems/") && request.Method == HttpMethod.Delete
                => new HttpResponseMessage(HttpStatusCode.NoContent),

            _ when path.Contains("/api/categories") && request.Method == HttpMethod.Get
                => CreateJsonResponse(GetMockCategories()),

            _ when path.Contains("/api/categories/") && request.Method == HttpMethod.Get
                => CreateJsonResponse(new { Id = Guid.NewGuid(), Name = "Development", IsActive = true }),

            // Pattern: Fallback — 404 for unmatched routes.
            _ => new HttpResponseMessage(HttpStatusCode.NotFound)
            {
                Content = new StringContent(
                    JsonSerializer.Serialize(new { Error = $"Mock route not found: {path}" }),
                    Encoding.UTF8, "application/json")
            }
        };

        return Task.FromResult(response);
    }

    // ── Mock Data Generators ─────────────────────────────────────

    private static object[] GetMockTodoItems() =>
    [
        new { Id = "00000000-0000-0000-0000-000000000001", TenantId,
              Title = "Review pull request", Description = "Check the latest PR for TaskFlow",
              Priority = 3, IsCompleted = false, CategoryName = "Development",
              DueDate = (DateTimeOffset?)null },
        new { Id = "00000000-0000-0000-0000-000000000002", TenantId,
              Title = "Write unit tests", Description = "Cover all domain entity patterns",
              Priority = 2, IsCompleted = false, CategoryName = "Testing",
              DueDate = (DateTimeOffset?)DateTimeOffset.UtcNow.AddDays(3) },
        new { Id = "00000000-0000-0000-0000-000000000003", TenantId,
              Title = "Deploy to staging", Description = "Push latest build to staging environment",
              Priority = 4, IsCompleted = false, CategoryName = "DevOps",
              DueDate = (DateTimeOffset?)DateTimeOffset.UtcNow.AddDays(-1) },
    ];

    private static object GetMockTodoItemById(string path)
    {
        // Pattern: Extract ID from path segment — last segment after /api/todoitems/
        var segments = path.Split('/', StringSplitOptions.RemoveEmptyEntries);
        var id = segments.Length > 2 ? segments[^1] : "00000000-0000-0000-0000-000000000001";

        return new
        {
            Id = id, TenantId,
            Title = "Review pull request",
            Description = "Check the latest PR for TaskFlow",
            Priority = 3, IsCompleted = false, CategoryName = "Development",
            DueDate = (DateTimeOffset?)null
        };
    }

    private static object[] GetMockCategories() =>
    [
        new { Id = "10000000-0000-0000-0000-000000000001", Name = "Development", IsActive = true },
        new { Id = "10000000-0000-0000-0000-000000000002", Name = "Testing", IsActive = true },
        new { Id = "10000000-0000-0000-0000-000000000003", Name = "DevOps", IsActive = true },
        new { Id = "10000000-0000-0000-0000-000000000004", Name = "Documentation", IsActive = false },
    ];

    // ── Response Builder ─────────────────────────────────────────

    /// <summary>
    /// Pattern: Build an HttpResponseMessage with serialized JSON content.
    /// Uses System.Text.Json with camelCase for Kiota compatibility.
    /// </summary>
    private static HttpResponseMessage CreateJsonResponse(
        object data, HttpStatusCode statusCode = HttpStatusCode.OK)
    {
        var json = JsonSerializer.Serialize(data, new JsonSerializerOptions
        {
            PropertyNamingPolicy = JsonNamingPolicy.CamelCase,
            WriteIndented = false
        });

        return new HttpResponseMessage(statusCode)
        {
            Content = new StringContent(json, Encoding.UTF8, "application/json")
        };
    }
}
