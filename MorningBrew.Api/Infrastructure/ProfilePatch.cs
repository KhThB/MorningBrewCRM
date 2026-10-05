using System.Text.Json;

namespace MorningBrew.Api.Infrastructure;

// Keep field presence separate from explicit null. No arbitrary SQL column names come from input.
public sealed record ProfilePatch(bool HasFullName, string? FullName, bool HasPhone, string? Phone,
    bool HasBirth, DateTime? Birth, bool HasGender, string? Gender)
{
    public static ProfilePatch Parse(JsonElement input)
    {
        if (input.ValueKind != JsonValueKind.Object) throw new ApiException(400, "Cần gửi một JSON object.");
        var fields = new Dictionary<string, JsonElement>();
        foreach (var property in input.EnumerateObject())
            if (property.Name is not ("full_name" or "phone" or "date_of_birth" or "gender") || !fields.TryAdd(property.Name, property.Value))
                throw new ApiException(400, "Trường không được phép hoặc bị lặp.", [new("FIELD_NOT_WRITABLE", property.Name)]);
        if (fields.Count == 0) throw new ApiException(400, "Cần ít nhất một trường để cập nhật.");
        string? Text(string key, int max, bool required = false)
        {
            if (!fields.TryGetValue(key, out var value)) return null;
            if (value.ValueKind == JsonValueKind.Null && !required) return null;
            if (value.ValueKind != JsonValueKind.String || value.GetString()!.Length > max || (required && string.IsNullOrWhiteSpace(value.GetString())))
                throw new ApiException(400, "Giá trị trường không hợp lệ.", [new("FIELD_INVALID", key)]);
            return value.GetString();
        }
        var name = Text("full_name", 120, true);
        var phone = Text("phone", 20);
        var gender = Text("gender", 15);
        if (gender is not (null or "Male" or "Female" or "Other" or "Undisclosed"))
            throw new ApiException(400, "Giới tính không hợp lệ.", [new("FIELD_INVALID", "gender")]);
        var birthText = Text("date_of_birth", 10);
        DateTime? birth = null;
        if (birthText is not null)
        {
            if (!DateOnly.TryParseExact(birthText, "yyyy-MM-dd", System.Globalization.CultureInfo.InvariantCulture,
                System.Globalization.DateTimeStyles.None, out var day) || day > DateOnly.FromDateTime(DateTime.UtcNow))
                throw new ApiException(400, "Ngày sinh phải có dạng yyyy-MM-dd và không ở tương lai.", [new("FIELD_INVALID", "date_of_birth")]);
            birth = day.ToDateTime(TimeOnly.MinValue);
        }
        return new(fields.ContainsKey("full_name"), name?.Trim(), fields.ContainsKey("phone"), phone,
            fields.ContainsKey("date_of_birth"), birth, fields.ContainsKey("gender"), gender);
    }

    public static (byte[] User, byte[] Customer) Versions(string value)
    {
        if (string.IsNullOrEmpty(value)) throw new ApiException(428, "Cần gửi header If-Match từ hồ sơ đã đọc.");
        if (!System.Text.RegularExpressions.Regex.IsMatch(value, "^\"[A-F0-9]{16}\\.[A-F0-9]{16}\"$"))
            throw new ApiException(400, "If-Match không hợp lệ.", [new("INVALID_ETAG", "If-Match")]);
        var parts = value.Trim('"').Split('.');
        return (Convert.FromHexString(parts[0]), Convert.FromHexString(parts[1]));
    }
}
