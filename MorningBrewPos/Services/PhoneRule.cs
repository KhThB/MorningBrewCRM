using System.Text.RegularExpressions;

namespace MorningBrew.Pos.Services;

/// <summary>[BUG-04] Cùng quy tắc với API (MorningBrew.Api/Infrastructure/PhoneRule.cs):
/// bỏ khoảng trắng/dấu chấm/gạch nối, rồi kiểm tra 10–11 số bắt đầu bằng 0.</summary>
public static class PhoneRule
{
    public const string Message = "Số điện thoại gồm 10–11 số, bắt đầu bằng 0.";
    public static string? Normalize(string? phone) =>
        string.IsNullOrWhiteSpace(phone) ? null : Regex.Replace(phone, @"[\s.\-]", "");
    public static bool IsValid(string? phone) =>
        Normalize(phone) is not { } p || Regex.IsMatch(p, @"^0\d{9,10}$");
}
