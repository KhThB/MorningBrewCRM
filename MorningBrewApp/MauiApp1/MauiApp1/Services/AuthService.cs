using Microsoft.Maui.Storage;

namespace MauiApp1.Services;

/// <summary>Đăng nhập/đăng ký/đăng xuất và lưu token phiên trên thiết bị.</summary>
public sealed class AuthService
{
    private readonly ApiClient api;

    public AuthService(ApiClient api)
    {
        this.api = api;
        // Phiên bị thu hồi (ví dụ quản lý khóa tài khoản) → xóa phiên, MainLayout chuyển về /login.
        api.Unauthorized += () => _ = ClearAsync();
    }

    private const string TokenKey = "mb_token";
    private const string UserKey = "mb_user";
    private bool loaded;

    public IdentityInfo? User { get; private set; }
    public bool IsLoggedIn => User is not null && api.Token is not null;
    public event Action? Changed;

    /// <summary>Khôi phục phiên đã lưu (gọi một lần khi mở app).</summary>
    public async Task EnsureLoadedAsync()
    {
        if (loaded) return;
        loaded = true;
        var token = await Read(TokenKey);
        if (string.IsNullOrEmpty(token)) return;
        api.Token = token;
        try
        {
            var me = await api.Get<IdentityInfo>("v1/auth/me");
            if (me.Role != "Customer") { await ClearAsync(); return; }
            User = me;
        }
        catch (ApiException e) when (e.Status == 401) { await ClearAsync(); }
        catch (ApiException)
        {
            // Máy chủ tạm tắt: vẫn giữ phiên, các màn hình sẽ báo lỗi kết nối.
            var name = await Read(UserKey);
            User = name is null ? null : new IdentityInfo(0, "", name, "Customer");
        }
    }

    public async Task LoginAsync(string email, string password)
    {
        var result = await api.Send<LoginResponse>(HttpMethod.Post, "v1/auth/login",
            new { Username = email.Trim(), Password = password });
        if (result.User.Role != "Customer")
        {
            api.Token = result.Token;
            try { await api.Send(HttpMethod.Post, "v1/auth/logout"); } catch (ApiException) { }
            api.Token = null;
            throw new ApiException(403, "Ứng dụng này dành cho khách hàng. Quản lý vui lòng dùng web quản trị.");
        }
        api.Token = result.Token;
        User = result.User;
        await Write(TokenKey, result.Token);
        await Write(UserKey, result.User.FullName);
        Changed?.Invoke();
    }

    /// <summary>
    /// Đăng ký khách hàng (yêu cầu 4.3.1). Cần API POST /v1/auth/register — xem API_CAN_BO_SUNG.md.
    /// </summary>
    public async Task RegisterAsync(string fullName, string email, string? phone, string password)
    {
        try
        {
            await api.Send(HttpMethod.Post, "v1/auth/register",
                new { FullName = fullName.Trim(), Email = email.Trim(), Phone = string.IsNullOrWhiteSpace(phone) ? null : phone.Trim(), Password = password });
        }
        catch (ApiException e) when (e.IsMissingEndpoint)
        {
            throw new ApiException(404, "Máy chủ chưa hỗ trợ đăng ký (cần thêm API /v1/auth/register).");
        }
        await LoginAsync(email, password);
    }

    public async Task LogoutAsync()
    {
        try { if (api.Token is not null) await api.Send(HttpMethod.Post, "v1/auth/logout"); }
        catch (ApiException) { }
        await ClearAsync();
    }

    private async Task ClearAsync()
    {
        api.Token = null;
        User = null;
        Remove(TokenKey);
        Remove(UserKey);
        await Task.CompletedTask;
        Changed?.Invoke();
    }

    // SecureStorage có thể không dùng được trên Windows unpackaged → dùng Preferences thay thế.
    private static async Task<string?> Read(string key)
    {
        try { return await SecureStorage.Default.GetAsync(key); }
        catch { return Preferences.Default.Get<string?>(key, null); }
    }

    private static async Task Write(string key, string value)
    {
        try { await SecureStorage.Default.SetAsync(key, value); }
        catch { Preferences.Default.Set(key, value); }
    }

    private static void Remove(string key)
    {
        try { SecureStorage.Default.Remove(key); } catch { }
        try { Preferences.Default.Remove(key); } catch { }
    }
}
