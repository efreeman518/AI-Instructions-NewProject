````markdown
# AI Search Template

## Search Service Interface

```csharp
namespace {Org}.{Project}.Infrastructure.AI.Search;

public interface I{Project}SearchService
{
    Task<IReadOnlyList<{Entity}SearchResult>> SearchAsync(
        string query, SearchMode mode = SearchMode.Hybrid, CancellationToken ct = default);

    Task IndexDocumentAsync({Entity}SearchDocument document, CancellationToken ct = default);

    Task IndexDocumentsBatchAsync(IEnumerable<{Entity}SearchDocument> documents, CancellationToken ct = default);

    Task DeleteDocumentAsync(string documentId, CancellationToken ct = default);
}

public enum SearchMode
{
    Keyword,
    Vector,
    Hybrid
}
```

## Search Document Model

```csharp
namespace {Org}.{Project}.Infrastructure.AI.Search;

using Azure.Search.Documents.Indexes;
using Azure.Search.Documents.Indexes.Models;

public class {Entity}SearchDocument
{
    [SimpleField(IsKey = true, IsFilterable = true)]
    public string Id { get; set; } = null!;

    [SearchableField(AnalyzerName = LexicalAnalyzerName.Values.StandardLucene)]
    public string {Property1} { get; set; } = null!;

    [SearchableField(AnalyzerName = LexicalAnalyzerName.Values.StandardLucene)]
    public string {Property2} { get; set; } = null!;

    [VectorSearchField(
        VectorSearchDimensions = 1536,
        VectorSearchProfileName = "default-vector-profile")]
    public ReadOnlyMemory<float>? {Property2}Vector { get; set; }

    [SimpleField(IsFilterable = true, IsSortable = true)]
    public DateTimeOffset? LastModified { get; set; }

    // Add tenant isolation field when isTenantEntity: true
    [SimpleField(IsFilterable = true)]
    public string? TenantId { get; set; }
}
```

## Search Result Model

```csharp
namespace {Org}.{Project}.Infrastructure.AI.Search;

public class {Entity}SearchResult
{
    public string Id { get; set; } = null!;
    public string {Property1} { get; set; } = null!;
    public string {Property2} { get; set; } = null!;
    public double Score { get; set; }
    public double? RerankerScore { get; set; }
}
```

## Search Service Implementation

```csharp
namespace {Org}.{Project}.Infrastructure.AI.Search;

using Azure;
using Azure.Search.Documents;
using Azure.Search.Documents.Models;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;

internal sealed class {Project}SearchService : I{Project}SearchService
{
    private readonly SearchClient _searchClient;
    private readonly ILogger<{Project}SearchService> _logger;

    public {Project}SearchService(
        SearchClient searchClient,
        ILogger<{Project}SearchService> logger)
    {
        _searchClient = searchClient;
        _logger = logger;
    }

    public async Task<IReadOnlyList<{Entity}SearchResult>> SearchAsync(
        string query, SearchMode mode = SearchMode.Hybrid, CancellationToken ct = default)
    {
        var options = BuildSearchOptions(mode, query);
        var response = await _searchClient.SearchAsync<{Entity}SearchDocument>(query, options, ct);

        var results = new List<{Entity}SearchResult>();
        await foreach (var result in response.Value.GetResultsAsync())
        {
            results.Add(new {Entity}SearchResult
            {
                Id = result.Document.Id,
                {Property1} = result.Document.{Property1},
                {Property2} = result.Document.{Property2},
                Score = result.Score ?? 0,
                RerankerScore = result.SemanticSearch?.RerankerScore
            });
        }

        _logger.LogInformation("Search for '{Query}' returned {Count} results (mode: {Mode})",
            query, results.Count, mode);

        return results;
    }

    public async Task IndexDocumentAsync({Entity}SearchDocument document, CancellationToken ct = default)
    {
        await _searchClient.MergeOrUploadDocumentsAsync([document], cancellationToken: ct);
        _logger.LogInformation("Indexed document {DocumentId}", document.Id);
    }

    public async Task IndexDocumentsBatchAsync(
        IEnumerable<{Entity}SearchDocument> documents, CancellationToken ct = default)
    {
        var batch = IndexDocumentsBatch.MergeOrUpload(documents);
        await _searchClient.IndexDocumentsAsync(batch, cancellationToken: ct);
        _logger.LogInformation("Indexed batch of {Count} documents", batch.Actions.Count);
    }

    public async Task DeleteDocumentAsync(string documentId, CancellationToken ct = default)
    {
        await _searchClient.DeleteDocumentsAsync("Id", [documentId], cancellationToken: ct);
        _logger.LogInformation("Deleted document {DocumentId}", documentId);
    }

    private static SearchOptions BuildSearchOptions(SearchMode mode, string query)
    {
        return mode switch
        {
            SearchMode.Keyword => new SearchOptions
            {
                QueryType = SearchQueryType.Simple,
                Size = 10
            },
            SearchMode.Vector => new SearchOptions
            {
                VectorSearch = new()
                {
                    Queries =
                    {
                        new VectorizableTextQuery(query)
                        {
                            KNearestNeighborsCount = 5,
                            Fields = { "{Property2}Vector" }
                        }
                    }
                },
                Size = 10
            },
            SearchMode.Hybrid => new SearchOptions
            {
                QueryType = SearchQueryType.Semantic,
                SemanticSearch = new()
                {
                    SemanticConfigurationName = "default"
                },
                VectorSearch = new()
                {
                    Queries =
                    {
                        new VectorizableTextQuery(query)
                        {
                            KNearestNeighborsCount = 5,
                            Fields = { "{Property2}Vector" }
                        }
                    }
                },
                Size = 10
            },
            _ => throw new ArgumentOutOfRangeException(nameof(mode))
        };
    }
}
```

