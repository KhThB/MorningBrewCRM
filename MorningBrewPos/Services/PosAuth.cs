using Microsoft.Maui.Storage;

namespace MorningBrew.Pos.Services;

/// <summary>[BUG-02 · giai đoạn 2] Đăng nhập POS: chỉ thu ngân, quản lý, chủ cửa hàng. Khách hàng dùng app khách.</summary>
public sealed class PosAuth
{
    private readonly ApiClient api;
    private const string TokenKey = "mb_pos_token";
    private bool loaded;
    public IdentityInfo? User { get; private set; }
    public bool IsLoggedIn => User is not null && api.Token is not null;
    /// <summary>Thu ngân không có quyền hủy đơn / hoàn tiền (Orders.Cancel, Refunds.Manage).</summary>
    public bool CanCancel => User?.Role is "BranchManager" or "Owner";
    public event Action? Changed;

    public PosAuth(ApiClient api)
    {
        this.api = api;
        api.Unauthorized += () => { Clear(); };
    }

    public async Task EnsureLoadedAsync()
    {
        if (loaded) return;
        loaded = true;
        string? token = null;
        try { token = await SecureStorage.Default.GetAsync(TokenKey); } catch { token = Preferences.Default.Get<string?>(TokenKey, null); }
        if (string.IsNullOrEmpty(token)) return;
        api.Token = token;
        try
        {
            var me = await api.Get<IdentityInfo>("v1/auth/me");
            if (me.Role is "Staff" or "BranchManager" or "Owner") User = me; else Clear();
        }
        catch (ApiException) { Clear(); }
    }

    public async Task LoginAsync(string email, string password)
    {
        var result = await api.Send<LoginResponse>(HttpMethod.Post, "v1/auth/login", new { Username = email.Trim(), Password = password });
        if (result.User.Role is not ("Staff" or "BranchManager" or "Owner"))
        {
            api.Token = result.Token;
            try { await api.Send(HttpMethod.Post, "v1/auth/logout"); } catch (ApiException) { }
            api.Token = null;
            throw new ApiException(403, "App POS dành cho nhân viên cửa hàng. Khách hàng vui lòng dùng app Morning Brew.");
        }
        api.Token = result.Token;
        User = result.User;
        try { await SecureStorage.Default.SetAsync(TokenKey, result.Token); } catch { Preferences.Default.Set(TokenKey, result.Token); }
        Changed?.Invoke();
    }

    public async Task LogoutAsync()
    {
        try { if (api.Token is not null) await api.Send(HttpMethod.Post, "v1/auth/logout"); } catch (ApiException) { }
        Clear();
    }

    private void Clear()
    {
        api.Token = null; User = null;
        try { SecureStorage.Default.Remove(TokenKey); } catch { }
        try { Preferences.Default.Remove(TokenKey); } catch { }
        Changed?.Invoke();
    }
}
