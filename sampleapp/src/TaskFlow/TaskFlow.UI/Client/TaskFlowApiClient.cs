// ═══════════════════════════════════════════════════════════════
// Pattern: Kiota-generated API client stub.
// In a real project, run: kiota generate --openapi <gateway-openapi-url>
//   --language CSharp --output ./Client --class-name TaskFlowApiClient
//   --namespace-name TaskFlow.UI.Client
//
// This produces typed request builders mapping 1:1 to Gateway endpoints.
// For this sample, we provide a minimal stub to show the pattern.
// ═══════════════════════════════════════════════════════════════

namespace TaskFlow.UI.Client;

/// <summary>
/// Pattern: Kiota-generated API client — registered via AddKiotaClient in App.xaml.host.cs.
/// All API calls go through the Gateway, which handles auth token relay.
/// </summary>
public class TaskFlowApiClient
{
    // Pattern: In a full Kiota-generated client, this would contain:
    //   public ApiRequestBuilder Api => new ApiRequestBuilder(PathParameters, RequestAdapter);
    // With nested builders like:
    //   api.Api.TodoItems.GetAsync()
    //   api.Api.TodoItems[id].GetAsync()
    //   api.Api.TodoItems.PostAsync(body)
    //   api.Api.Categories.GetAsync()
    //
    // Generated via: kiota generate \
    //   --openapi https://localhost:7200/openapi/v1.json \
    //   --language CSharp \
    //   --output ./Client \
    //   --class-name TaskFlowApiClient \
    //   --namespace-name TaskFlow.UI.Client
}
