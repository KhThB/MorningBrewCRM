using System.Text;
using Microsoft.Maui.Storage;
#if WINDOWS
using System.Security.Cryptography;
#endif

namespace MauiApp1.Services;

/// <summary>[BUG-14] Lưu token phiên đăng nhập. Ưu tiên SecureStorage; khi SecureStorage không dùng được
/// (thường gặp khi chạy app Windows unpackaged) thì lưu vào Preferences nhưng MÃ HÓA bằng DPAPI theo tài khoản
/// Windows hiện tại – trước đây token nằm dạng chữ thường trong Preferences.</summary>
public static class TokenStore
{
    private const string Prefix = "dpapi:";
#if WINDOWS
    private static readonly byte[] Entropy = Encoding.UTF8.GetBytes("MorningBrew.Token.v1");
#endif

    public static async Task<string?> Read(string key)
    {
        try { return await SecureStorage.Default.GetAsync(key); }
        catch { return Unprotect(Preferences.Default.Get<string?>(key, null)); }
    }

    public static async Task Write(string key, string value)
    {
        try { await SecureStorage.Default.SetAsync(key, value); }
        catch { Preferences.Default.Set(key, Protect(value)); }
    }

    public static void Remove(string key)
    {
        try { SecureStorage.Default.Remove(key); } catch { }
        try { Preferences.Default.Remove(key); } catch { }
    }

    private static string Protect(string value)
    {
#if WINDOWS
        return Prefix + Convert.ToBase64String(ProtectedData.Protect(Encoding.UTF8.GetBytes(value), Entropy, DataProtectionScope.CurrentUser));
#else
        return value;
#endif
    }

    private static string? Unprotect(string? stored)
    {
        if (string.IsNullOrEmpty(stored)) return null;
        if (!stored.StartsWith(Prefix)) return stored; // dữ liệu cũ dạng chữ thường: đọc được, lần lưu sau sẽ mã hóa
#if WINDOWS
        try { return Encoding.UTF8.GetString(ProtectedData.Unprotect(Convert.FromBase64String(stored[Prefix.Length..]), Entropy, DataProtectionScope.CurrentUser)); }
        catch { return null; } // giải mã lỗi (đổi máy / đổi tài khoản Windows) → coi như chưa đăng nhập
#else
        return null;
#endif
    }
}
