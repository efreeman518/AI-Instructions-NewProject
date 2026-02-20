// ═══════════════════════════════════════════════════════════════
// Pattern: DI Registration — AddNotificationServices extension method.
// Configuration-driven provider registration: only registers providers
// that have a configuration section present (graceful degradation).
// Called from Bootstrapper's RegisterInfrastructureServices method.
// ═══════════════════════════════════════════════════════════════

using Application.MessageHandlers;
using Infrastructure.Notification.Configuration;
using Infrastructure.Notification.Providers;
using Infrastructure.Notification.Providers.Email;
using Infrastructure.Notification.Providers.Sms;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;

namespace Infrastructure.Notification;

/// <summary>
/// Pattern: ServiceCollectionExtensions — static class with AddXxx extension methods.
/// Encapsulates all notification DI registrations in a single composable call.
/// </summary>
public static class ServiceCollectionExtensions
{
    /// <summary>
    /// Pattern: Configuration-driven provider registration.
    /// Only registers providers whose config sections exist in appsettings.
    /// Missing section = provider not available = graceful degradation at runtime.
    /// </summary>
    public static IServiceCollection AddNotificationServices(
        this IServiceCollection services, IConfiguration configuration)
    {
        // ═══════════════════════════════════════════════════════════════
        // Bind configuration
        // ═══════════════════════════════════════════════════════════════

        // Pattern: Bind strongly-typed options from "Notification" section.
        services.Configure<NotificationOptions>(
            configuration.GetSection(NotificationOptions.SectionName));

        // Pattern: Bind settings POCO for runtime checks.
        services.Configure<NotificationServiceSettings>(
            configuration.GetSection("Notification:Settings"));

        // ═══════════════════════════════════════════════════════════════
        // Conditional provider registration
        // Pattern: Only register when config section exists — prevents DI resolution
        // failures for unconfigured providers. NotificationService accepts nullable providers.
        // ═══════════════════════════════════════════════════════════════

        var notificationSection = configuration.GetSection(NotificationOptions.SectionName);

        if (notificationSection.GetSection("Email").Exists())
        {
            // Pattern: Singleton provider — email clients are thread-safe and reusable.
            services.AddSingleton<IEmailProvider, AzureEmailProvider>();
        }

        if (notificationSection.GetSection("Sms").Exists())
        {
            // Pattern: Singleton provider — Twilio client is initialized once.
            services.AddSingleton<ISmsProvider, TwilioSmsProvider>();
        }

        // ═══════════════════════════════════════════════════════════════
        // Register unified service
        // Pattern: Scoped lifetime — aligns with request scope in API host.
        // INotificationService is defined in Application.MessageHandlers,
        // implemented here in Infrastructure.Notification.
        // ═══════════════════════════════════════════════════════════════

        services.AddScoped<INotificationService, NotificationService>();

        return services;
    }
}
