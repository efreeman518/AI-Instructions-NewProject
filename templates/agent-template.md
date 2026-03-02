````markdown
# Agent Template

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

## Agent Service (ChatClientAgent)

```csharp
namespace {Org}.{Project}.Infrastructure.AI.Agents;

using System.ComponentModel;
using Azure.AI.OpenAI;
using Microsoft.Agents.AI;
using Microsoft.Extensions.AI;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;

internal sealed class {Agent}AgentService : I{Agent}Agent
{
    private readonly AIAgent _agent;
    private readonly ILogger<{Agent}AgentService> _logger;

    public {Agent}AgentService(
        AzureOpenAIClient openAiClient,
        IOptions<AiSettings> settings,
        I{Entity}Service entityService,       // inject domain services for function tools
        I{Project}SearchService searchService, // inject search service if agent needs RAG
        ILogger<{Agent}AgentService> logger)
    {
        _logger = logger;

        // Load system prompt from embedded resource or file
        var systemPrompt = LoadSystemPrompt();

        // Build function tools that wrap domain services
        var tools = BuildTools(entityService, searchService);

        // Create agent
        _agent = openAiClient
            .GetChatClient(settings.Value.AgentModelDeployment)
            .AsAIAgent(
                instructions: systemPrompt,
                name: "{Agent}",
                tools: tools);
    }

    public async Task<AgentResponse> RunAsync(
        string userMessage, AgentSession? session = null, CancellationToken ct = default)
    {
        session ??= await _agent.CreateSessionAsync();

        _logger.LogInformation("Agent {AgentName} processing message", "{Agent}");

        var response = await _agent.RunAsync(userMessage, session, cancellationToken: ct);

        _logger.LogInformation("Agent {AgentName} completed with {MessageCount} response messages",
            "{Agent}", response.Messages.Count);

        return response;
    }

    public async Task<AgentSession> CreateSessionAsync(CancellationToken ct = default)
    {
        return await _agent.CreateSessionAsync();
    }

    private static AIFunction[] BuildTools(
        I{Entity}Service entityService,
        I{Project}SearchService searchService)
    {
        return
        [
            // Search tool — delegates to search service
            AIFunctionFactory.Create(
                async ([Description("The search query text")] string query, CancellationToken ct) =>
                    await searchService.SearchAsync(query, SearchMode.Hybrid, ct),
                "Search{Entity}s",
                "Search for {entity}s by natural language query"),

            // Domain operation tool — delegates to application service
            AIFunctionFactory.Create(
                async ([Description("The {entity} ID")] string id, CancellationToken ct) =>
                    await entityService.GetAsync(Guid.Parse(id), ct),
                "Get{Entity}",
                "Get a {entity} by its ID"),

            // Add more tools as needed — each wraps an existing domain service method
            // AIFunctionFactory.Create(
            //     async ([Description("...")] string param, CancellationToken ct) =>
            //         await entityService.SomeOperationAsync(param, ct),
            //     "ToolName",
            //     "Tool description for the LLM"),
        ];
    }

    private static string LoadSystemPrompt()
    {
        // Option 1: Embedded resource
        // var assembly = typeof({Agent}AgentService).Assembly;
        // using var stream = assembly.GetManifestResourceStream("{namespace}.Prompts.{Agent}.system-prompt.txt")
        //     ?? throw new InvalidOperationException("System prompt not found");
        // using var reader = new StreamReader(stream);
        // return reader.ReadToEnd();

        // Option 2: Inline (replace with file-based for production)
        return """
            You are {Agent}, an AI assistant for {Project}.
            
            ## Your Role
            {Describe the agent's purpose and behavior}
            
            ## Guidelines
            - Use available tools to answer questions accurately
            - If you don't have enough information, ask clarifying questions
            - Always cite sources when using search results
            - Respect tenant boundaries — only access data for the current context
            
            ## Available Tools
            - Search{Entity}s: Search the knowledge base
            - Get{Entity}: Retrieve specific {entity} details
            """;
    }
}
```

## Agent with Middleware

```csharp
// Apply middleware for logging, auth propagation, or content safety
var agentWithMiddleware = baseAgent
    .AsBuilder()
    // Agent run middleware — intercept all runs
    .Use(
        runFunc: async (messages, session, options, innerAgent, ct) =>
        {
            // Pre-run: validate tenant context, log, etc.
            logger.LogInformation("Agent run: {MessageCount} messages", messages.Count());
            var response = await innerAgent.RunAsync(messages, session, options, ct);
            logger.LogInformation("Agent response: {ResponseCount} messages", response.Messages.Count);
            return response;
        })
    // Function calling middleware — intercept all tool invocations
    .Use(async (agent, context, next, ct) =>
    {
        logger.LogInformation("Tool call: {FunctionName}", context.Function.Name);
        var result = await next(context, ct);
        return result;
    })
    .Build();
```

## Agent-as-Tool Pattern

