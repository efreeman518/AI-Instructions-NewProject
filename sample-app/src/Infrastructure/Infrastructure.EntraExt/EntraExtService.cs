using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;

namespace Infrastructure.EntraExt;

public class EntraExtService(
    ILogger<EntraExtService> logger,
    IOptions<EntraExtServiceSettings> settings) : IEntraExtService
{
    private readonly EntraExtServiceSettings _settings = settings.Value;

    public async Task<string?> GetUserDisplayNameAsync(Guid userId, CancellationToken ct = default)
    {
        logger.LogInformation("Getting display name for user {UserId}", userId);
        // MS Graph call would go here via EF.MSGraph base class
        await Task.CompletedTask;
        return null;
    }

    public async Task<string?> GetUserEmailAsync(Guid userId, CancellationToken ct = default)
    {
        logger.LogInformation("Getting email for user {UserId}", userId);
        // MS Graph call would go here via EF.MSGraph base class
        await Task.CompletedTask;
        return null;
    }
}
