namespace TaskFlow.UI.Business.Models;

/// <summary>
/// Login credentials for authentication flow.
/// </summary>
public record Credentials
{
    public string? Username { get; init; }
    public string? Password { get; init; }
}
