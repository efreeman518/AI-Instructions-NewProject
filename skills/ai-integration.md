````markdown
# AI Integration

Use this only when the current slice actually needs semantic retrieval, grounded Q&A, or bounded tool-driven automation. Default to search first, agent second, workflows or hosted agents last.

## Prerequisites

- [solution-structure.md](solution-structure.md)
- [bootstrapper.md](bootstrapper.md)
- [configuration.md](configuration.md)
- [identity-management.md](identity-management.md) (agents need auth context)
- [package-dependencies.md](package-dependencies.md)

## Non-Negotiables

1. All AI services behind interfaces — testable, swappable.
2. Embedding generation is infrastructure, not domain.
3. Agent function tools delegate to existing `I{Entity}Service` application services — no domain logic in tools.
4. Search indexes are projections, not source of truth.
5. Use `DefaultAzureCredential` for Foundry/Search auth (no API keys in code). In production, prefer `ManagedIdentityCredential`.
6. Configuration-driven model selection (appsettings, not hardcoded deployment names).
7. Use **Microsoft Agent Framework** (`Microsoft.Agents.AI`) — the successor to Semantic Kernel and AutoGen. Do not scaffold with Semantic Kernel or AutoGen packages.
8. Agent sessions (`AgentSession`) must be scoped per user/conversation — never share sessions across tenants.
9. Start with one agent and a small tool set. Do not scaffold multi-agent orchestration until a single-agent path is proven insufficient.
10. System prompts live in files, not inline string literals spread through services.

---

## Pragmatic Defaults

1. **Search-only first** when the requirement is findability, retrieval, or grounded Q&A over existing data.
2. **Single agent second** when the model must choose among a few application-service tools.
3. **Workflows last** when the process is durable, branching, resumable, or needs explicit approvals/checkpoints.
4. **Foundry Agent Service only when justified** by hosted memory, centralized tool catalogs, or operational requirements that a code-hosted agent cannot meet.
5. **Keyword or semantic search before vector or hybrid**. Add embeddings only when search quality testing shows a clear gap.
6. **Do not scaffold empty AI folders**. Add only the Search, Agents, and Workflows folders that are enabled.

## Decision Order

- **Need retrieval over business data?** Start with Azure AI Search.
- **Need the model to call internal business operations?** Add one `ChatClientAgent` with a few function tools that delegate to existing application services.
- **Need long-running or branching AI processes?** Add `Microsoft.Agents.Workflows`.
- **Need hosted memory or Foundry-managed tools?** Add Foundry Agent Service after the simpler code-hosted path is proven insufficient.

## Technology Choices

- **Foundry Models / Azure OpenAI client:** default model host for completions, embeddings, and tool-calling.
- **Azure AI Search:** default retrieval tier.
- **Microsoft Agent Framework:** default code-side agent SDK.
- **Foundry Agent Service:** optional hosted agent backend.
- **Agent Framework Workflows:** optional explicit orchestration layer.

Useful primitives:
- `ChatClientAgent` for the default single-agent path
- `AIFunctionFactory.Create()` for application-service tools
- `AgentSession` for per-conversation state
- `Microsoft.Agents.Workflows` for explicit orchestration only when needed

---

## Packages

- Baseline for any AI capability:
    - `Azure.AI.OpenAI`
    - `Azure.Identity`
- Add only when enabled:
    - `Azure.Search.Documents` for search
    - `Microsoft.Agents.AI.OpenAI` for agents
    - `Azure.AI.Agents.Persistent` for Foundry Agent Service
    - `Microsoft.Agents.Workflows` for workflow orchestration

Version all packages in `Directory.Packages.props`.

---

## Project Structure

Generate only the folders used by the enabled feature set.

```
src/Infrastructure/{Project}.Infrastructure.AI/
├── {Project}.Infrastructure.AI.csproj
├── Search/                                   # Only if useSearch: true
│   ├── I{Project}SearchService.cs
│   ├── {Project}SearchService.cs
│   ├── {Entity}SearchIndexDefinition.cs
│   └── {Entity}VectorizationHandler.cs
├── Agents/                                   # Only if useAgents: true
│   ├── I{Agent}Agent.cs
│   ├── {Agent}AgentService.cs
│   ├── Tools/
│   │   └── {Tool}Tool.cs
│   ├── Middleware/
│   └── Prompts/
├── Workflows/                                 # Only if workflow.enabled: true
├── {Project}AiSettings.cs
└── ServiceCollectionExtensions.cs
```

