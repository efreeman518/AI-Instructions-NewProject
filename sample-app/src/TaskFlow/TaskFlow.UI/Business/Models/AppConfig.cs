namespace TaskFlow.UI.Business.Models;

/// <summary>
/// Application configuration bound from appsettings.json.
/// </summary>
public record AppConfig
{
    public string Title { get; init; } = "TaskFlow";
}

/// <summary>
/// JSON serialization context for AppConfig (used by Uno Serialization).
/// </summary>
[System.Text.Json.Serialization.JsonSerializable(typeof(string))]
public partial class AppConfigContext : System.Text.Json.Serialization.JsonSerializerContext;
