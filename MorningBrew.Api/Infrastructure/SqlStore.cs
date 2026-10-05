using System.Data;
using Microsoft.Data.SqlClient;
namespace MorningBrew.Api.Infrastructure;

// All SQL and procedure names are server-owned constants; values are always parameters.
public sealed class SqlStore(IConfiguration configuration)
{
    public Task<List<Dictionary<string, object?>>> Query(string sql, params (string, object?)[] values)
        => Run(sql, CommandType.Text, values);
    public Task<List<Dictionary<string, object?>>> Procedure(string name, params (string, object?)[] values)
        => Run(name, CommandType.StoredProcedure, values);
    public Task<List<List<Dictionary<string, object?>>>> ProcedureSets(string name, params (string, object?)[] values)
        => RunSets(name, CommandType.StoredProcedure, values);
    private async Task<List<Dictionary<string, object?>>> Run(string sql, CommandType type, (string, object?)[] values)
        => (await RunSets(sql, type, values)).FirstOrDefault() ?? [];
    private async Task<List<List<Dictionary<string, object?>>>> RunSets(string sql, CommandType type, (string, object?)[] values)
    {
        await using var connection = new SqlConnection(configuration.GetConnectionString("MorningBrew")
            ?? throw new InvalidOperationException("Configure ConnectionStrings:MorningBrew."));
        await connection.OpenAsync();
        await using var command = new SqlCommand(sql, connection) { CommandType = type, CommandTimeout = 30 };
        foreach (var (key, value) in values)
        {
            if (value is DateTime date) command.Parameters.Add(key, SqlDbType.DateTime2).Value = date;
            else command.Parameters.AddWithValue(key, value ?? DBNull.Value);
        }
        await using var reader = await command.ExecuteReaderAsync();
        var sets = new List<List<Dictionary<string, object?>>>();
        do
        {
            if (reader.FieldCount == 0) continue;
            var result = new List<Dictionary<string, object?>>();
            while (await reader.ReadAsync())
            {
                var row = new Dictionary<string, object?>(StringComparer.OrdinalIgnoreCase);
                for (var i = 0; i < reader.FieldCount; i++)
                    row[reader.GetName(i)] = reader.IsDBNull(i) ? null : reader.GetDataTypeName(i) == "date"
                        ? DateOnly.FromDateTime(reader.GetDateTime(i)) : reader.GetValue(i);
                result.Add(row);
            }
            sets.Add(result);
        } while (await reader.NextResultAsync());
        return sets;
    }
}
public sealed class ApiException(int status, string message, ErrorDetail[]? details = null) : Exception(message)
{
    public int Status { get; } = status;
    public ErrorDetail[] Details { get; } = details ?? [];
}
public sealed class ApiErrors(RequestDelegate next, ILogger<ApiErrors> logger)
{
    public async Task InvokeAsync(HttpContext context)
    {
        try { await next(context); }
        catch (Exception ex)
        {
            if (context.Response.HasStarted || context.RequestAborted.IsCancellationRequested) throw;
            var (status, message) = ex switch
            {
                ApiException e => (e.Status, e.Message),
                SqlException { Number: 51103 } => (403, "Thao tác nằm ngoài quyền hoặc phạm vi của bạn."),
                SqlException { Number: 51104 } => (404, "Không tìm thấy tài nguyên."),
                SqlException { Number: 51112 } => (412, "Dữ liệu đã thay đổi. Hãy tải lại trước khi lưu."),
                SqlException { Number: 51109 } => (409, "Yêu cầu xung đột với trạng thái hoặc yêu cầu trước đó."),
                BadHttpRequestException e => (e.StatusCode, "Yêu cầu HTTP không hợp lệ."),
                SqlException e when e.Number is 2601 or 2627 => (409, "Dữ liệu đã tồn tại hoặc yêu cầu đã được xử lý."),
                SqlException e when e.Number is 547 or 515 or 2628 => (400, "Dữ liệu không hợp lệ hoặc đang được sử dụng."),
                SqlException e when e.Number >= 51000 && e.Number < 52000 => (400, e.Message),
                SqlException => (503, "Không thể truy cập SQL Server. Kiểm tra cấu hình và database."),
                _ => (500, "Không thể xử lý yêu cầu. Vui lòng thử lại.")
            };
            if (status >= 500) logger.LogError(ex, "Request failed: {Path}", context.Request.Path);
            context.Response.StatusCode = status;
            var error = ApiContract.Error(status, message, (ex as ApiException)?.Details);
            if (ex is SqlException { Number: 51109 }) error = error with { Error = error.Error with { Status = "ABORTED" } };
            await context.Response.WriteAsJsonAsync(error);
        }
    }
}
