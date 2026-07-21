using System.Security.Cryptography;
using System.Text;

namespace GameServerManager.Services;

/// <summary>
/// Windows DPAPI を利用した暗号化・復号サービス
/// Webhook URL 等の機密情報を CurrentUser スコープで保護
/// </summary>
public class CryptoService
{
    // アプリ固有のエントロピー（追加の保護層）
    private static readonly byte[] Entropy = Encoding.UTF8.GetBytes("GameServerManager_v1_Entropy");

    /// <summary>
    /// 平文を DPAPI で暗号化し、Base64 文字列として返す
    /// </summary>
    public string Encrypt(string plainText)
    {
        if (string.IsNullOrEmpty(plainText))
            return string.Empty;

        byte[] plainBytes = Encoding.UTF8.GetBytes(plainText);
        byte[] encryptedBytes = ProtectedData.Protect(
            plainBytes,
            Entropy,
            DataProtectionScope.CurrentUser
        );
        return Convert.ToBase64String(encryptedBytes);
    }

    /// <summary>
    /// Base64 暗号化文字列を DPAPI で復号し、平文を返す
    /// 復号失敗時は空文字列を返す
    /// </summary>
    public string Decrypt(string encryptedBase64)
    {
        if (string.IsNullOrEmpty(encryptedBase64))
            return string.Empty;

        try
        {
            byte[] encryptedBytes = Convert.FromBase64String(encryptedBase64);
            byte[] plainBytes = ProtectedData.Unprotect(
                encryptedBytes,
                Entropy,
                DataProtectionScope.CurrentUser
            );
            return Encoding.UTF8.GetString(plainBytes);
        }
        catch (CryptographicException)
        {
            // ユーザープロファイル不一致や鍵破損時
            return string.Empty;
        }
        catch (FormatException)
        {
            // 不正な Base64 文字列
            return string.Empty;
        }
    }
}
