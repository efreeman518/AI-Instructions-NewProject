````markdown
# AI Integration

Semantic search (Azure AI Search) and AI agents (Microsoft Agent Framework) backed by Microsoft Foundry models.

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

---

## Technology Stack

### Microsoft Foundry (Azure)

The unified Azure PaaS for enterprise AI. Provides:
- **Model deployments** — deploy and manage OpenAI, Anthropic, and other models
- **Foundry Agent Service** — hosted agent backend with built-in memory, knowledge (Foundry IQ), and tool catalogs
- **Observability** — tracing, monitoring, evaluations
- **Enterprise controls** — RBAC, networking, policies under one resource provider

Portal: [ai.azure.com](https://ai.azure.com). SDK: `Azure.AI.OpenAI`, `Azure.AI.Agents.Persistent`.

### Microsoft Agent Framework (Code)

The next-generation agent SDK, created by the Semantic Kernel + AutoGen teams. Key primitives:

| Concept | Package / Type | Description |
|---|---|---|
| `AIAgent` | `Microsoft.Agents.AI` | Base abstraction for all agents |
| `ChatClientAgent` | `Microsoft.Agents.AI` | Agent wrapping any `IChatClient` |
| Function tools | `AIFunctionFactory.Create()` | Turn any C# method into an agent tool |
| Agent-as-tool | `agent.AsAIFunction()` | Compose agents — one agent calls another |
| Sessions | `AgentSession` | Multi-turn conversation state container |
| Middleware | `agent.AsBuilder().Use(...)` | Intercept runs, function calls, chat client requests |
| Workflows | `Microsoft.Agents.Workflows` | Graph-based executors + edges for multi-agent orchestration |
| MCP tools | Hosted (Foundry) + Local | Connect to 1,400+ tools via MCP protocol |

Docs: [learn.microsoft.com/agent-framework](https://learn.microsoft.com/en-us/agent-framework/overview/).

---

## Packages

```xml
<!-- Agent Framework core -->
<PackageReference Include="Microsoft.Agents.AI.OpenAI" />

<!-- Azure OpenAI client (also used for Foundry Models) -->
<PackageReference Include="Azure.AI.OpenAI" />

<!-- Azure AI Search -->
<PackageReference Include="Azure.Search.Documents" />

<!-- Azure Identity -->
<PackageReference Include="Azure.Identity" />

<!-- Foundry Agent Service (only if useFoundryAgentService: true) -->
<PackageReference Include="Azure.AI.Agents.Persistent" />

<!-- Agent Framework workflows (only if workflow.enabled: true) -->
<PackageReference Include="Microsoft.Agents.Workflows" />
```

Version all packages in `Directory.Packages.props`.

---

## Project Structure

```
src/Infrastructure/{Project}.Infrastructure.AI/
├── {Project}.Infrastructure.AI.csproj
├── Search/
│   ├── I{Project}SearchService.cs              # Search abstraction
│   ├── {Project}SearchService.cs               # Azure AI Search implementation
│   ├── {Entity}SearchIndexDefinition.cs        # Index field mappings
│   └── {Entity}VectorizationHandler.cs         # Embedding pipeline (event-driven or batch)
├── Agents/
│   ├── I{Agent}Agent.cs                        # Agent abstraction
│   ├── {Agent}AgentService.cs                  # Agent setup + RunAsync wrapper
│   ├── Tools/
│   │   └── {Tool}Tool.cs                       # Function tools (wrap domain services)
│   ├── Middleware/
│   │   └── {Project}AgentMiddleware.cs          # Logging, auth, content safety
│   └── Prompts/
│       └── {Agent}.system-prompt.txt           # System prompts (file-based, not hardcoded)
├── Workflows/                                   # Only if workflow.enabled: true
│   ├── {Workflow}WorkflowDefinition.cs          # Executor + edge graph definition
│   └── {Workflow}Executor.cs                    # Individual workflow step
├── Models/
│   ├── SearchResult.cs
│   ├── SearchRequest.cs
│   └── AgentResponse.cs
├── {Project}AiSettings.cs
└── ServiceCollectionExtensions.cs
```

---

## Agent Patterns

### Simple Agent (ChatClientAgent)

Most common pattern. Wraps an Azure OpenAI / Foundry model with function tools that delegate to your existing application services.

```csharp
public class SupportTriageAgentService : ISupportTriageAgent
{
    private readonly AIAgent _agent;
    private readonly ILogger<SupportTriageAgentService> _logger;

    public SupportTriageAgentService(
        AzureOpenAIClient openAiClient,
        IOptions<AiSettings> settings,
        ITicketService ticketService,
        ISearchService searchService,
        ILogger<SupportTriageAgentService> logger)
    {
        _logger = logger;

        // Load system prompt from embedded file
        var systemPrompt = EmbeddedResource.Read("Prompts.SupportTriageAgent.system-prompt.txt");

        // Create agent with function tools that wrap domain services
        _agent = openAiClient
            .GetChatClient(settings.Value.AgentModelDeployment)
            .AsAIAgent(
                instructions: systemPrompt,
                name: "SupportTriageAgent",
                tools:
                [
                    AIFunctionFactory.Create(
                        ([Description("Search knowledge base")] string query) =>
                            searchService.SearchAsync(query, CancellationToken.None),
                        "SearchKnowledgeBase",
                        "Search the knowledge base for relevant articles"),

                    AIFunctionFactory.Create(
                        ([Description("Ticket ID")] string ticketId) =>
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

### Agent with Middleware

```csharp
var agentWithMiddleware = baseAgent
    .AsBuilder()
    .Use(
        runFunc: async (messages, session, options, innerAgent, ct) =>
        {
            logger.LogInformation("Agent run started with {MessageCount} messages", messages.Count());
            var response = await innerAgent.RunAsync(messages, session, options, ct);
            logger.LogInformation("Agent run completed with {ResponseCount} response messages", response.Messages.Count);
            return response;
        })
    .Use(async (agent, context, next, ct) =>
    {
        logger.LogInformation("Function call: {FunctionName}", context.Function.Name);
        var result = await next(context, ct);
        logger.LogInformation("Function result: {Result}", result?.ToString()?[..100]);
        return result;
    })
    .Build();
```

### Agent-as-Tool (Composition)

```csharp
// Inner agent becomes a tool for the outer agent
AIAgent classifierAgent = openAiClient
    .GetChatClient(deploymentName)
    .AsAIAgent(
        instructions: "Classify support tickets by urgency: Low, Medium, High, Critical.",
        name: "ClassifierAgent",
        description: "Classifies support ticket urgency.");

AIAgent triageAgent = openAiClient
    .GetChatClient(deploymentName)
    .AsAIAgent(
        instructions: "You triage support tickets using available tools.",
        tools: [classifierAgent.AsAIFunction(), searchTool]);
```

### Foundry Agent Service (Hosted)

Use when you want server-side memory, Foundry IQ knowledge, and the hosted tool catalog.

```csharp
var persistentAgentsClient = new PersistentAgentsClient(
    new Uri(settings.FoundryAgentServiceEndpoint),
    new DefaultAzureCredential());

AIAgent hostedAgent = await persistentAgentsClient.CreateAIAgentAsync(
    model: settings.AgentModelDeployment,
    instructions: systemPrompt,
    name: "HostedSupportAgent");
```

### Multi-Agent Workflow

Use Agent Framework Workflows for explicit multi-step orchestration with type-safe routing and checkpointing.

```csharp
// Workflows use executors (processing units) and edges (connections)
// Each executor receives input, performs work, produces output
// Edges define the flow graph — sequential, parallel, conditional
// See: https://learn.microsoft.com/en-us/agent-framework/workflows/
```

Workflow scaffolding produces:
- Executor classes per step (wrapping agents or domain functions)
- Edge definitions with optional conditions
- Workflow builder + runner registration
- Checkpoint support for long-running processes

---

## Search Patterns

### Azure AI Search Client

```csharp
public class ProjectSearchService : IProjectSearchService
{
    private readonly SearchClient _searchClient;
    private readonly AzureOpenAIClient _openAiClient;
    private readonly AiSettings _settings;

    public async Task<IReadOnlyList<SearchResult<T>>> SearchAsync<T>(
        string query, SearchMode mode, CancellationToken ct) where T : class
    {
        SearchOptions options = mode switch
        {
            SearchMode.Keyword => new() { QueryType = SearchQueryType.Simple },
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

        var response = await _searchClient.SearchAsync<T>(query, options, ct);
        return [.. response.Value.GetResults()];
    }
}
```

### Vectorization Pipeline

#### On-Write (Domain Event Handler)

```csharp
public class ProductVectorizationHandler : IMessageHandler<ProductCreatedEvent>
{
    private readonly SearchClient _searchClient;
    private readonly EmbeddingClient _embeddingClient;

    public async Task HandleAsync(ProductCreatedEvent evt, CancellationToken ct)
    {
        var embedding = await _embeddingClient.GenerateEmbeddingAsync(
            $"{evt.Name} {evt.Description}", cancellationToken: ct);

        var doc = new SearchDocument
        {
            ["id"] = evt.ProductId.ToString(),
            ["Name"] = evt.Name,
            ["Description"] = evt.Description,
            ["DescriptionVector"] = embedding.Value.ToFloats()
        };

        await _searchClient.MergeOrUploadDocumentsAsync([doc], cancellationToken: ct);
    }
}
```

#### Batch (Function App / Scheduler)

Use when vectorizing large existing datasets or when eventual consistency is acceptable. Wire as a timer-triggered function or scheduled job.

---

## DI Registration

```csharp
public static class AiServiceCollectionExtensions
{
    public static IServiceCollection AddAiServices(this IServiceCollection services, IConfiguration config)
    {
        var aiSection = config.GetSection(AiSettings.ConfigSectionName);
        services.Configure<AiSettings>(aiSection);
        var settings = aiSection.Get<AiSettings>()!;

        // Azure OpenAI / Foundry Models client
        services.AddSingleton(new AzureOpenAIClient(
            new Uri(settings.FoundryEndpoint),
            new DefaultAzureCredential()));

        // Azure AI Search (if configured)
        if (!string.IsNullOrEmpty(settings.SearchEndpoint))
        {
            services.AddSingleton(new SearchClient(
                new Uri(settings.SearchEndpoint),
                settings.SearchIndexName,
                new DefaultAzureCredential()));

            services.AddScoped<IProjectSearchService, ProjectSearchService>();
        }

        // Agent services
        services.AddScoped<ISupportTriageAgent, SupportTriageAgentService>();

        return services;
    }
}
```

---

## Configuration (appsettings)

```json
{
  "AiServices": {
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

```csharp
// AppHost — wire AI resources
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

Mock `SearchClient` or use the Azure AI Search emulator for integration tests. Verify index schema matches entity properties.

### Function Tool Tests

Test function tools independently — they are plain C# methods that wrap domain services. Use standard unit test patterns with mocked `I{Entity}Service`.

---

## References

- [Microsoft Foundry](https://learn.microsoft.com/en-us/azure/ai-foundry/what-is-foundry)
- [Agent Framework Overview](https://learn.microsoft.com/en-us/agent-framework/overview/)
- [Agent Framework — Agents](https://learn.microsoft.com/en-us/agent-framework/agents/)
- [Agent Framework — Tools](https://learn.microsoft.com/en-us/agent-framework/agents/tools/)
- [Agent Framework — Workflows](https://learn.microsoft.com/en-us/agent-framework/workflows/)
- [Agent Framework — Sessions](https://learn.microsoft.com/en-us/agent-framework/agents/conversations/session)
- [Agent Framework — Middleware](https://learn.microsoft.com/en-us/agent-framework/agents/middleware/)
- [Azure AI Search — .NET SDK](https://learn.microsoft.com/en-us/azure/search/search-howto-dotnet-sdk)
- [Migration from Semantic Kernel](https://learn.microsoft.com/en-us/agent-framework/migration-guide/from-semantic-kernel/)
- [Migration from AutoGen](https://learn.microsoft.com/en-us/agent-framework/migration-guide/from-autogen/)
````
