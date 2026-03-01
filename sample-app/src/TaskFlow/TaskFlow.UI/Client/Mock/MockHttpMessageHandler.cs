using System.Net;
using System.Text;
using System.Text.Json;
using Domain.Shared;

namespace TaskFlow.UI.Client.Mock;

/// <summary>
/// Mock HTTP message handler — replaces real Gateway calls with in-memory mock data.
/// Activated via USE_MOCKS preprocessor define.
/// Follows the Chefs app pattern: intercepts requests by path, returns JSON responses.
/// </summary>
public partial class MockHttpMessageHandler : HttpMessageHandler
{
    private static readonly JsonSerializerOptions _jsonOptions = new()
    {
        PropertyNamingPolicy = JsonNamingPolicy.CamelCase,
        WriteIndented = false,
    };

    private static readonly List<MockTodoItem> _todoItems =
    [
        new() { Id = Guid.Parse("11111111-1111-1111-1111-111111111111"), Title = "Set up development environment", Description = "Install .NET 10 SDK, VS Code, and required extensions", Priority = 1, Status = TodoItemStatus.IsCompleted, CategoryId = Guid.Parse("aaaaaaaa-aaaa-aaaa-aaaa-aaaaaaaaaaaa"), CategoryName = "Development", CommentCount = 2, TagCount = 1 },
        new() { Id = Guid.Parse("22222222-2222-2222-2222-222222222222"), Title = "Design database schema", Description = "Create entity-relationship diagram and define table structures", Priority = 2, Status = TodoItemStatus.IsStarted, CategoryId = Guid.Parse("aaaaaaaa-aaaa-aaaa-aaaa-aaaaaaaaaaaa"), CategoryName = "Development", DueDate = DateTimeOffset.UtcNow.AddDays(7), TagCount = 2 },
        new() { Id = Guid.Parse("33333333-3333-3333-3333-333333333333"), Title = "Write unit tests for domain model", Description = "Cover all entity creation, state transitions, and validation rules", Priority = 2, Status = TodoItemStatus.None, CategoryId = Guid.Parse("bbbbbbbb-bbbb-bbbb-bbbb-bbbbbbbbbbbb"), CategoryName = "Testing", DueDate = DateTimeOffset.UtcNow.AddDays(14) },
        new() { Id = Guid.Parse("44444444-4444-4444-4444-444444444444"), Title = "Review PR #42 — API endpoints", Description = "Code review for the new todo item endpoints", Priority = 3, Status = TodoItemStatus.IsBlocked, CategoryName = "Review", CommentCount = 5 },
        new() { Id = Guid.Parse("55555555-5555-5555-5555-555555555555"), Title = "Update project documentation", Description = "Update README and API docs with latest changes", Priority = 4, Status = TodoItemStatus.None, DueDate = DateTimeOffset.UtcNow.AddDays(30) },
    ];

    private static readonly List<MockCategory> _categories =
    [
        new() { Id = Guid.Parse("aaaaaaaa-aaaa-aaaa-aaaa-aaaaaaaaaaaa"), Name = "Development", Description = "Software development tasks", ColorHex = "#4CAF50", DisplayOrder = 1, IsActive = true },
        new() { Id = Guid.Parse("bbbbbbbb-bbbb-bbbb-bbbb-bbbbbbbbbbbb"), Name = "Testing", Description = "QA and testing tasks", ColorHex = "#2196F3", DisplayOrder = 2, IsActive = true },
        new() { Id = Guid.Parse("cccccccc-cccc-cccc-cccc-cccccccccccc"), Name = "Design", Description = "UI/UX design tasks", ColorHex = "#FF9800", DisplayOrder = 3, IsActive = true },
        new() { Id = Guid.Parse("dddddddd-dddd-dddd-dddd-dddddddddddd"), Name = "DevOps", Description = "CI/CD and infrastructure tasks", ColorHex = "#9C27B0", DisplayOrder = 4, IsActive = true },
    ];

    private static readonly List<MockTag> _tags =
    [
        new() { Id = Guid.Parse("11110000-0000-0000-0000-000000000001"), Name = "urgent", Description = "Requires immediate attention" },
        new() { Id = Guid.Parse("11110000-0000-0000-0000-000000000002"), Name = "backend", Description = "Backend work" },
        new() { Id = Guid.Parse("11110000-0000-0000-0000-000000000003"), Name = "frontend", Description = "Frontend work" },
        new() { Id = Guid.Parse("11110000-0000-0000-0000-000000000004"), Name = "documentation" },
    ];

