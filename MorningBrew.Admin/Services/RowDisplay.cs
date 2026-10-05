using System.Globalization;
using System.Text.Json;
namespace MorningBrew.Admin.Services;
public static class RowDisplay
{
    private static readonly Dictionary<string,string> Names=new()
    {
        ["CustomerID"]="Mã khách",["UserID"]="Mã tài khoản",["FullName"]="Họ tên",["Phone"]="Điện thoại",
        ["DateOfBirth"]="Ngày sinh",["Gender"]="Giới tính",["LoyaltyPoints"]="Điểm",["IsActive"]="Hoạt động",
        ["FeedbackID"]="Mã phản hồi",["BranchID"]="Mã chi nhánh",["Type"]="Loại",["Content"]="Nội dung",
        ["Rating"]="Đánh giá",["Status"]="Trạng thái",["ResponseContent"]="Trả lời",["CreatedAt"]="Ngày tạo",
        ["SurveyID"]="Mã khảo sát",["Title"]="Tiêu đề",["Scope"]="Phạm vi",["AreaID"]="Mã khu vực",
        ["StartsAt"]="Bắt đầu",["EndsAt"]="Kết thúc",["Invited"]="Đã mời",["Completed"]="Đã trả lời",
        ["PromotionID"]="Mã ưu đãi",["Name"]="Tên",["Description"]="Mô tả",["DiscountType"]="Kiểu giảm",
        ["DiscountValue"]="Mức giảm",["PointsCost"]="Điểm đổi",["ProductID"]="Mã sản phẩm",
        ["Category"]="Danh mục",["VariantID"]="Mã biến thể",["Size"]="Kích cỡ",["BasePrice"]="Giá cơ bản",
        ["Role"]="Vai trò",["Permission"]="Quyền",["Branch"]="Chi nhánh",["ShiftID"]="Mã ca",
        ["Hours"]="Số giờ",["IsCancelled"]="Đã hủy",["SupplierID"]="Mã nhà cung cấp",["Code"]="Mã",
        ["ProductGroup"]="Nhóm sản phẩm",["CompletedOrders"]="Đơn hoàn tất",["GrossRevenue"]="Doanh thu",
        ["RefundedRevenue"]="Đã hoàn tiền",["NetRevenue"]="Doanh thu thuần",["QuestionID"]="Mã câu hỏi",
        ["Text"]="Câu hỏi",["TextValue"]="Trả lời",["RatingValue"]="Điểm đánh giá",["SelectedOptions"]="Lựa chọn",
        ["LoyaltyTransactionID"]="Mã giao dịch điểm",["Kind"]="Loại giao dịch",["Points"]="Số điểm",
        ["Note"]="Ghi chú",["CustomerPromotionID"]="Mã voucher",["PointsSpent"]="Điểm đã dùng",
        ["ExpiresAt"]="Hết hạn",["CompletedAt"]="Hoàn thành",["MaxDiscount"]="Giảm tối đa",["MinSubtotal"]="Đơn tối thiểu"
    };
    private static readonly Dictionary<string,string> Values=new()
    {
        ["New"]="Mới",["Processing"]="Đang xử lý",["Resolved"]="Đã giải quyết",["Draft"]="Bản nháp",
        ["Open"]="Đang mở",["Closed"]="Đã đóng",["Chain"]="Toàn chuỗi",["Area"]="Khu vực",["Branch"]="Chi nhánh",
        ["Owner"]="Chủ chuỗi",["AreaManager"]="Quản lý khu vực",["BranchManager"]="Quản lý chi nhánh",
        ["Staff"]="Nhân viên",["Customer"]="Khách hàng",["Male"]="Nam",["Female"]="Nữ",["Other"]="Khác",
        ["Undisclosed"]="Chưa cung cấp",["Available"]="Có thể dùng",["Reserved"]="Đang giữ cho đơn",
        ["Used"]="Đã dùng",["Expired"]="Hết hạn",["Earn"]="Tích điểm",["Redeem"]="Đổi điểm",["Reverse"]="Hoàn tác điểm",["Adjust"]="Điều chỉnh"
    };
    public static string Label(string key)=>Names.GetValueOrDefault(key,key);
    public static string Value(string key,JsonElement value)
    {
        if(value.ValueKind==JsonValueKind.Null)return "—";
        if(value.ValueKind is JsonValueKind.True or JsonValueKind.False)return value.GetBoolean()?"Có":"Không";
        var text=value.ToString();
        if(key=="DateOfBirth"&&DateTime.TryParse(text,CultureInfo.InvariantCulture,DateTimeStyles.None,out var birthday))
            return birthday.ToString("dd/MM/yyyy");
        if(key.EndsWith("At")&&DateTime.TryParse(text,CultureInfo.InvariantCulture,DateTimeStyles.None,out var time))
            return DateTime.SpecifyKind(time,DateTimeKind.Utc).ToLocalTime().ToString("dd/MM/yyyy HH:mm");
        if(key is "Status" or "Scope" or "Role" or "Gender" or "Kind")return Values.GetValueOrDefault(text,text);
        return text;
    }
}
