using System.Text.Json.Serialization;

namespace GameServerManager.Models;

/// <summary>
/// アプリケーション全体の設定
/// </summary>
public class AppSettings
{
    [JsonPropertyName("theme")]
    public string Theme { get; set; } = "system";

    [JsonPropertyName("encrypted_global_webhook")]
    public string EncryptedGlobalWebhook { get; set; } = string.Empty;

    [JsonPropertyName("polling_interval_seconds")]
    public int PollingIntervalSeconds { get; set; } = 3;
}
