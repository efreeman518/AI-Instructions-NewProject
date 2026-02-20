// ═══════════════════════════════════════════════════════════════
// Pattern: Aspire ServiceDefaults — shared telemetry, resilience, health.
// Referenced by all deployable projects (API, Scheduler, Gateway).
// Provides consistent OpenTelemetry, health checks, service discovery,
// and Polly resilience across the entire solution.
// ═══════════════════════════════════════════════════════════════

using Microsoft.AspNetCore.Builder;
using Microsoft.AspNetCore.Diagnostics.HealthChecks;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Diagnostics.HealthChecks;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Logging;
using OpenTelemetry;
using OpenTelemetry.Metrics;
using OpenTelemetry.Trace;

namespace Aspire.ServiceDefaults;

/// <summary>
/// Pattern: Shared service defaults — single place for cross-cutting concerns.
/// Every deployable host calls builder.AddServiceDefaults() in Program.cs.
/// </summary>
public static class Extensions
{
    // ═══════════════════════════════════════════════════════════════
    // Pattern: AddServiceDefaults — the main entry point.
    // Configures OpenTelemetry, health checks, service discovery, and resilience.
    // ═══════════════════════════════════════════════════════════════

    public static IHostApplicationBuilder AddServiceDefaults(this IHostApplicationBuilder builder)
    {
        builder.ConfigureOpenTelemetry();
        builder.AddDefaultHealthChecks();

        // Pattern: Service discovery — resolves Aspire resource names to URLs.
        builder.Services.AddServiceDiscovery();

        // Pattern: Polly resilience — standard retry + circuit breaker for all HttpClients.
        builder.Services.ConfigureHttpClientDefaults(http =>
        {
            http.AddStandardResilienceHandler();
            http.AddServiceDiscovery();
        });

        return builder;
    }

    // ═══════════════════════════════════════════════════════════════
    // Pattern: OpenTelemetry — traces + metrics exported to OTLP (Aspire dashboard).
    // ═══════════════════════════════════════════════════════════════

    private static IHostApplicationBuilder ConfigureOpenTelemetry(this IHostApplicationBuilder builder)
    {
        builder.Logging.AddOpenTelemetry(logging =>
        {
            logging.IncludeFormattedMessage = true;
            logging.IncludeScopes = true;
        });

        builder.Services.AddOpenTelemetry()
            .WithMetrics(metrics =>
            {
                metrics
                    .AddAspNetCoreInstrumentation()
                    .AddHttpClientInstrumentation()
                    .AddRuntimeInstrumentation();
            })
            .WithTracing(tracing =>
            {
                tracing
                    .AddAspNetCoreInstrumentation()
                    .AddHttpClientInstrumentation();
            });

        // Pattern: OTLP exporter — Aspire dashboard receives telemetry via OTLP.
        builder.AddOpenTelemetryExporters();

        return builder;
    }

    private static IHostApplicationBuilder AddOpenTelemetryExporters(this IHostApplicationBuilder builder)
    {
        var useOtlpExporter = !string.IsNullOrWhiteSpace(
            builder.Configuration["OTEL_EXPORTER_OTLP_ENDPOINT"]);

        if (useOtlpExporter)
        {
            builder.Services.AddOpenTelemetry().UseOtlpExporter();
        }

        return builder;
    }

    // ═══════════════════════════════════════════════════════════════
    // Pattern: Default health checks — /health (detailed) and /alive (liveness).
    // Aspire dashboard reads these automatically for resource status.
    // ═══════════════════════════════════════════════════════════════

    private static IHostApplicationBuilder AddDefaultHealthChecks(this IHostApplicationBuilder builder)
    {
        builder.Services.AddHealthChecks()
            .AddCheck("self", () => HealthCheckResult.Healthy(), ["live"]);

        return builder;
    }

    // ═══════════════════════════════════════════════════════════════
    // Pattern: MapDefaultEndpoints — maps /health and /alive.
    // Called from every host's pipeline configuration.
    // ═══════════════════════════════════════════════════════════════

    public static WebApplication MapDefaultEndpoints(this WebApplication app)
    {
        // Pattern: /health — runs ALL registered health checks.
        app.MapHealthChecks("/health");

        // Pattern: /alive — runs only "live" tagged checks (liveness probe).
        app.MapHealthChecks("/alive", new HealthCheckOptions
        {
            Predicate = registration => registration.Tags.Contains("live")
        });

        return app;
    }
}
