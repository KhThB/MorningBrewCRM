using System.Text.Json;
using System.Text.Json.Serialization;

namespace MorningBrew.Pos.Services;

/// <summary>
/// Quy ước JSON giống MorningBrew.Api (Shared/ApiJson.cs): snake_case và một số tên đặc biệt.
/// API từ chối trường lạ trong request, nên tên thuộc tính gửi đi phải khớp chính xác.
/// </summary>
public sealed class ApiNamingPolicy : JsonNamingPolicy
{
    public override string ConvertName(string name) => name switch
    {
        "Name" or "name" => "display_name",
        "ResourceName" => "name",
        "CreatedAt" or "createdAt" => "create_time",
        "UpdatedAt" or "updatedAt" => "update_time",
        "StartsAt" or "startsAt" => "start_time",
        "EndsAt" or "endsAt" => "end_time",
        "ExpiresAt" or "expiresAt" => "expire_time",
        "CompletedAt" or "completedAt" => "complete_time",
        _ => SnakeCaseLower.ConvertName(name)
    };
}

public static class ApiJson
{
    /// <summary>Dùng cho body gửi lên API.</summary>
    public static readonly JsonSerializerOptions Request = new(JsonSerializerDefaults.Web)
    {
        PropertyNamingPolicy = new ApiNamingPolicy(),
        DictionaryKeyPolicy = null
    };

    /// <summary>Dùng khi đọc kết quả: bỏ qua trường app chưa cần.</summary>
    public static readonly JsonSerializerOptions Response = new(JsonSerializerDefaults.Web)
    {
        PropertyNamingPolicy = new ApiNamingPolicy(),
        PropertyNameCaseInsensitive = false,
        UnmappedMemberHandling = JsonUnmappedMemberHandling.Skip,
        NumberHandling = JsonNumberHandling.AllowReadingFromString
    };
}
