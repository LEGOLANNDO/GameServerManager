using System.Text.Json.Serialization;

namespace GameServerManager.Models;

/// <summary>
/// config.json のルートモデル
/// </summary>
public class AppConfig
{
    [JsonPropertyName("app_settings")]
    public AppSettings AppSettings { get; set; } = new();

    [JsonPropertyName("servers")]
    public List<ServerConfig> Servers { get; set; } = new();
}
