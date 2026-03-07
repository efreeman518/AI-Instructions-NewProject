````markdown
# Agent Template

Use this only when search alone is not enough and a single model needs to choose among a few bounded application-service tools. Default to one `ChatClientAgent`. Add middleware, agent-to-agent composition, or Foundry Agent Service only after the simple path is proven insufficient.

## Default Shape

- One interface
- One service
- A small tool set that delegates to existing application services
- Prompt files in `Prompts/`
- One DI registration
- One API endpoint only if the slice exposes chat directly

## Agent Interface

```csharp
namespace {Org}.{Project}.Infrastructure.AI.Agents;

using Microsoft.Agents.AI;

public interface I{Agent}Agent
{
    Task<AgentResponse> RunAsync(string userMessage, AgentSession? session = null, CancellationToken ct = default);

    Task<AgentSession> CreateSessionAsync(CancellationToken ct = default);
}
```

## Default Agent Service

```csharp
namespace {Org}.{Project}.Infrastructure.AI.Agents;

using System.ComponentModel;
using Azure.AI.OpenAI;
using Microsoft.Agents.AI;
using Microsoft.Extensions.AI;
using Microsoft.Extensions.Options;

internal sealed class {Agent}AgentService : I{Agent}Agent
{
    private readonly AIAgent _agent;

    public {Agent}AgentService(
        AzureOpenAIClient openAiClient,
        IOptions<AiSettings> settings,
        I{Entity}Service entityService)
    {
        var systemPrompt = EmbeddedResource.Read("Prompts.{Agent}.system-prompt.txt");

        _agent = openAiClient
            .GetChatClient(settings.Value.AgentModelDeployment)
            .AsAIAgent(
                instructions: systemPrompt,
                name: "{Agent}",
                tools:
                [
                    AIFunctionFactory.Create(
                        async ([Description("The {entity} ID (GUID)")] string id, CancellationToken ct) =>
                            await entityService.GetAsync(Guid.Parse(id), ct),
                        "Get{Entity}",
                        "Get a {entity} by ID")
                ]);
    }

    public async Task<AgentResponse> RunAsync(
        string userMessage, AgentSession? session = null, CancellationToken ct = default)
    {
        session ??= await _agent.CreateSessionAsync();
        return await _agent.RunAsync(userMessage, session, cancellationToken: ct);
    }

    public async Task<AgentSession> CreateSessionAsync(CancellationToken ct = default)
    {
        return await _agent.CreateSessionAsync();
    }
}
```

## Add Search Only If Needed

If the agent must ground answers in indexed data, add one search tool that delegates to the search service.

```csharp
AIFunctionFactory.Create(
    async ([Description("The search query")] string query, CancellationToken ct) =>
        await searchService.SearchAsync(query, SearchMode.Semantic, ct),
    "Search{Entity}s",
    "Search for {entity}s by natural language query")
```

Keep the first pass narrow. Do not register tools that bypass application services or duplicate domain logic.

## Optional Tool Helper

```csharp
namespace {Org}.{Project}.Infrastructure.AI.Agents.Tools;

using System.ComponentModel;

internal static class {Agent}Tools
{
    [Description("Search for {entity}s matching a natural language query")]
    public static async Task<object> Search{Entity}s(
        [Description("The search query")] string query,
        I{Project}SearchService searchService,
        CancellationToken ct)
    {
        var results = await searchService.SearchAsync(query, SearchMode.Semantic, ct);
        return results.Select(r => new { r.Id, r.{Property1}, r.Score });
    }

    [Description("Get details of a specific {entity}")]
    public static async Task<object?> Get{Entity}(
        [Description("The {entity} ID (GUID)")] string id,
        I{Entity}Service entityService,
        CancellationToken ct)
    {
        return await entityService.GetAsync(Guid.Parse(id), ct);
    }
}
```

## Prompt File

Create `Prompts/{Agent}.system-prompt.txt`:

```text
You are {Agent}, an AI assistant for {Project}.

## Role
{Describe the bounded business task this agent owns}

## Rules
- Use tools only when they materially improve the answer
- Cite IDs or other traceable references from tool results
- Respect tenant and authorization boundaries
- If available tools do not support the request, say so clearly
```

## Escalate Only If Needed

- Middleware: add only for a concrete cross-cutting need such as auth propagation, redaction, or audit logging.
- Agent-as-tool composition: add only when another agent owns a distinct bounded capability that should stay isolated.
- Foundry Agent Service: add only when hosted memory, hosted tools, or centralized operations are real requirements.

## DI Registration

```csharp
services.AddScoped<I{Agent}Agent, {Agent}AgentService>();
```

## API Endpoint

```csharp
group.MapPost("/agent/{agent-route}/chat", async (
    [FromBody] AgentChatRequest request,
    I{Agent}Agent agent,
    CancellationToken ct) =>
{
    var session = await agent.CreateSessionAsync(ct);
    var response = await agent.RunAsync(request.Message, session, ct);

    return TypedResults.Ok(new AgentChatResponse
    {
        Messages = response.Messages.Select(m => m.Text).ToList(),
        SessionId = session.ToString()
    });
})
.WithName("Chat{Agent}")
.WithSummary("Send a message to the {Agent} agent");
```
````
