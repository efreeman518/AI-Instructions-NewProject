namespace Infrastructure.EntraExt;

public class EntraExtServiceSettings
{
    public const string ConfigurationSection = "EntraExt";

    public string TenantId { get; set; } = string.Empty;
    public string ClientId { get; set; } = string.Empty;
    public string ClientSecret { get; set; } = string.Empty;
}
