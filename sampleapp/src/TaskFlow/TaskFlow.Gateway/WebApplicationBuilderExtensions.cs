// ═══════════════════════════════════════════════════════════════
// Pattern: Gateway pipeline configuration.
// Middleware order: Security → CORS → Routing → RateLimiter → Auth → Endpoints → ReverseProxy.
// ReverseProxy must be LAST — it terminates the pipeline.
// ═══════════════════════════════════════════════════════════════

namespace TaskFlow.Gateway;

internal static class WebApplicationBuilderExtensions
{
    // ═══════════════════════════════════════════════════════════════
    // Pattern: Single ConfigurePipeline method with ordered middleware.
    // Called from Program.cs after Build().
    // ═══════════════════════════════════════════════════════════════

    public static WebApplication ConfigurePipeline(this WebApplication app)
    {
        ConfigureSecurity(app);
        ConfigureCors(app);
        ConfigureMiddleware(app);
        ConfigureEndpoints(app);
        ConfigureReverseProxy(app);
        return app;
    }

    // Pattern: HTTPS redirect (optional — comment out for local dev behind Aspire).
    private static void ConfigureSecurity(WebApplication app)
    {
        if (!app.Environment.IsDevelopment())
        {
            app.UseHttpsRedirection();
        }
    }

    // Pattern: CORS before routing — required for preflight OPTIONS requests.
    private static void ConfigureCors(WebApplication app)
    {
        app.UseCors("GatewayPolicy");
    }

    // Pattern: Routing → Auth → OpenAPI.
    private static void ConfigureMiddleware(WebApplication app)
    {
        app.UseRouting();
        app.UseAuthentication();
        app.UseAuthorization();
    }

    // Pattern: Basic endpoints — health, liveness, root redirect.
    private static void ConfigureEndpoints(WebApplication app)
    {
        app.MapDefaultEndpoints();

        app.MapGet("/", () => Results.Redirect("/health"))
            .AllowAnonymous();

        app.MapGet("/alive", () => Results.Ok("alive"))
            .AllowAnonymous();
    }

    // ═══════════════════════════════════════════════════════════════
    // Pattern: YARP reverse proxy — MUST be last in the pipeline.
    // Wraps proxy with error logging for traceability.
    // ═══════════════════════════════════════════════════════════════

    private static void ConfigureReverseProxy(WebApplication app)
    {
        app.MapReverseProxy(pipeline =>
        {
            // Pattern: Error logging wrapper — log proxy errors with trace ID.
            pipeline.Use(async (context, next) =>
            {
                try
                {
                    await next();
                }
                catch (Exception ex)
                {
                    var logger = context.RequestServices.GetRequiredService<ILogger<Program>>();
                    logger.LogError(ex, "Proxy error. TraceId: {TraceId}", context.TraceIdentifier);
                    throw;
                }
            });
        });
    }
}
