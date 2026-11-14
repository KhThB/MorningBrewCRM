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
    /// <summary>[UI-05] Không có URL chi tiết: bấm dòng mở ngăn hiển thị đủ các cột của dòng (kể cả cột bị ẩn trong bảng).</summary>
    public bool RowDetail {get;init;}
    /// <summary>[UI-05] Cột thêm vào ngăn chi tiết dạng (nhãn, giá trị) – ví dụ nội dung phản hồi đầy đủ.</summary>
    public Func<Dictionary<string,JsonElement>,IEnumerable<(string Label,string Value)>>? ExtraDetail {get;init;}
    /// <summary>Định dạng riêng cho một ô (null = mặc định).</summary>
    public Func<string,Dictionary<string,JsonElement>,string?>? Format {get;init;}
    /// <summary>[UI-03] Chữ cho nhãn cột is_active theo ngữ cảnh (Đang bán / Ngừng bán…); null = Hoạt động / Đang khóa.</summary>
    public (string On,string Off)? ActiveLabels {get;init;}
    public string Badge(string key,JsonElement value)
        => key=="is_active" && ActiveLabels is { } l && value.ValueKind is JsonValueKind.True or JsonValueKind.False ? (value.GetBoolean()?l.On:l.Off) : BadgeText(key,value);
    public string EmptyText {get;init;}="Chưa có dữ liệu phù hợp. Thử đổi bộ lọc hoặc thêm bản ghi mới.";

    /// <summary>[UI-03] Lớp CSS nhãn màu cho các cột trạng thái; null = hiển thị chữ thường.</summary>
    public static string? BadgeClass(string key,JsonElement value)
    {
        if(key=="is_active" && value.ValueKind is JsonValueKind.True or JsonValueKind.False) return value.GetBoolean()?"ok":"bad";
        if(key=="in_stock" && value.ValueKind is JsonValueKind.True or JsonValueKind.False) return value.GetBoolean()?"ok":"warn"; // [UI2-A4a]
        if(key=="is_low" && value.ValueKind is JsonValueKind.True or JsonValueKind.False) return value.GetBoolean()?"bad":"ok"; // [KHO] cảnh báo đỏ khi dưới mức tối thiểu
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
        => key=="in_stock" && value.ValueKind is JsonValueKind.True or JsonValueKind.False ? (value.GetBoolean()?"Còn hàng":"Tạm hết") // [UI2-A4a]
        : key=="is_low" && value.ValueKind is JsonValueKind.True or JsonValueKind.False ? (value.GetBoolean()?"Sắp hết":"Đủ hàng") // [KHO]
        : key=="is_active" && value.ValueKind is JsonValueKind.True or JsonValueKind.False
            ? (value.GetBoolean()?"Hoạt động":"Đang khóa") : RowDisplay.Value(key,value);
}
