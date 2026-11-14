using System.Net;
using System.Net.Http.Headers;
using System.Net.Http.Json;
using System.Text.Json;

namespace MauiApp1.Services;

public sealed class ApiException(int status, string message) : Exception(message)
{
    public int Status { get; } = status; // [CLEAN-02] bỏ IsMissingEndpoint (chỉ dùng cho dữ liệu mẫu)
}

public static class ApiSettings
{
    // API chạy profile "http" của MorningBrew.Api: http://localhost:5029
    // Android emulator truy cập máy tính qua 10.0.2.2.
#if ANDROID
    public const string BaseUrl = "http://10.0.2.2:5029/";
#else
    public const string BaseUrl = "http://localhost:5029/";
#endif
}

/// <summary>Gọi MorningBrew.Api, gắn Bearer token và đổi lỗi API thành thông báo tiếng Việt.</summary>
public sealed class ApiClient(HttpClient http)
{
    public string? Token { get; set; }

    /// <summary>Phiên bị thu hồi (tài khoản bị khóa, đăng xuất nơi khác, hết hạn).</summary>
    public event Action? Unauthorized;

    public Task<T> Get<T>(string path) => Send<T>(HttpMethod.Get, path);

    public Task<ApiPage<T>> Page<T>(string path, int pageSize = 50) =>
        Get<ApiPage<T>>($"{path}{(path.Contains('?') ? '&' : '?')}pageSize={pageSize}");

    /// <summary>[BUG-15] Đọc lần lượt mọi trang (theo next_page_token) thay vì chỉ trang đầu 50 dòng; giới hạn maxItems để tránh tải vô hạn.</summary>
    public async Task<List<T>> All<T>(string path, int pageSize = 100, int maxItems = 1000)
    {
        var items = new List<T>();
        string? token = null;
        do
        {
            var url = $"{path}{(path.Contains('?') ? '&' : '?')}pageSize={pageSize}"
                      + (token is null ? "" : $"&pageToken={Uri.EscapeDataString(token)}");
            var page = await Get<ApiPage<T>>(url);
            items.AddRange(page.Items);
            token = string.IsNullOrEmpty(page.NextPageToken) ? null : page.NextPageToken;
        } while (token is not null && items.Count < maxItems);
        return items;
    }

    public async Task<T> Send<T>(HttpMethod method, string path, object? body = null, string? ifMatch = null)
    {
        using var request = new HttpRequestMessage(method, path);
        if (Token is not null) request.Headers.Authorization = new AuthenticationHeaderValue("Bearer", Token);
        if (ifMatch is not null) request.Headers.TryAddWithoutValidation("If-Match", ifMatch);
        if (body is not null) request.Content = JsonContent.Create(body, body.GetType(), options: ApiJson.Request);

        HttpResponseMessage response;
        try { response = await http.SendAsync(request); }
        catch (HttpRequestException)
        { throw new ApiException(503, "Không kết nối được máy chủ. Hãy chạy MorningBrew.Api (http://localhost:5029)."); }
        catch (TaskCanceledException)
        { throw new ApiException(503, "Máy chủ phản hồi quá chậm. Vui lòng thử lại."); }

        using (response)
        {
            if (!response.IsSuccessStatusCode)
            {
                var error = new ApiException((int)response.StatusCode, await ReadError(response));
                if (error.Status == 401 && Token is not null && !path.StartsWith("v1/auth/")) Unauthorized?.Invoke();
                throw error;
            }
            if (response.StatusCode == HttpStatusCode.NoContent || typeof(T) == typeof(object))
                return default!;
            var text = await response.Content.ReadAsStringAsync();
            if (string.IsNullOrWhiteSpace(text)) return default!;
            return JsonSerializer.Deserialize<T>(text, ApiJson.Response)!;
        }
    }

    public Task Send(HttpMethod method, string path, object? body = null, string? ifMatch = null) =>
        Send<object>(method, path, body, ifMatch);

    private static async Task<string> ReadError(HttpResponseMessage response)
    {
        string? message = null;
        try
        {
            using var json = JsonDocument.Parse(await response.Content.ReadAsStringAsync());
            if (json.RootElement.TryGetProperty("error", out var error) && error.TryGetProperty("message", out var value))
                message = value.GetString();
        }
        catch (JsonException) { }
        return Translate(message) ?? (int)response.StatusCode switch
        {
            401 => "Phiên đăng nhập đã hết hạn. Vui lòng đăng nhập lại.",
            403 => "Bạn không có quyền thực hiện thao tác này.",
            404 => "Không tìm thấy dữ liệu hoặc chức năng chưa có trên máy chủ.",
            409 => "Yêu cầu đã được xử lý trước đó.",
            412 => "Thông tin đã thay đổi ở nơi khác. Hãy tải lại rồi lưu.",
            429 => "Bạn thao tác quá nhanh, vui lòng thử lại sau ít phút.",
            _ => "Có lỗi xảy ra, vui lòng thử lại."
        };
    }

    // Một số thông điệp từ SQL còn tiếng Anh: dịch để khách dễ hiểu.
    private static string? Translate(string? message) => message switch
    {
        null => null,
        "Survey is not open." => "Khảo sát đã đóng hoặc hết hạn.",
        "Required question is missing." => "Bạn chưa trả lời hết các câu bắt buộc.",
        "Answer type or rating bounds are invalid." => "Câu trả lời chưa hợp lệ.",
        "Feedback cannot be blank." => "Nội dung phản hồi không được để trống.",
        "Product does not occur in this order." => "Sản phẩm không có trong đơn này.",
        _ => message
    };
}
