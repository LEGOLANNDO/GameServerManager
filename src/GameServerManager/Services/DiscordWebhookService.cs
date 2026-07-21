using System.Diagnostics;
using System.Net.Http;
using System.Text;
using System.Text.Json;

namespace GameServerManager.Services;

/// <summary>
/// Discord Webhook 送信サービス
/// 仕様書 §3.4 の4パターンの Embed 通知を構築・送信する
/// </summary>
public class DiscordWebhookService
{
    private readonly HttpClient _httpClient;
    private readonly ConfigService _configService;

    // Embed カラー定義 (§3.4)
    private const int ColorGreen = 0x2ECC71;   // 起動
    private const int ColorRed = 0xE74C3C;     // クラッシュ
    private const int ColorOrange = 0xE67E22;  // 停止
    private const int ColorYellow = 0xF1C40F;  // 再起動

    public DiscordWebhookService(ConfigService configService)
    {
        _configService = configService;
        _httpClient = new HttpClient();
        _httpClient.DefaultRequestHeaders.Add("User-Agent", "GameServerManager/1.0");
    }

    /// <summary>
    /// サーバーに適用するWebhook URLを解決する
    /// サーバー固有 → グローバル の優先順位
    /// </summary>
    public string ResolveWebhookUrl(string serverEncryptedWebhook)
    {
        // サーバー固有のWebhookがあればそちらを使用
        if (!string.IsNullOrEmpty(serverEncryptedWebhook))
        {
            var serverUrl = _configService.DecryptWebhookUrl(serverEncryptedWebhook);
            if (!string.IsNullOrEmpty(serverUrl))
                return serverUrl;
        }

        // グローバルWebhookにフォールバック
        var config = _configService.LoadConfig();
        return _configService.DecryptWebhookUrl(config.AppSettings.EncryptedGlobalWebhook);
    }

    /// <summary>
    /// 🟢 起動完了通知
    /// </summary>
    public async Task<bool> SendStartNotificationAsync(string serverName, string webhookUrl)
    {
        if (string.IsNullOrEmpty(webhookUrl)) return false;

        var embed = new Dictionary<string, object>
        {
            ["title"] = $"🟢【起動】{serverName}",
            ["color"] = ColorGreen,
            ["fields"] = new[]
            {
                new Dictionary<string, object> { ["name"] = "起動時刻", ["value"] = DateTime.Now.ToString("yyyy/MM/dd HH:mm:ss"), ["inline"] = true }
            },
            ["timestamp"] = DateTime.UtcNow.ToString("o")
        };

        return await SendEmbedAsync(webhookUrl, embed);
    }

    /// <summary>
    /// 🚨 クラッシュ・異常停止通知
    /// </summary>
    public async Task<bool> SendCrashNotificationAsync(string serverName, int? exitCode, string webhookUrl)
    {
        if (string.IsNullOrEmpty(webhookUrl)) return false;

        var fields = new List<Dictionary<string, object>>
        {
            new() { ["name"] = "停止検出時刻", ["value"] = DateTime.Now.ToString("yyyy/MM/dd HH:mm:ss"), ["inline"] = true }
        };

        if (exitCode.HasValue)
        {
            fields.Add(new Dictionary<string, object>
            {
                ["name"] = "終了コード", ["value"] = exitCode.Value.ToString(), ["inline"] = true
            });
        }

        var embed = new Dictionary<string, object>
        {
            ["title"] = $"🚨【クラッシュ・異常停止】{serverName}",
            ["color"] = ColorRed,
            ["fields"] = fields,
            ["timestamp"] = DateTime.UtcNow.ToString("o")
        };

        return await SendEmbedAsync(webhookUrl, embed);
    }

