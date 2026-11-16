using Microsoft.AspNetCore.Http;
namespace MorningBrew.Admin.Services;

// [SEP-01] Yêu cầu II.2 "Giao diện Admin server tách biệt với giao diện quản lý":
// cùng một project nhưng chạy HAI cổng, mỗi cổng là một giao diện riêng với phiên đăng nhập (cookie) riêng.
//   Admin server  : http://localhost:5190  → chỉ /Admin/*,   chỉ chủ cửa hàng (Owner)
//   Quản lý CRM   : http://localhost:5191  → chỉ /Manager/*, chủ cửa hàng + quản lý
// Cookie trình duyệt không phân biệt cổng, nên mỗi giao diện dùng một tên cookie khác nhau.
// Cổng và địa chỉ đọc từ appsettings.json (mục "Portals"); khi triển khai thật có thể đổi sang hai tên miền.
public sealed record Portal(string Key, string Scheme, string CookieName, string Title, string Prefix, string Home, int[] Ports, string BaseUrl, string[] Roles);

public static class Portals
{
    public const string Selector = "PortalByPort";
    public static Portal Admin { get; private set; } = null!;
    public static Portal Manager { get; private set; } = null!;

    public static void Load(IConfiguration config)
    {
        Admin = Build(config, "Admin", "AdminServer", "MorningBrew.AdminServer.Session", "Admin server", "/Admin", "/Admin/Index", "5190,7286", "http://localhost:5190", ["Owner"]);
        Manager = Build(config, "Manager", "ManagerCrm", "MorningBrew.Manager.Session", "Quản lý CRM", "/Manager", "/Manager/Index", "5191,7287", "http://localhost:5191", ["Owner", "BranchManager"]);
    }

    private static Portal Build(IConfiguration config, string key, string scheme, string cookie, string title, string prefix, string home, string ports, string url, string[] roles)
    {
        var section = config.GetSection($"Portals:{key}");
        var portList = (section["Ports"] ?? ports).Split(',', StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries).Select(int.Parse).ToArray();
        return new Portal(key, scheme, cookie, title, prefix, home, portList, (section["BaseUrl"] ?? url).TrimEnd('/'), roles);
    }

    // Giao diện của request hiện tại, xác định theo cổng máy chủ nhận request. Null = cổng không thuộc giao diện nào.
    public static Portal? Of(HttpContext context)
    {
        var port = context.Connection.LocalPort;
        return Admin.Ports.Contains(port) ? Admin : Manager.Ports.Contains(port) ? Manager : null;
    }

    public static Portal Other(Portal portal) => portal == Admin ? Manager : Admin;

    public static bool Allows(Portal portal, string? role) => role is not null && portal.Roles.Contains(role);
}
