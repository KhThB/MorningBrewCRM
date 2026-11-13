using System.Text.Json;
using System.Text.Json.Serialization;

namespace MorningBrew.Contracts;

// One wire convention shared by the API and its server-side web client.
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
    public static readonly JsonSerializerOptions Options = Create();
    public static void Configure(JsonSerializerOptions options)
    {
        options.PropertyNamingPolicy = new ApiNamingPolicy();
        options.DictionaryKeyPolicy = new ApiNamingPolicy();
        options.PropertyNameCaseInsensitive = false;
        options.UnmappedMemberHandling = JsonUnmappedMemberHandling.Disallow;
        options.Converters.Add(new UtcDateTimeConverter());
        options.Converters.Add(new UtcDateTimeOffsetConverter());
    }
    private static JsonSerializerOptions Create()
    {
        var options = new JsonSerializerOptions(JsonSerializerDefaults.Web);
        Configure(options);
        return options;
    }
}

public sealed class UtcDateTimeConverter : JsonConverter<DateTime>
{
    public override DateTime Read(ref Utf8JsonReader reader, Type type, JsonSerializerOptions options)
    {
        if (reader.TokenType != JsonTokenType.String) throw new JsonException("Timestamp must be a string.");
        var text = reader.GetString() ?? throw new JsonException("Timestamp is required.");
        if (!text.EndsWith('Z') && !System.Text.RegularExpressions.Regex.IsMatch(text, @"[+-]\d{2}:\d{2}$"))
            throw new JsonException("Timestamp must include Z or a UTC offset.");
        if (!DateTimeOffset.TryParse(text, System.Globalization.CultureInfo.InvariantCulture,
            System.Globalization.DateTimeStyles.None, out var date)) throw new JsonException("Invalid timestamp.");
        return date.UtcDateTime;
    }
    public override void Write(Utf8JsonWriter writer, DateTime value, JsonSerializerOptions options)
        => writer.WriteStringValue(value.Kind == DateTimeKind.Unspecified
            ? DateTime.SpecifyKind(value, DateTimeKind.Utc) : value.ToUniversalTime());
}

public sealed class UtcDateTimeOffsetConverter : JsonConverter<DateTimeOffset>
{
    public override DateTimeOffset Read(ref Utf8JsonReader reader, Type type, JsonSerializerOptions options)
        => new(new UtcDateTimeConverter().Read(ref reader, typeof(DateTime), options));
    public override void Write(Utf8JsonWriter writer, DateTimeOffset value, JsonSerializerOptions options)
        => writer.WriteStringValue(value.UtcDateTime);
}