    /// <summary>
    /// 🛑 アプリからの停止通知
    /// </summary>
    public async Task<bool> SendStopNotificationAsync(
        string serverName, string classification, string reason, string webhookUrl)
    {
        if (string.IsNullOrEmpty(webhookUrl)) return false;

        var embed = new Dictionary<string, object>
        {
            ["title"] = $"🛑【停止 - {classification}】{serverName}",
            ["color"] = ColorOrange,
            ["fields"] = new[]
            {
                new Dictionary<string, object> { ["name"] = "実施区分", ["value"] = classification, ["inline"] = true },
                new Dictionary<string, object> { ["name"] = "停止理由", ["value"] = string.IsNullOrEmpty(reason) ? "(未入力)" : reason, ["inline"] = false },
                new Dictionary<string, object> { ["name"] = "実行時刻", ["value"] = DateTime.Now.ToString("yyyy/MM/dd HH:mm:ss"), ["inline"] = true }
            },
            ["timestamp"] = DateTime.UtcNow.ToString("o")
        };

        return await SendEmbedAsync(webhookUrl, embed);
    }

    /// <summary>
    /// 🔄 アプリからの再起動通知
    /// </summary>
    public async Task<bool> SendRestartNotificationAsync(
        string serverName, string classification, string reason, string webhookUrl)
    {
        if (string.IsNullOrEmpty(webhookUrl)) return false;

        var embed = new Dictionary<string, object>
        {
            ["title"] = $"🔄【再起動 - {classification}】{serverName}",
            ["color"] = ColorYellow,
            ["fields"] = new[]
            {
                new Dictionary<string, object> { ["name"] = "実施区分", ["value"] = classification, ["inline"] = true },
                new Dictionary<string, object> { ["name"] = "再起動理由", ["value"] = string.IsNullOrEmpty(reason) ? "(未入力)" : reason, ["inline"] = false },
                new Dictionary<string, object> { ["name"] = "実行時刻", ["value"] = DateTime.Now.ToString("yyyy/MM/dd HH:mm:ss"), ["inline"] = true }
            },
            ["timestamp"] = DateTime.UtcNow.ToString("o")
        };

        return await SendEmbedAsync(webhookUrl, embed);
    }

    /// <summary>
    /// テスト通知送信 (§4.2)
    /// </summary>
    public async Task<bool> SendTestNotificationAsync(string webhookUrl)
    {
        if (string.IsNullOrEmpty(webhookUrl)) return false;

        var embed = new Dictionary<string, object>
        {
            ["title"] = "✅ テスト通知",
            ["description"] = "Game Server Manager からのテスト通知です。\nWebhook接続が正常に確認されました。",
            ["color"] = ColorGreen,
            ["fields"] = new[]
            {
                new Dictionary<string, object> { ["name"] = "送信時刻", ["value"] = DateTime.Now.ToString("yyyy/MM/dd HH:mm:ss"), ["inline"] = true },
                new Dictionary<string, object> { ["name"] = "アプリバージョン", ["value"] = "v1.0.0", ["inline"] = true }
            },
            ["footer"] = new Dictionary<string, object> { ["text"] = "Game Server Manager" },
            ["timestamp"] = DateTime.UtcNow.ToString("o")
        };

        return await SendEmbedAsync(webhookUrl, embed);
    }

    /// <summary>
    /// Embed ペイロードを構築して Webhook URL に POST 送信
    /// </summary>
    private async Task<bool> SendEmbedAsync(string webhookUrl, Dictionary<string, object> embed)
    {
        try
        {
            var payload = new Dictionary<string, object>
            {
                ["embeds"] = new[] { embed }
            };

            var json = JsonSerializer.Serialize(payload, new JsonSerializerOptions
            {
                PropertyNamingPolicy = JsonNamingPolicy.CamelCase,
                WriteIndented = false
            });

            var content = new StringContent(json, Encoding.UTF8, "application/json");
            var response = await _httpClient.PostAsync(webhookUrl, content);

            if (response.IsSuccessStatusCode)
            {
                Debug.WriteLine($"[Discord] Notification sent successfully to {webhookUrl[..50]}...");
                return true;
            }

            Debug.WriteLine($"[Discord] Failed to send notification. Status: {response.StatusCode}");
            return false;
        }
        catch (Exception ex)
        {
            Debug.WriteLine($"[Discord] Error sending notification: {ex.Message}");
            return false;
        }
    }
}
