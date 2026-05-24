using UnityEngine;
using System;
using System.Text;
using System.Security.Cryptography;
using System.IO;

/// <summary>
/// 存档数据加密 - AES加密保护存档文件
/// 防止玩家篡改存档数据（修改金币、解锁关卡等）
/// 提供加密/解密和完整性校验
/// </summary>
public static class SaveDataEncryption
{
    // 密钥派生参数（不要硬编码在正式发行版中）
    private static readonly byte[] Salt = Encoding.UTF8.GetBytes("DoubleForward2024Salt");
    private const int KeySize = 256;
    private const int BlockSize = 128;
    private const int Iterations = 1000;

    /// <summary>
    /// 获取设备唯一密钥（基于设备ID）
    /// </summary>
    private static string GetDeviceKey()
    {
        string deviceId = SystemInfo.deviceUniqueIdentifier;
        // 混合固定密钥和设备ID
        return $"DF_{deviceId}_SecureKey_2024";
    }

    /// <summary>
    /// 加密JSON字符串
    /// </summary>
    public static string Encrypt(string plainText)
    {
        if (string.IsNullOrEmpty(plainText)) return "";

        try
        {
            string key = GetDeviceKey();

            using (var deriveBytes = new Rfc2898DeriveBytes(key, Salt, Iterations))
            {
                byte[] keyBytes = deriveBytes.GetBytes(KeySize / 8);
                byte[] ivBytes = deriveBytes.GetBytes(BlockSize / 8);

                using (var aes = Aes.Create())
                {
                    aes.Key = keyBytes;
                    aes.IV = ivBytes;
                    aes.Mode = CipherMode.CBC;
                    aes.Padding = PaddingMode.PKCS7;

                    using (var encryptor = aes.CreateEncryptor())
                    {
                        byte[] plainBytes = Encoding.UTF8.GetBytes(plainText);
                        byte[] encryptedBytes = encryptor.TransformFinalBlock(plainBytes, 0, plainBytes.Length);

                        // 添加校验哈希
                        string hash = ComputeHash(plainText);
                        string combined = Convert.ToBase64String(encryptedBytes) + "|" + hash;

                        return Convert.ToBase64String(Encoding.UTF8.GetBytes(combined));
                    }
                }
            }
        }
        catch (Exception e)
        {
            Debug.LogError($"[Encryption] Encrypt failed: {e.Message}");
            return plainText; // 回退到明文
        }
    }

    /// <summary>
    /// 解密字符串
    /// </summary>
    public static string Decrypt(string encryptedText)
    {
        if (string.IsNullOrEmpty(encryptedText)) return "";

        try
        {
            // 先尝试解码外层Base64
            string combined;
            try
            {
                combined = Encoding.UTF8.GetString(Convert.FromBase64String(encryptedText));
            }
            catch
            {
                // 可能是未加密的旧版存档
                return encryptedText;
            }

            string[] parts = combined.Split('|');
            if (parts.Length != 2)
            {
                // 可能是未加密数据
                return encryptedText;
            }

            byte[] encryptedBytes = Convert.FromBase64String(parts[0]);
            string storedHash = parts[1];

            string key = GetDeviceKey();

            using (var deriveBytes = new Rfc2898DeriveBytes(key, Salt, Iterations))
            {
                byte[] keyBytes = deriveBytes.GetBytes(KeySize / 8);
                byte[] ivBytes = deriveBytes.GetBytes(BlockSize / 8);

                using (var aes = Aes.Create())
                {
                    aes.Key = keyBytes;
                    aes.IV = ivBytes;
                    aes.Mode = CipherMode.CBC;
                    aes.Padding = PaddingMode.PKCS7;

                    using (var decryptor = aes.CreateDecryptor())
                    {
                        byte[] decryptedBytes = decryptor.TransformFinalBlock(encryptedBytes, 0, encryptedBytes.Length);
                        string decryptedText = Encoding.UTF8.GetString(decryptedBytes);

                        // 验证完整性
                        string computedHash = ComputeHash(decryptedText);
                        if (computedHash != storedHash)
                        {
                            Debug.LogWarning("[Encryption] Data integrity check failed! Save may be tampered.");
                            // 仍然返回数据，但记录警告
                        }

                        return decryptedText;
                    }
                }
            }
        }
        catch (Exception e)
        {
            Debug.LogError($"[Encryption] Decrypt failed: {e.Message}");
            // 可能是未加密的旧版存档，返回原文
            return encryptedText;
        }
    }

    /// <summary>
    /// 计算数据的SHA256哈希
    /// </summary>
    private static string ComputeHash(string data)
    {
        using (var sha256 = SHA256.Create())
        {
            byte[] hashBytes = sha256.ComputeHash(Encoding.UTF8.GetBytes(data));
            return Convert.ToBase64String(hashBytes);
        }
    }

    /// <summary>
    /// 检查文件是否为加密格式
    /// </summary>
    public static bool IsEncrypted(string data)
    {
        if (string.IsNullOrEmpty(data)) return false;

        try
        {
            string decoded = Encoding.UTF8.GetString(Convert.FromBase64String(data));
            return decoded.Contains("|");
        }
        catch
        {
            return false;
        }
    }

    /// <summary>
    /// 安全保存文件（加密后写入）
    /// </summary>
    public static void SaveEncrypted(string filePath, string jsonData, bool useEncryption = true)
    {
        try
        {
            string dataToWrite = useEncryption ? Encrypt(jsonData) : jsonData;

            // 先写入临时文件，再原子替换（防止写入中断导致数据丢失）
            string tempPath = filePath + ".tmp";
            File.WriteAllText(tempPath, dataToWrite);

            // 备份旧文件
            if (File.Exists(filePath))
            {
                string backupPath = filePath + ".bak";
                if (File.Exists(backupPath))
                    File.Delete(backupPath);
                File.Move(filePath, backupPath);
            }

            // 替换
            File.Move(tempPath, filePath);
        }
        catch (Exception e)
        {
            Debug.LogError($"[Encryption] SaveEncrypted failed: {e.Message}");
            // 应急：直接写入
            File.WriteAllText(filePath, jsonData);
        }
    }

    /// <summary>
    /// 安全读取文件（解密后返回）
    /// </summary>
    public static string LoadDecrypted(string filePath, bool useEncryption = true)
    {
        try
        {
            if (!File.Exists(filePath))
            {
                // 尝试备份文件
                string backupPath = filePath + ".bak";
                if (File.Exists(backupPath))
                {
                    Debug.LogWarning("[Encryption] Main save missing, loading backup.");
                    filePath = backupPath;
                }
                else
                {
                    return null;
                }
            }

            string data = File.ReadAllText(filePath);

            if (!useEncryption) return data;

            // 自动检测是否加密
            if (IsEncrypted(data))
                return Decrypt(data);
            else
                return data; // 未加密的旧版存档
        }
        catch (Exception e)
        {
            Debug.LogError($"[Encryption] LoadDecrypted failed: {e.Message}");
            return null;
        }
    }
}
