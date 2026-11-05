using System.Net.Http.Headers;
using MorningBrew.Contracts;
using System.Net.Http.Json;
using System.Text.Json;
using Microsoft.AspNetCore.Authentication;
namespace MorningBrew.Admin.Services;

public record IdentityInfo(int Id,string Email,string FullName,string Role);
public record LoginResponse(string Token,DateTime ExpiresAt,IdentityInfo User);
public sealed class CrmApiException(int status,string message):Exception(message)
{
    public int Status {get;}=status;
}
public record ApiPage(List<Dictionary<string,JsonElement>> Items,string? NextPageToken);
public sealed class CrmApi(HttpClient client,IHttpContextAccessor accessor)
{
    private static readonly JsonSerializerOptions ResponseOptions = new(ApiJson.Options)
        { UnmappedMemberHandling = System.Text.Json.Serialization.JsonUnmappedMemberHandling.Skip };
    public async Task<IdentityInfo> Me(string? token)=>await Request<IdentityInfo>(HttpMethod.Get,"v1/auth/me",null,token);
    public Task<LoginResponse> Login(string email,string password)=>Request<LoginResponse>(HttpMethod.Post,"v1/auth/login",new {username=email,password},null,false);
    public Task<T> Get<T>(string path)=>Request<T>(HttpMethod.Get,path);
    public async Task Revoke(string token)=>_ = await Request<JsonElement>(HttpMethod.Post,"v1/auth/logout",null,token,false);
    public async Task Send(HttpMethod method,string path,object? body=null,string? etag=null)=>_ = await Request<JsonElement>(method,path,body,etag:etag);
    private async Task<T> Request<T>(HttpMethod method,string path,object? body=null,string? token=null,bool useCookie=true,string? etag=null)
    {
        token ??= useCookie && accessor.HttpContext is not null ? await accessor.HttpContext.GetTokenAsync("access_token") : null;
        using var request=new HttpRequestMessage(method,path);
        if(token is not null) request.Headers.Authorization=new AuthenticationHeaderValue("Bearer",token);
        if(etag is not null) request.Headers.TryAddWithoutValidation("If-Match",etag);
        if(body is not null) request.Content=JsonContent.Create(body,options:ApiJson.Options);
        HttpResponseMessage response;
        try { response=await client.SendAsync(request); }
        catch(HttpRequestException) { throw new CrmApiException(503,"Không kết nối được API. Hãy chạy MorningBrew.Api và kiểm tra địa chỉ kết nối."); }
        catch(TaskCanceledException) { throw new CrmApiException(503,"API phản hồi quá chậm. Vui lòng thử lại."); }
        using(response)
        {
            if(!response.IsSuccessStatusCode)
            {
                var text=await response.Content.ReadAsStringAsync();
                string? message=null;
                try { using var json=JsonDocument.Parse(text); if(json.RootElement.TryGetProperty("error",out var error) && error.TryGetProperty("message",out var value)) message=value.GetString(); }
                catch(JsonException) { }
                throw new CrmApiException((int)response.StatusCode,message ?? (response.StatusCode==System.Net.HttpStatusCode.Forbidden ? "Bạn không có quyền thực hiện thao tác này." : "Yêu cầu không hợp lệ hoặc phiên đăng nhập đã hết hạn."));
            }
            if(response.StatusCode==System.Net.HttpStatusCode.NoContent) return default!;
            return (await response.Content.ReadFromJsonAsync<T>(ResponseOptions))!;
        }
    }
}
