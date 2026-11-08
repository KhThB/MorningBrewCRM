using System.Text.Json;
namespace MorningBrew.Admin.Services;

/// <summary>[UI-02][UI-03][UI-05] Cấu hình bảng danh sách dùng chung (Pages/Shared/_Table.cshtml):
/// ẩn cột, cột "Thao tác" theo từng dòng, nhãn màu cho trạng thái, bấm dòng để mở ngăn chi tiết.</summary>
public sealed class TableView
{
    public required List<Dictionary<string,JsonElement>> Rows {get;init;}
    /// <summary>Các cột không hiển thị (ngoài name/etag luôn ẩn).</summary>
    public IReadOnlySet<string> Hide {get;init;}=new HashSet<string>();
    /// <summary>Nội dung cột "Thao tác" cho mỗi dòng (Razor template: @&lt;div&gt;…@item[…]…&lt;/div&gt;).</summary>
    public Func<Dictionary<string,JsonElement>,object>? Actions {get;init;}
    /// <summary>URL trả về HTML chi tiết của dòng – bấm vào dòng sẽ mở ngăn bên phải (U5).</summary>
    public Func<Dictionary<string,JsonElement>,string?>? DetailUrl {get;init;}
    public string DetailTitle {get;init;}="Chi tiết";
    public string EmptyText {get;init;}="Chưa có dữ liệu phù hợp. Thử đổi bộ lọc hoặc thêm bản ghi mới.";

    /// <summary>[UI-03] Lớp CSS nhãn màu cho các cột trạng thái; null = hiển thị chữ thường.</summary>
    public static string? BadgeClass(string key,JsonElement value)
    {
        if(key=="is_active" && value.ValueKind is JsonValueKind.True or JsonValueKind.False) return value.GetBoolean()?"ok":"bad";
        if(key!="status" || value.ValueKind!=JsonValueKind.String) return null;
        return value.GetString() switch
        {
            "New" or "Draft" or "Pending" or "AwaitingPayment" => "info",
            "Processing" or "Preparing" or "Confirmed" or "Ready" or "Delivering" => "warn",
            "Resolved" or "Open" or "Completed" or "Succeeded" => "ok",
            "Closed" or "Cancelled" or "Failed" => "muted",
            _ => "info"
        };
    }
    public static string BadgeText(string key,JsonElement value)
        => key=="is_active" && value.ValueKind is JsonValueKind.True or JsonValueKind.False
            ? (value.GetBoolean()?"Hoạt động":"Đang khóa") : RowDisplay.Value(key,value);
}