When one agent should be callable by another agent:

```csharp
// The inner agent handles a specialized task
AIAgent classifierAgent = openAiClient
    .GetChatClient(deploymentName)
    .AsAIAgent(
        instructions: "Classify the urgency of the request: Low, Medium, High, Critical. Return only the classification.",
        name: "ClassifierAgent",
        description: "Classifies request urgency level.");

// The outer agent can call the inner agent as a function tool
AIAgent orchestratorAgent = openAiClient
    .GetChatClient(deploymentName)
    .AsAIAgent(
        instructions: "You handle incoming requests. Use available tools to classify and process them.",
        name: "OrchestratorAgent",
        tools:
        [
            classifierAgent.AsAIFunction(),   // agent-to-agent delegation
            otherTool                          // regular function tools
        ]);
```

## Foundry Agent Service (Hosted Agent)

Use when you want server-managed memory, knowledge grounding (Foundry IQ), or the hosted tool catalog.

```csharp
namespace {Org}.{Project}.Infrastructure.AI.Agents;

using Azure.AI.Agents.Persistent;
using Azure.Identity;
using Microsoft.Agents.AI;
using Microsoft.Extensions.Options;

internal sealed class {Agent}FoundryAgentService : I{Agent}Agent
{
    private readonly AIAgent _agent;

    public {Agent}FoundryAgentService(IOptions<AiSettings> settings)
    {
        var client = new PersistentAgentsClient(
            new Uri(settings.Value.FoundryAgentServiceEndpoint),
            new DefaultAzureCredential());

        // CreateAIAgentAsync registers the agent with Foundry Agent Service
        _agent = client.CreateAIAgentAsync(
            model: settings.Value.AgentModelDeployment,
            instructions: LoadSystemPrompt(),
            name: "{Agent}")
            .GetAwaiter().GetResult(); // Consider async factory pattern in production
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

    private static string LoadSystemPrompt() => """
        You are {Agent}, an AI assistant for {Project}.
        Use your knowledge base and available tools to help users.
        """;
}
```

## Function Tool Template

```csharp
namespace {Org}.{Project}.Infrastructure.AI.Agents.Tools;

using System.ComponentModel;
using Microsoft.Extensions.AI;

/// <summary>
/// Function tools for {Agent}. Each method wraps a domain service operation
/// so the agent can invoke it during reasoning.
/// </summary>
internal static class {Agent}Tools
{
    [Description("Search for {entity}s matching a natural language query")]
    public static async Task<object> Search{Entity}s(
        [Description("The search query")] string query,
        I{Project}SearchService searchService,
        CancellationToken ct)
    {
        var results = await searchService.SearchAsync(query, SearchMode.Hybrid, ct);
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

    // Register tools with an agent:
    // tools: [
    //     AIFunctionFactory.Create(
    //         (string query, CancellationToken ct) => {Agent}Tools.Search{Entity}s(query, searchService, ct),
    //         nameof(Search{Entity}s), "Search {entity}s"),
    //     AIFunctionFactory.Create(
    //         (string id, CancellationToken ct) => {Agent}Tools.Get{Entity}(id, entityService, ct),
    //         nameof(Get{Entity}), "Get {entity} by ID"),
    // ]
}
```

## System Prompt File

Create as `Prompts/{Agent}.system-prompt.txt`:

```text
You are {Agent}, an AI assistant for {Project}.

## Your Role
{Describe what this agent does in business terms}

## Guidelines
- Use available tools to answer questions accurately
- When using Search{Entity}s, prefer hybrid search for best results
- If results are insufficient, try rephrasing the query
- Always cite the source {entity} ID when referencing search results
- Respect data boundaries — only reference data from tool results
- If you cannot answer a question with available tools, say so clearly

## Response Format
- Be concise and direct
- Use bullet points for multiple items
- Include relevant IDs for traceability
```

## DI Registration

```csharp
// In ServiceCollectionExtensions.cs or Bootstrapper
services.AddScoped<I{Agent}Agent, {Agent}AgentService>();

// If using Foundry Agent Service instead:
// services.AddScoped<I{Agent}Agent, {Agent}FoundryAgentService>();
```

## API Endpoint

```csharp
// In the API project — agent interaction endpoint
group.MapPost("/agent/{agent-route}/chat", async (
    [FromBody] AgentChatRequest request,
    I{Agent}Agent agent,
    CancellationToken ct) =>
{
    var session = !string.IsNullOrEmpty(request.SessionId)
        ? await agent.CreateSessionAsync(ct)  // TODO: restore session from store
        : await agent.CreateSessionAsync(ct);

    var response = await agent.RunAsync(request.Message, session, ct);

    return TypedResults.Ok(new AgentChatResponse
    {
        Messages = response.Messages.Select(m => m.Text).ToList(),
        SessionId = session?.ToString()
    });
})
.WithName("Chat{Agent}")
.WithSummary("Send a message to the {Agent} agent");
```
````
