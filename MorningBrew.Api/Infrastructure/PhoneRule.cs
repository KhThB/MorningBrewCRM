using System.Text.RegularExpressions;
namespace MorningBrew.Api.Infrastructure;

// [BUG-04] Một quy tắc số điện thoại dùng chung cho đăng ký, sửa hồ sơ, tạo tài khoản ở Admin và đơn hàng.
// Bỏ khoảng trắng, dấu chấm, gạch nối rồi kiểm tra 10–11 chữ số, bắt đầu bằng 0. Chuỗi rỗng = không có số.
public static partial class PhoneRule
{
    [GeneratedRegex(@"^0\d{9,10}$")] private static partial Regex Valid();
    [GeneratedRegex(@"[\s.\-]")] private static partial Regex Separators();

    public static string? Normalize(string? phone, string field = "phone")
    {
        if (string.IsNullOrWhiteSpace(phone)) return null;
        var digits = Separators().Replace(phone, "");
        if (!Valid().IsMatch(digits))
            throw new ApiException(400, "Số điện thoại gồm 10–11 số, bắt đầu bằng 0.", [new("FIELD_INVALID", field)]);
        return digits;
    }
}
