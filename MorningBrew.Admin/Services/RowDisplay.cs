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
        ["file_name"]="Tên file",["backup_type"]="Loại bản sao lưu",["in_stock"]="Tình trạng",["size_mb"]="Dung lượng (MB)",["audit_id"]="Mã nhật ký",["actor"]="Người thực hiện",["action"]="Thao tác",["entity_type"]="Đối tượng",["entity_id"]="Mã đối tượng",["details"]="Chi tiết",["email"]="Email",
        // [KHO] kho nguyên liệu
        ["ingredient_id"]="Mã nguyên liệu",["unit"]="Đơn vị",["quantity"]="Tồn kho",["min_stock"]="Tồn tối thiểu",["is_low"]="Cảnh báo",
        ["supplier_name"]="Nhà cung cấp",["last_unit_cost"]="Giá nhập gần nhất",["stock_updated_at"]="Cập nhật tồn",
        ["transaction_id"]="Mã phiếu",["quantity_change"]="Số lượng",["balance_after"]="Tồn sau phiếu",["unit_cost"]="Đơn giá",["total_cost"]="Thành tiền"
    };
    private static readonly Dictionary<string,string> Values=new()
    {
        ["New"]="Mới",["Processing"]="Đang xử lý",["Resolved"]="Đã giải quyết",["Draft"]="Bản nháp",
        ["Open"]="Đang mở",["Closed"]="Đã đóng",["Chain"]="Toàn chuỗi",["Area"]="Khu vực",["Branch"]="Chi nhánh",
        ["Owner"]="Chủ cửa hàng",["BranchManager"]="Quản lý cửa hàng",
        ["Staff"]="Thu ngân",["Customer"]="Khách hàng",["Male"]="Nam",["Female"]="Nữ",["Other"]="Khác",
        ["Undisclosed"]="Chưa cung cấp",["Available"]="Có thể dùng",["Reserved"]="Đang giữ cho đơn",
        ["Used"]="Đã dùng",["Expired"]="Hết hạn",["Rating"]="Chấm điểm",["SingleChoice"]="Chọn một",["MultipleChoice"]="Chọn nhiều",["Text"]="Tự luận",
        ["Service"]="Dịch vụ",["Product"]="Sản phẩm",["Space"]="Không gian",["Earn"]="Tích điểm",["Redeem"]="Đổi điểm",["Reverse"]="Hoàn tác điểm",["Adjust"]="Điều chỉnh",
        ["Import"]="Nhập kho",["Export"]="Xuất dùng",["Waste"]="Hủy / hư hỏng" // [KHO]
    };
    // [AUD-01] Nhật ký thao tác: ghi rõ mỗi mã thao tác làm gì (trước đây chỉ hiện mã kỹ thuật như "Account.Role").
    // Mã gốc giữ trong ngoặc để vẫn tìm được bằng ô tìm kiếm của trang Nhật ký hệ thống.
    private static readonly Dictionary<string,string> Actions=new()
    {
        ["Account.Create"]="Tạo tài khoản nội bộ mới",
        ["Account.Role"]="Đổi vai trò của tài khoản",
        ["Account.Status"]="Khóa / mở khóa tài khoản",
        ["Account.ResetPassword"]="Đặt lại mật khẩu cho tài khoản (đăng xuất mọi thiết bị)",
        ["Account.ChangePassword"]="Người dùng tự đổi mật khẩu",
        ["Account.SoftDelete"]="Xóa tài khoản (ẩn danh dữ liệu cá nhân, giữ lịch sử đơn)",
        ["Customer.Register"]="Khách tự đăng ký tài khoản trên app",
        ["Customer.Status"]="Khóa / mở khóa tài khoản khách hàng",
        ["Permission.Change"]="Cấp / thu hồi một quyền của vai trò",
        ["Product.Create"]="Thêm sản phẩm mới vào thực đơn",
        ["Product.Image"]="Cập nhật ảnh sản phẩm",
        ["Product.Availability"]="Đánh dấu sản phẩm còn hàng / tạm hết",
        ["Variant.Price"]="Đổi giá bán của một size (áp dụng cho đơn mới)",
        ["Menu.Price"]="Đổi giá bán tại cửa hàng",
        ["Order.Create"]="Tạo đơn hàng mới (app hoặc POS)",
        ["Order.Advance"]="Chuyển đơn sang trạng thái tiếp theo",
        ["Order.Cancel"]="Hủy đơn hàng",
        ["Payment.Simulate"]="Ghi nhận kết quả thanh toán (mô phỏng / thu tiền mặt)",
        ["Refund.Simulate"]="Ghi nhận kết quả hoàn tiền",
        ["Delivery.Update"]="Cập nhật giao hàng (người giao, giao thất bại / giao lại)",
        ["Loyalty.Adjust"]="Điều chỉnh điểm thành viên bằng tay",
        ["Promotion.Create"]="Tạo chương trình ưu đãi",
        ["Voucher.Issue"]="Phát voucher cho khách (tặng hoặc đổi điểm)",
        ["Feedback.Handle"]="Trả lời / đổi trạng thái phản hồi của khách",
        ["Survey.Create"]="Tạo khảo sát (bản nháp)",
        ["Survey.Update"]="Sửa khảo sát bản nháp",
        ["Survey.Delete"]="Xóa khảo sát bản nháp",
        ["Survey.Publish"]="Phát hành khảo sát cho khách",
        ["Survey.InviteBulk"]="Mời khách làm khảo sát theo nhóm",
        ["Survey.Close"]="Đóng khảo sát",
        ["Inventory.Transaction"]="Lập phiếu kho (nhập / xuất / hủy / kiểm kê), cập nhật tồn",
        ["Database.Backup"]="Sao lưu cơ sở dữ liệu (tạo file .bak)",
        ["Database.Restore"]="Phục hồi cơ sở dữ liệu từ file .bak",
        ["Database.BackupDelete"]="Xóa một file sao lưu"
    };
    // [AUD-01] Bảng bị tác động, ghi tiếng Việt.
    private static readonly Dictionary<string,string> Entities=new()
    {
        ["Users"]="Tài khoản",["Customers"]="Khách hàng",["RolePermissions"]="Phân quyền",["Products"]="Sản phẩm",
        ["ProductVariants"]="Size / giá",["Orders"]="Đơn hàng",["PaymentAttempts"]="Thanh toán",["Refunds"]="Hoàn tiền",
        ["Deliveries"]="Giao hàng",["Promotions"]="Chương trình ưu đãi",["CustomerPromotions"]="Voucher",["Feedbacks"]="Phản hồi",
        ["Surveys"]="Khảo sát",["Ingredients"]="Nguyên liệu",["Database"]="Cơ sở dữ liệu"
    };
    // [AUD-01] Giá trị thường gặp trong cột Chi tiết của nhật ký.
    private static readonly Dictionary<string,string> DetailWords=new()
    {
        ["True"]="cho phép",["False"]="thu hồi",["InStock"]="Còn hàng",["OutOfStock"]="Tạm hết",["Succeeded"]="Thành công",["Failed"]="Thất bại",
        ["Pending"]="Chờ xử lý",["Confirmed"]="Đã xác nhận",["Preparing"]="Đang pha chế",["Ready"]="Sẵn sàng",["Delivering"]="Đang giao",
        ["Completed"]="Hoàn tất",["Cancelled"]="Đã hủy",["Delivered"]="Đã giao",["Assigned"]="Đã có người giao"
    };
    private static string Word(string w)=>Values.TryGetValue(w,out var v)?v:DetailWords.GetValueOrDefault(w,w);
    public static string Action(string code)=>Actions.TryGetValue(code,out var text)?$"{text} ({code})":code; // [AUD-01]
    public static string Detail(string text) // [AUD-01]
    {
        var role=System.Text.RegularExpressions.Regex.Match(text,@"^(\w+)->(\w+)$");
        if(role.Success)return $"{Word(role.Groups[1].Value)} → {Word(role.Groups[2].Value)}";
        var perm=System.Text.RegularExpressions.Regex.Match(text,@"^(\w+):([\w.]+):(True|False)$");
        if(perm.Success)return $"{Word(perm.Groups[1].Value)}: {Word(perm.Groups[3].Value)} quyền {perm.Groups[2].Value}";
        var status=System.Text.RegularExpressions.Regex.Match(text,@"^(\w+): (.*)$");
        if(status.Success&&DetailWords.ContainsKey(status.Groups[1].Value))return $"{Word(status.Groups[1].Value)}: {status.Groups[2].Value}";
        return Word(text);
    }
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
        if(key=="action")return Action(text);                         // [AUD-01]
        if(key=="entity_type")return Entities.GetValueOrDefault(text,text); // [AUD-01]
        if(key=="details")return Detail(text);                         // [AUD-01]
        return text;
    }
}