    private static readonly List<MockTeam> _teams =
    [
        new() { Id = Guid.Parse("eeeeeeee-eeee-eeee-eeee-eeeeeeeeeeee"), Name = "Platform Team", Description = "Core platform development", IsActive = true, Members =
        [
            new() { Id = Guid.NewGuid(), UserId = Guid.NewGuid(), DisplayName = "Alice Johnson", Role = TeamMemberRole.Owner, JoinedAt = DateTimeOffset.UtcNow.AddMonths(-6) },
            new() { Id = Guid.NewGuid(), UserId = Guid.NewGuid(), DisplayName = "Bob Smith", Role = TeamMemberRole.Admin, JoinedAt = DateTimeOffset.UtcNow.AddMonths(-3) },
        ]},
        new() { Id = Guid.Parse("ffffffff-ffff-ffff-ffff-ffffffffffff"), Name = "QA Team", Description = "Quality assurance", IsActive = true, Members =
        [
            new() { Id = Guid.NewGuid(), UserId = Guid.NewGuid(), DisplayName = "Carol Davis", Role = TeamMemberRole.Owner, JoinedAt = DateTimeOffset.UtcNow.AddMonths(-12) },
        ]},
    ];

    protected override async Task<HttpResponseMessage> SendAsync(HttpRequestMessage request, CancellationToken cancellationToken)
    {
        var path = request.RequestUri?.AbsolutePath ?? string.Empty;
        var method = request.Method;

        object? responseData = null;
        var statusCode = HttpStatusCode.OK;

        if (method == HttpMethod.Get)
        {
            responseData = path switch
            {
                var p when p.Contains("/api/todoitems") && !ContainsGuid(p, "/api/todoitems/") => _todoItems,
                var p when ContainsGuid(p, "/api/todoitems/") => _todoItems.FirstOrDefault(x => p.Contains(x.Id.ToString())),
                var p when p.Contains("/api/categories") && !ContainsGuid(p, "/api/categories/") => _categories,
                var p when ContainsGuid(p, "/api/categories/") => _categories.FirstOrDefault(x => p.Contains(x.Id.ToString())),
                var p when p.Contains("/api/tags") && !ContainsGuid(p, "/api/tags/") => _tags,
                var p when ContainsGuid(p, "/api/tags/") => _tags.FirstOrDefault(x => p.Contains(x.Id.ToString())),
                var p when p.Contains("/api/teams") && !ContainsGuid(p, "/api/teams/") => _teams,
                var p when ContainsGuid(p, "/api/teams/") => _teams.FirstOrDefault(x => p.Contains(x.Id.ToString())),
                _ => null,
            };
        }
        else if (method == HttpMethod.Post || method == HttpMethod.Put)
        {
            // Echo back the request body so the service receives the created/updated entity.
            var body = request.Content is not null
                ? await request.Content.ReadAsStringAsync(cancellationToken)
                : null;

            if (body is not null)
            {
                responseData = JsonSerializer.Deserialize<JsonElement>(body);
                statusCode = method == HttpMethod.Post ? HttpStatusCode.Created : HttpStatusCode.OK;
            }
            else
            {
                responseData = new { success = true };
            }
        }
        else if (method == HttpMethod.Delete)
        {
            statusCode = HttpStatusCode.NoContent;
        }
        var json = responseData is not null ? JsonSerializer.Serialize(responseData, _jsonOptions) : "{}";

        var response = new HttpResponseMessage(statusCode)
        {
            Content = new StringContent(json, Encoding.UTF8, "application/json"),
        };

        return response;
    }

    private static bool ContainsGuid(string path, string prefix)
    {
        var idx = path.IndexOf(prefix, StringComparison.OrdinalIgnoreCase);
        return idx >= 0 && path.Length > idx + prefix.Length;
    }

    // Internal mock DTOs
    private sealed class MockTodoItem
    {
        public Guid Id { get; set; }
        public string Title { get; set; } = string.Empty;
        public string? Description { get; set; }
        public int Priority { get; set; } = 3;
        public TodoItemStatus Status { get; set; }
        public decimal? EstimatedHours { get; set; }
        public decimal? ActualHours { get; set; }
        public DateTimeOffset? StartDate { get; set; }
        public DateTimeOffset? DueDate { get; set; }
        public Guid? CategoryId { get; set; }
        public Guid? AssignedToId { get; set; }
        public Guid? TeamId { get; set; }
        public string? CategoryName { get; set; }
        public string? AssignedToName { get; set; }
        public string? TeamName { get; set; }
        public int CommentCount { get; set; }
        public int TagCount { get; set; }
    }

    private sealed class MockCategory
    {
        public Guid Id { get; set; }
        public string Name { get; set; } = string.Empty;
        public string? Description { get; set; }
        public string? ColorHex { get; set; }
        public int DisplayOrder { get; set; }
        public bool IsActive { get; set; }
    }

    private sealed class MockTag
    {
        public Guid Id { get; set; }
        public string Name { get; set; } = string.Empty;
        public string? Description { get; set; }
    }

    private sealed class MockTeam
    {
        public Guid Id { get; set; }
        public string Name { get; set; } = string.Empty;
        public string? Description { get; set; }
        public bool IsActive { get; set; }
        public List<MockTeamMember>? Members { get; set; }
    }

    private sealed class MockTeamMember
    {
        public Guid Id { get; set; }
        public Guid UserId { get; set; }
        public string DisplayName { get; set; } = string.Empty;
        public TeamMemberRole Role { get; set; }
        public DateTimeOffset JoinedAt { get; set; }
    }
}
