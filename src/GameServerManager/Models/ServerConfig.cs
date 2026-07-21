using System.Text.Json.Serialization;

namespace GameServerManager.Models;

/// <summary>
/// 個別ゲームサーバーの設定
/// </summary>
public class ServerConfig
{
    [JsonPropertyName("id")]
    public string Id { get; set; } = string.Empty;

    [JsonPropertyName("name")]
    public string Name { get; set; } = string.Empty;

    /// <summary>
    /// サーバータイプ: exe, bat, cmd, ps1, jar, py, sh, docker, custom
    /// </summary>
    [JsonPropertyName("type")]
    public string Type { get; set; } = "exe";

    [JsonPropertyName("executable_path")]
    public string ExecutablePath { get; set; } = string.Empty;

    [JsonPropertyName("arguments")]
    public string Arguments { get; set; } = string.Empty;

    [JsonPropertyName("working_directory")]
    public string WorkingDirectory { get; set; } = string.Empty;

    [JsonPropertyName("encrypted_webhook")]
    public string EncryptedWebhook { get; set; } = string.Empty;
}
