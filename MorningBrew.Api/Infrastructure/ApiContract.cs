using System.Security.Claims;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.Filters;
using MorningBrew.Contracts;

namespace MorningBrew.Api.Infrastructure;

public record ErrorDetail(string Reason, string? Field = null);
public record ErrorBody(int Code, string Message, string Status, ErrorDetail[] Details);
public record ErrorResponse(ErrorBody Error);

public static class ApiContract
{
    public static ErrorResponse Error(int code, string? message = null, ErrorDetail[]? details = null)
        => new(new(code, message ?? code switch
        {
            400 => "Dữ liệu yêu cầu không hợp lệ.",
            401 => "Bạn cần đăng nhập hoặc phiên đăng nhập đã hết hạn.",
            403 => "Bạn không có quyền thực hiện thao tác này.",
            404 => "Không tìm thấy tài nguyên.",
            405 => "Phương thức HTTP không được hỗ trợ.",
            412 => "Dữ liệu đã thay đổi. Hãy tải lại trước khi lưu.",
            415 => "Content-Type không được hỗ trợ. Sử dụng application/json.",
            428 => "Cần gửi If-Match từ lần đọc tài nguyên gần nhất.",
            429 => "Quá nhiều yêu cầu. Vui lòng thử lại sau.",
            _ => "Không thể xử lý yêu cầu."
        }, code switch
        {
            400 or 415 => "INVALID_ARGUMENT", 401 => "UNAUTHENTICATED", 403 => "PERMISSION_DENIED",
            404 => "NOT_FOUND", 405 => "METHOD_NOT_ALLOWED", 409 => "ALREADY_EXISTS",
            412 or 428 => "FAILED_PRECONDITION", 429 => "RESOURCE_EXHAUSTED",
            503 => "UNAVAILABLE", _ => "INTERNAL"
        }, details ?? []));

    public static BadRequestObjectResult Validation(ActionContext context)
        => new(Error(400, details: context.ModelState
            .Where(x => x.Value?.Errors.Count > 0)
            .Select(x => new ErrorDetail("FIELD_INVALID", new ApiNamingPolicy().ConvertName(x.Key)))
            .ToArray()));

    public static Dictionary<string, object?> Resource(Dictionary<string, object?> row, string collection, string key)
    {
        row["ResourceName"] = $"{collection}/{row[key]}";
        return row;
    }
}

// MVC can otherwise generate ProblemDetails for empty status results, unlike middleware errors.
public sealed class ApiStatusFilter : IAlwaysRunResultFilter
{
    public void OnResultExecuting(ResultExecutingContext context)
    {
        if (context.Result is ObjectResult { StatusCode: >= 400 } result && result.Value is not ErrorResponse)
            context.Result = new ObjectResult(ApiContract.Error(result.StatusCode.Value)) { StatusCode = result.StatusCode };
    }
    public void OnResultExecuted(ResultExecutedContext context) { }
}
