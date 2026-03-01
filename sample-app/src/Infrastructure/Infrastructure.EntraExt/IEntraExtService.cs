namespace Infrastructure.EntraExt;

public interface IEntraExtService
{
    Task<string?> GetUserDisplayNameAsync(Guid userId, CancellationToken ct = default);
    Task<string?> GetUserEmailAsync(Guid userId, CancellationToken ct = default);
}