---

## Agent Patterns

### Simple Agent (ChatClientAgent)

This is the default agent pattern. Wrap an Azure OpenAI / Foundry model with a small number of function tools that delegate to existing application services.

```csharp
public sealed class SupportTriageAgentService : ISupportTriageAgent
{
    private readonly AIAgent _agent;

    public SupportTriageAgentService(
        AzureOpenAIClient openAiClient,
        IOptions<AiSettings> settings,
        ITicketService ticketService)
    {
        var systemPrompt = EmbeddedResource.Read("Prompts.SupportTriageAgent.system-prompt.txt");

        _agent = openAiClient
            .GetChatClient(settings.Value.AgentModelDeployment)
            .AsAIAgent(
                instructions: systemPrompt,
                name: "SupportTriageAgent",
                tools:
                [
                    AIFunctionFactory.Create(
                        (string ticketId) =>
                            ticketService.GetTicketHistoryAsync(ticketId, CancellationToken.None),
                        "GetTicketHistory",
                        "Get the history of a support ticket")
                ]);
    }

    public async Task<AgentResponse> TriageAsync(string userMessage, AgentSession? session = null, CancellationToken ct = default)
    {
        session ??= await _agent.CreateSessionAsync();
        return await _agent.RunAsync(userMessage, session, cancellationToken: ct);
    }
}
```

### Escalate Only When Needed

- **Middleware:** add only after the core run path works and there is a concrete need for logging, redaction, authorization, or safety interception.
- **Agent-as-tool composition:** add only when one agent owns a distinct bounded capability that should stay isolated from the outer agent.
- **Foundry Agent Service:** use only when server-side memory, hosted tools, or centralized management are real requirements.
- **Workflows:** use only for branching, resumable, or human-in-the-loop flows. Do not introduce workflows for a single linear task.

If you add one of these escalations, keep the first pass narrow: one middleware policy, one subordinate agent, or one workflow path.

---

## Search Patterns

### Search Rollout Order

1. Start with keyword or semantic search.
2. Add vector search only if search-quality testing shows that lexical or semantic ranking is inadequate.
3. Add hybrid search only after both lexical and vector behavior are individually understood.

### Azure AI Search Client

```csharp
public class ProjectSearchService : IProjectSearchService
{
    private readonly SearchClient _searchClient;

    public async Task<IReadOnlyList<SearchResult<SearchDocument>>> SearchAsync(
        string query, SearchMode mode, CancellationToken ct)
    {
        SearchOptions options = mode switch
        {
            SearchMode.Keyword => new() { QueryType = SearchQueryType.Simple },
            SearchMode.Semantic => new()
            {
                QueryType = SearchQueryType.Semantic,
                SemanticSearch = new() { SemanticConfigurationName = "default" }
            },
            SearchMode.Vector => new()
            {
                VectorSearch = new()
                {
                    Queries = { new VectorizableTextQuery(query) { KNearestNeighborsCount = 5, Fields = { "DescriptionVector" } } }
                }
            },
            SearchMode.Hybrid => new()
            {
                QueryType = SearchQueryType.Semantic,
                SemanticSearch = new() { SemanticConfigurationName = "default" },
                VectorSearch = new()
                {
                    Queries = { new VectorizableTextQuery(query) { KNearestNeighborsCount = 5, Fields = { "DescriptionVector" } } }
                }
            },
            _ => throw new ArgumentOutOfRangeException(nameof(mode))
        };

        var response = await _searchClient.SearchAsync<SearchDocument>(query, options, ct);
        return [.. response.Value.GetResults()];
    }
}
```

### Vectorization Pipeline

#### On-Write (Domain Event Handler)

- Use an event handler only when search freshness matters enough to justify write-path work.
- Index only projection fields plus the vector field. Always keep the primary entity ID in the document.
- Call a dedicated embedding service abstraction from the handler or job. Do not generate embeddings in domain code.

#### Batch (Function App / Scheduler)

