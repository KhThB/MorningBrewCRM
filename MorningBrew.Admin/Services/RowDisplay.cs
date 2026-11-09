using System.Globalization;
using System.Text.Json;
namespace MorningBrew.Admin.Services;
public static class RowDisplay
{
    private static readonly Dictionary<string,string> Names=new()
    {
        ["customer_id"]="Mã khách",["user_id"]="Mã tài khoản",["full_name"]="Họ tên",["phone"]="Điện thoại",
        ["date_of_birth"]="Ngày sinh",["gender"]="Giới tính",["loyalty_points"]="Điểm",["is_active"]="Hoạt động",
        ["feedback_id"]="Mã phản hồi",["branch_id"]="Mã chi nhánh",["type"]="Loại",["content"]="Nội dung",
        ["rating"]="Đánh giá",["status"]="Trạng thái",["response_content"]="Trả lời",["create_time"]="Ngày tạo",
        ["survey_id"]="Mã khảo sát",["title"]="Tiêu đề",["scope"]="Phạm vi",["area_id"]="Mã khu vực",
        ["start_time"]="Bắt đầu",["end_time"]="Kết thúc",["invited"]="Đã mời",["completed"]="Đã trả lời",
        ["promotion_id"]="Mã ưu đãi",["display_name"]="Tên",["description"]="Mô tả",["discount_type"]="Kiểu giảm",
        ["discount_value"]="Mức giảm",["points_cost"]="Điểm đổi",["product_id"]="Mã sản phẩm",
        ["category"]="Danh mục",["variant_id"]="Mã biến thể",["size"]="Kích cỡ",["base_price"]="Giá cơ bản",
        ["role"]="Vai trò",["permission"]="Quyền",["branch"]="Chi nhánh",["shift_id"]="Mã ca",
        ["hours"]="Số giờ",["is_cancelled"]="Đã hủy",["supplier_id"]="Mã nhà cung cấp",["code"]="Mã",
        ["product_group"]="Nhóm sản phẩm",["completed_orders"]="Đơn hoàn tất",["gross_revenue"]="Doanh thu",
        ["refunded_revenue"]="Đã hoàn tiền",["net_revenue"]="Doanh thu thuần",["question_id"]="Mã câu hỏi",
        ["text"]="Câu hỏi",["text_value"]="Trả lời",["rating_value"]="Điểm đánh giá",["selected_options"]="Lựa chọn",
        ["loyalty_transaction_id"]="Mã giao dịch điểm",["kind"]="Loại giao dịch",["points"]="Số điểm",
        ["note"]="Ghi chú",["customer_promotion_id"]="Mã voucher",["points_spent"]="Điểm đã dùng",
        ["expire_time"]="Hết hạn",["complete_time"]="Hoàn thành",["max_discount"]="Giảm tối đa",["min_subtotal"]="Đơn tối thiểu",
        ["file_name"]="Tên file",["backup_type"]="Loại bản sao lưu",["in_stock"]="Tình trạng",["size_mb"]="Dung lượng (MB)",["audit_id"]="Mã nhật ký",["actor"]="Người thực hiện",["action"]="Thao tác",["entity_type"]="Đối tượng",["entity_id"]="Mã đối tượng",["details"]="Chi tiết",["email"]="Email"
    };
    private static readonly Dictionary<string,string> Values=new()
    {
        ["New"]="Mới",["Processing"]="Đang xử lý",["Resolved"]="Đã giải quyết",["Draft"]="Bản nháp",
        ["Open"]="Đang mở",["Closed"]="Đã đóng",["Chain"]="Toàn chuỗi",["Area"]="Khu vực",["Branch"]="Chi nhánh",
        ["Owner"]="Chủ cửa hàng",["BranchManager"]="Quản lý cửa hàng",
        ["Staff"]="Thu ngân",["Customer"]="Khách hàng",["Male"]="Nam",["Female"]="Nữ",["Other"]="Khác",
        ["Undisclosed"]="Chưa cung cấp",["Available"]="Có thể dùng",["Reserved"]="Đang giữ cho đơn",
        ["Used"]="Đã dùng",["Expired"]="Hết hạn",["Rating"]="Chấm điểm",["SingleChoice"]="Chọn một",["MultipleChoice"]="Chọn nhiều",["Text"]="Tự luận",
        ["Service"]="Dịch vụ",["Product"]="Sản phẩm",["Space"]="Không gian",["Earn"]="Tích điểm",["Redeem"]="Đổi điểm",["Reverse"]="Hoàn tác điểm",["Adjust"]="Điều chỉnh"
    };
    public static string Label(string key)=>Names.GetValueOrDefault(key,key);
    public static string Value(string key,JsonElement value)
    {
        if(value.ValueKind==JsonValueKind.Null)return "—";
        if(value.ValueKind is JsonValueKind.True or JsonValueKind.False)return value.GetBoolean()?"Có":"Không";
        var text=value.ToString();
        if(key=="date_of_birth"&&DateTime.TryParse(text,CultureInfo.InvariantCulture,DateTimeStyles.None,out var birthday))
            return birthday.ToString("dd/MM/yyyy");
        // API luôn trả thời gian UTC dạng ISO có hậu tố "Z". Đọc bằng DateTimeOffset (giữ đúng mốc UTC) rồi
        // đổi sang giờ của máy chạy web. Trước đây DateTime.TryParse đã tự đổi sang giờ máy, sau đó lại cộng
        // múi giờ thêm một lần nên lệch +7 giờ.
        if((key.EndsWith("_time")||key.EndsWith("_at"))&&DateTimeOffset.TryParse(text,CultureInfo.InvariantCulture,DateTimeStyles.AssumeUniversal,out var time))
            return time.ToLocalTime().ToString("dd/MM/yyyy HH:mm:ss");
        if(key is "status" or "scope" or "role" or "gender" or "kind" or "type")return Values.GetValueOrDefault(text,text);
        return text;
    }
}
