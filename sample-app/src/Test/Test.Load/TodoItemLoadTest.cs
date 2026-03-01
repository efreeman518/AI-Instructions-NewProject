using System.Text;
using System.Text.Json;
using NBomber.Contracts;
using NBomber.CSharp;

// https://nbomber.com/

namespace Test.Load;

/// <summary>
/// Load test for TaskFlow API TodoItem CRUD endpoints.
/// Modeled after https://github.com/efreeman518/EF.DemoApp1.net/blob/main/Test.Load/TodoLoadTest.cs
/// </summary>
internal static class TodoItemLoadTest
{
    private static readonly JsonSerializerOptions JsonOptions = new()
    {
        PropertyNameCaseInsensitive = true,
        PropertyNamingPolicy = JsonNamingPolicy.CamelCase
    };

    public static void Run(string baseUrl)
    {
        using var httpClient = new HttpClient();
        httpClient.BaseAddress = new Uri(baseUrl);
        string url = $"{baseUrl}/api/todoitems";

        var scenario = Scenario.Create("todoitem-crud", async context =>
        {
            var tenantId = Guid.NewGuid();

            // POST — create a new TodoItem
            var createPayload = new
            {
                tenantId,
                title = $"LoadTest-{Guid.NewGuid()}",
                description = "Created by NBomber load test",
                priority = 3,
                status = 0
            };

            var postResponse = await Step.Run("post", context, async () =>
            {
                var content = new StringContent(
                    JsonSerializer.Serialize(createPayload, JsonOptions), Encoding.UTF8, "application/json");
                var response = await httpClient.PostAsync(url, content);
                if (!response.IsSuccessStatusCode)
                    return Response.Fail(statusCode: response.StatusCode.ToString());

                var body = await response.Content.ReadAsStringAsync();
                context.Data["createdId"] = ExtractId(body);
                return Response.Ok(sizeBytes: Encoding.UTF8.GetByteCount(body),
                    statusCode: response.StatusCode.ToString());
            });

            if (postResponse.IsError)
                return Response.Fail();

            var createdId = context.Data["createdId"];

            // GET — retrieve the created item
            await Step.Run("get", context, async () =>
            {
                var response = await httpClient.GetAsync($"{url}/{createdId}");
                var body = await response.Content.ReadAsStringAsync();
                return response.IsSuccessStatusCode
                    ? Response.Ok(sizeBytes: Encoding.UTF8.GetByteCount(body),
                        statusCode: response.StatusCode.ToString())
                    : Response.Fail(statusCode: response.StatusCode.ToString());
            });

            // DELETE — clean up
            await Step.Run("delete", context, async () =>
            {
                var response = await httpClient.DeleteAsync($"{url}/{createdId}");
                return response.IsSuccessStatusCode
                    ? Response.Ok(statusCode: response.StatusCode.ToString())
                    : Response.Fail(statusCode: response.StatusCode.ToString());
            });

            return Response.Ok();
        })
        .WithWarmUpDuration(TimeSpan.FromSeconds(10))
        .WithLoadSimulations(
            // Ramp up from 0 to 5 requests/sec over 15 seconds
            Simulation.RampingInject(rate: 5,
                interval: TimeSpan.FromSeconds(1),
                during: TimeSpan.FromSeconds(15)),
            // Sustain 5 requests/sec for 30 seconds
            Simulation.Inject(rate: 5,
                interval: TimeSpan.FromSeconds(1),
                during: TimeSpan.FromSeconds(30))
        );

        NBomberRunner
            .RegisterScenarios(scenario)
            .Run();
    }

    private static string ExtractId(string json)
    {
        using var doc = JsonDocument.Parse(json);
        return doc.RootElement.GetProperty("id").GetString() ?? string.Empty;
    }
}
