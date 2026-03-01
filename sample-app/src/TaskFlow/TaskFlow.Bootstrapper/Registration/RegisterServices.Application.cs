using Application.Contracts.Repositories;
using Application.Contracts.Services;
using Application.Services;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using EF.BackgroundServices.InternalMessageBus;

namespace TaskFlow.Bootstrapper;

public static partial class RegisterServices
{
    private static void AddApplicationServices(this IServiceCollection services, IConfiguration config)
    {
        services.AddScoped<ITodoItemService, TodoItemService>();
        services.AddScoped<ICategoryService, CategoryService>();
        services.AddScoped<ITagService, TagService>();
        services.AddScoped<ITeamService, TeamService>();
        services.AddScoped<IMaintenanceService, MaintenanceService>();
    }

    private static void AddMessageHandlers(IServiceCollection services)
    {
        // Message handlers are auto-registered via [ScopedMessageHandler] attribute
        // by the InternalMessageBus.AutoRegisterHandlers() call in startup
    }
}
