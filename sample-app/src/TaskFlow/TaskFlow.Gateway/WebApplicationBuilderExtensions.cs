namespace TaskFlow.Gateway;

public static class WebApplicationBuilderExtensions
{
    public static WebApplication ConfigurePipeline(this WebApplication app)
    {
        app.UseExceptionHandler("/error");
        app.UseHttpsRedirection();
        app.UseRouting();
        app.UseCors();
        app.UseAuthentication();
        app.UseAuthorization();

        app.MapHealthChecks("/health").AllowAnonymous();
        app.MapGet("/alive", () => Results.Ok("Alive")).AllowAnonymous();

        app.MapReverseProxy();

        return app;
    }
}
