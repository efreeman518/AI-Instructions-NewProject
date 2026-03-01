using Scalar.AspNetCore;
using TaskFlow.Api.Endpoints;

namespace TaskFlow.Api;

public static class WebApplicationBuilderExtensions
{
    public static WebApplication ConfigurePipeline(this WebApplication app)
    {
        app.UseExceptionHandler();
        app.UseHttpsRedirection();
        app.UseRouting();
        app.UseRateLimiter();
        app.UseAuthentication();
        app.UseAuthorization();

        // OpenAPI + Scalar
        if (app.Environment.IsDevelopment())
        {
            app.MapOpenApi();
            app.MapScalarApiReference(options =>
            {
                options.WithTitle("TaskFlow API");
                options.WithTheme(ScalarTheme.Moon);
            });
        }

        // Health endpoints
        app.MapHealthChecks("/health");
        app.MapGet("/alive", () => Results.Ok("Alive"));

        // API endpoints
        var apiGroup = app.MapGroup("api");
        apiGroup.MapTodoItemEndpoints();
        apiGroup.MapCategoryEndpoints();
        apiGroup.MapTagEndpoints();
        apiGroup.MapTeamEndpoints();
        apiGroup.MapMaintenanceEndpoints();

        return app;
    }
}
