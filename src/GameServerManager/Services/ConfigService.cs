using System.IO;
using System.Text.Json;
using GameServerManager.Models;

namespace GameServerManager.Services;

/// <summary>
/// config.json の読み書きサービス
/// 暗号化フィールド (encrypted_*) は CryptoService 経由で自動暗号化/復号
/// </summary>
public class ConfigService
{
    private readonly CryptoService _cryptoService;
    private readonly string _configFilePath;
    private readonly JsonSerializerOptions _jsonOptions;

    public ConfigService(CryptoService cryptoService)
    {
        _cryptoService = cryptoService;

        // config.json は実行ファイルと同じディレクトリに配置
        string appDir = AppDomain.CurrentDomain.BaseDirectory;
        _configFilePath = Path.Combine(appDir, "config.json");

        _jsonOptions = new JsonSerializerOptions
        {
            WriteIndented = true,
            PropertyNamingPolicy = JsonNamingPolicy.SnakeCaseLower
        };
    }

    /// <summary>
    /// config.json を読み込み、暗号化フィールドを復号して返す
    /// ファイルが存在しない場合はデフォルト設定を生成して返す
    /// </summary>
    public AppConfig LoadConfig()
    {
        if (!File.Exists(_configFilePath))
        {
            var defaultConfig = CreateDefaultConfig();
            SaveConfig(defaultConfig);
            return defaultConfig;
        }

        try
        {
            string json = File.ReadAllText(_configFilePath);
            var config = JsonSerializer.Deserialize<AppConfig>(json, _jsonOptions) ?? CreateDefaultConfig();
            return config;
        }
        catch (JsonException)
        {
            return CreateDefaultConfig();
        }
    }

    /// <summary>
    /// AppConfig を config.json に書き出す
    /// 機密フィールドは暗号化された状態で保存される
    /// </summary>
    public void SaveConfig(AppConfig config)
    {
        string json = JsonSerializer.Serialize(config, _jsonOptions);
        
        // ディレクトリが存在しない場合は作成
        string? dir = Path.GetDirectoryName(_configFilePath);
        if (dir != null && !Directory.Exists(dir))
        {
            Directory.CreateDirectory(dir);
        }

        File.WriteAllText(_configFilePath, json);
    }

    /// <summary>
    /// Webhook URL を暗号化して返す
    /// </summary>
    public string EncryptWebhookUrl(string plainUrl)
    {
        return _cryptoService.Encrypt(plainUrl);
    }

    /// <summary>
    /// 暗号化された Webhook URL を復号して返す
    /// </summary>
    public string DecryptWebhookUrl(string encryptedUrl)
    {
        return _cryptoService.Decrypt(encryptedUrl);
    }

    /// <summary>
    /// デフォルト設定を生成
    /// </summary>
    private AppConfig CreateDefaultConfig()
    {
        return new AppConfig
        {
            AppSettings = new AppSettings
            {
                Theme = "system",
                EncryptedGlobalWebhook = string.Empty,
                PollingIntervalSeconds = 3
            },
            Servers = new List<ServerConfig>()
        };
    }
}