## Vectorization Handler (On-Write)

```csharp
namespace {Org}.{Project}.Infrastructure.AI.Search;

using Azure.AI.OpenAI;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;
using OpenAI.Embeddings;

internal sealed class {Entity}VectorizationHandler : IMessageHandler<{Entity}CreatedEvent>
{
    private readonly I{Project}SearchService _searchService;
    private readonly EmbeddingClient _embeddingClient;
    private readonly ILogger<{Entity}VectorizationHandler> _logger;

    public {Entity}VectorizationHandler(
        I{Project}SearchService searchService,
        AzureOpenAIClient openAiClient,
        IOptions<AiSettings> settings,
        ILogger<{Entity}VectorizationHandler> logger)
    {
        _searchService = searchService;
        _embeddingClient = openAiClient.GetEmbeddingClient(settings.Value.EmbeddingModelDeployment);
        _logger = logger;
    }

    public async Task HandleAsync({Entity}CreatedEvent evt, CancellationToken ct)
    {
        var textToEmbed = $"{evt.{Property1}} {evt.{Property2}}";
        var embedding = await _embeddingClient.GenerateEmbeddingAsync(textToEmbed, cancellationToken: ct);

        var document = new {Entity}SearchDocument
        {
            Id = evt.{Entity}Id.ToString(),
            {Property1} = evt.{Property1},
            {Property2} = evt.{Property2},
            {Property2}Vector = embedding.Value.ToFloats(),
            LastModified = DateTimeOffset.UtcNow,
            TenantId = evt.TenantId?.ToString()
        };

        await _searchService.IndexDocumentAsync(document, ct);
        _logger.LogInformation("Vectorized and indexed {Entity} {EntityId}", nameof({Entity}), evt.{Entity}Id);
    }
}
```

## Settings

```csharp
namespace {Org}.{Project}.Infrastructure.AI;

public class AiSettings
{
    public const string ConfigSectionName = "AiServices";

    public string FoundryEndpoint { get; set; } = null!;            // TODO: [CONFIGURE]
    public string AgentModelDeployment { get; set; } = null!;       // TODO: [CONFIGURE]
    public string EmbeddingModelDeployment { get; set; } = null!;   // TODO: [CONFIGURE]
    public string SearchEndpoint { get; set; } = null!;             // TODO: [CONFIGURE]
    public string SearchIndexName { get; set; } = null!;            // TODO: [CONFIGURE]
    public string FoundryAgentServiceEndpoint { get; set; } = "";   // TODO: [CONFIGURE] — only if useFoundryAgentService: true
}
```

## Index Creation (Setup / Migration Utility)

```csharp
// Run once during setup or as part of deployment script
var indexClient = new SearchIndexClient(
    new Uri(settings.SearchEndpoint),
    new DefaultAzureCredential());

var fieldBuilder = new FieldBuilder();
var fields = fieldBuilder.Build(typeof({Entity}SearchDocument));

var index = new SearchIndex("{entity}-index")
{
    Fields = fields,
    VectorSearch = new()
    {
        Profiles = { new VectorSearchProfile("default-vector-profile", "default-hnsw") },
        Algorithms = { new HnswAlgorithmConfiguration("default-hnsw") }
    },
    SemanticSearch = new()
    {
        Configurations =
        {
            new SemanticConfiguration("default", new()
            {
                TitleField = new SemanticField("{Property1}"),
                ContentFields = { new SemanticField("{Property2}") }
            })
        }
    }
};

await indexClient.CreateOrUpdateIndexAsync(index);
```
````
