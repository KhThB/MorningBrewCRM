using System.Security.Cryptography;
using System.Text.Json;
using Microsoft.AspNetCore.DataProtection;
using Microsoft.AspNetCore.Mvc;

namespace MorningBrew.Api.Infrastructure;

public sealed class PageRequest
{
    [FromQuery(Name = "pageSize")] public int PageSize { get; set; } = 50;
    [FromQuery(Name = "pageToken")] public string? PageToken { get; set; }
}
public record ApiPage(List<Dictionary<string, object?>> Items, string? NextPageToken);

// Only server-owned column names/SQL are accepted. Tokens are encrypted, authenticated,
// short-lived and bound to actor, collection and filters. Authorization stays in each SQL query.
public sealed class PageReader(SqlStore db, IDataProtectionProvider provider)
{
    private readonly IDataProtector protector = provider.CreateProtector("MorningBrew.Api.Pages.v1");
    private record Cursor(string Scope, DateTime Expires, JsonElement[] Keys);

    public async Task<ApiPage> Read(string sql, string[] keys, bool descending, PageRequest page,
        string scope, (string, object?)[] parameters, string? collection = null, string? id = null)
    {
        if (page.PageSize is < 1 or > 100)
            throw new ApiException(400, "pageSize phải từ 1 đến 100.", [new("OUT_OF_RANGE", "pageSize")]);
        var values = parameters.ToList();
        string predicate = "1=1";
        if (!string.IsNullOrEmpty(page.PageToken))
        {
            try
            {
                if (page.PageToken.Length > 4096) throw new FormatException();
                var cursor = JsonSerializer.Deserialize<Cursor>(protector.Unprotect(page.PageToken))!;
                if (cursor.Scope != scope || cursor.Expires < DateTime.UtcNow || cursor.Keys.Length != keys.Length)
                    throw new FormatException();
                for (var i = 0; i < keys.Length; i++)
                    values.Add(($"@cursor{i}", cursor.Keys[i].ValueKind == JsonValueKind.Number
                        ? cursor.Keys[i].GetInt64() : cursor.Keys[i].GetString()));
                var op = descending ? "<" : ">";
                predicate = string.Join(" OR ", keys.Select((key, i) => "(" +
                    string.Join(" AND ", keys.Take(i).Select((previous, j) => $"[{previous}]=@cursor{j}")
                        .Append($"[{key}]{op}@cursor{i}")) + ")"));
            }
            catch (Exception ex) when (ex is CryptographicException or FormatException or JsonException or InvalidOperationException or ArgumentException)
            { throw new ApiException(400, "pageToken không hợp lệ, đã hết hạn hoặc không khớp bộ lọc.", [new("INVALID_PAGE_TOKEN", "pageToken")]); }
        }
        values.Add(("@take", page.PageSize + 1));
        var order = string.Join(",", keys.Select(x => $"[{x}] {(descending ? "DESC" : "ASC")}"));
        var rows = await db.Query($"WITH page_source AS ({sql}) SELECT TOP(@take) * FROM page_source WHERE {predicate} ORDER BY {order}", values.ToArray());
        string? next = null;
        if (rows.Count > page.PageSize)
        {
            rows.RemoveAt(rows.Count - 1);
            var cursor = new Cursor(scope, DateTime.UtcNow.AddHours(2), keys.Select(x => JsonSerializer.SerializeToElement(rows[^1][x])).ToArray());
            next = protector.Protect(JsonSerializer.Serialize(cursor));
        }
        if (collection is not null && id is not null)
            foreach (var row in rows) ApiContract.Resource(row, collection, id);
        return new(rows, next);
    }
}