Use when vectorizing large existing datasets or when eventual consistency is acceptable. Prefer batch backfill first when introducing embeddings to an existing system.

---

## DI Registration

```csharp
public static class AiServiceCollectionExtensions
{
    public static IServiceCollection AddAiServices(this IServiceCollection services, IConfiguration config)
    {
        var aiSection = config.GetSection(AiSettings.ConfigSectionName);
        services.AddOptions<AiSettings>()
            .Bind(aiSection)
            .ValidateDataAnnotations()
            .ValidateOnStart();

        // Azure OpenAI / Foundry Models client
        services.AddSingleton(sp =>
        {
            var settings = sp.GetRequiredService<IOptions<AiSettings>>().Value;
            return new AzureOpenAIClient(
                new Uri(settings.FoundryEndpoint),
                new DefaultAzureCredential());
        });

        // Azure AI Search (if configured)
        var settings = aiSection.Get<AiSettings>()!;

        if (settings.UseSearch && !string.IsNullOrWhiteSpace(settings.SearchEndpoint))
        {
            services.AddSingleton(new SearchClient(
                new Uri(settings.SearchEndpoint),
                settings.SearchIndexName,
                new DefaultAzureCredential()));

            services.AddScoped<IProjectSearchService, ProjectSearchService>();
        }

        // Agent services
        if (settings.UseAgents)
        {
            services.AddScoped<ISupportTriageAgent, SupportTriageAgentService>();
        }

        return services;
    }
}
```

---

## Configuration (appsettings)

```json
{
  "AiServices": {
    "UseSearch": true,
    "UseAgents": false,
    "UseVectorSearch": false,
    "UseFoundryAgentService": false,
    "FoundryEndpoint": "https://ai-foundry-{resource}.services.ai.azure.com/",
    "AgentModelDeployment": "gpt-4o-deploy",
    "EmbeddingModelDeployment": "embedding-deploy",
    "SearchEndpoint": "https://{search-resource}.search.windows.net",
    "SearchIndexName": "products-index",
    "FoundryAgentServiceEndpoint": ""
  }
}
```

> **Stub rule:** Generate all AI settings with `// TODO: [CONFIGURE]` comments. Use empty strings for endpoints — never hardcode real URLs.

---

## Aspire Integration

Only wire AI resources through Aspire if the solution already uses an AppHost. Do not introduce Aspire solely for AI.

```csharp
var openai = builder.AddAzureOpenAI("openai")
    .AddDeployment(new("gpt-4o-deploy", "gpt-4o", "2024-08-06", "GlobalStandard", 10))
    .AddDeployment(new("embedding-deploy", "text-embedding-3-small", "1", "GlobalStandard", 10));

var search = builder.AddAzureSearch("search");

var api = builder.AddProject<Projects.MyApp_Api>("api")
    .WithReference(openai)
    .WithReference(search);
```

---

## Testing

Cover the smallest useful surface first:

1. Search service returns expected fields and ordering for the selected search mode.
2. Agent tools call the intended application services and do not bypass business rules.
3. Prompt loading works from file-based system prompts.
4. Disabled AI features do not register or resolve their services.

### Agent Tests

```csharp
// Use a test IChatClient to return deterministic responses
var testAgent = new ChatClientAgent(
    new TestChatClient(fixedResponse: "Classified as: High urgency"),
    instructions: "test");

var response = await testAgent.RunAsync("Test ticket");
Assert.Contains("High urgency", response.ToString());
```

### Search Tests

Mock `SearchClient` or use an integration test against a real test index. Verify index schema and field names match the projected entity shape.

### Function Tool Tests

Test function tools independently — they are plain C# methods that wrap domain services. Use standard unit test patterns with mocked `I{Entity}Service`.

---

## References

- [Microsoft Foundry](https://learn.microsoft.com/en-us/azure/ai-foundry/what-is-foundry)
- [Agent Framework Overview](https://learn.microsoft.com/en-us/agent-framework/overview/)
- [Agent Framework — Workflows](https://learn.microsoft.com/en-us/agent-framework/workflows/)
- [Azure AI Search — .NET SDK](https://learn.microsoft.com/en-us/azure/search/search-howto-dotnet-sdk)
````
