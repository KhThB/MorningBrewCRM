using System.Globalization;
using System.Text.Json;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.RazorPages;
using MorningBrew.Admin.Services;
namespace MorningBrew.Admin.Pages;

// Nền chung cho các trang danh sách của giao diện Quản trị (/Admin) và Quản lý CRM (/Manager).
// Mỗi trang con chỉ khai báo tiêu đề, nguồn dữ liệu và các thao tác được phép gửi lên.
public abstract class WorkspaceBase(CrmApi api):PageModel
{
    protected CrmApi Api {get;}=api;
    public abstract string Title {get;}
    public virtual string Subtitle=>"";
    // Đường dẫn API danh sách; null nếu trang tự nạp dữ liệu (LoadAsync).
    protected virtual string? ListPath=>null;
    protected virtual bool Searchable=>false;
    protected abstract IReadOnlySet<string> Operations {get;}

    [BindProperty(SupportsGet=true)] public string? Q {get;set;}
    [BindProperty(SupportsGet=true)] public string? PageToken {get;set;}
    public string? NextPageToken {get;set;}
    public List<Dictionary<string,JsonElement>> Rows {get;set;}=[];
    public Dictionary<string,List<Dictionary<string,JsonElement>>> Options {get;set;}=[];
    public List<Dictionary<string,JsonElement>> Choices(string name)=>Options.GetValueOrDefault(name)??[];
    public bool IsOwner=>User.IsInRole("Owner");

    public async Task<IActionResult> OnGetAsync()
    {
        try
        {
            await LoadAsync();
            Options=await Api.Get<Dictionary<string,List<Dictionary<string,JsonElement>>>>("v1/lookups/options");
        }
        catch(CrmApiException e) { TempData["Error"]=e.Message; }
        return Page();
    }

    protected virtual async Task LoadAsync()
    {
        if(ListPath is null) return;
        var query=$"pageSize=50&pageToken={Uri.EscapeDataString(PageToken??"")}";
        if(Searchable) query+=$"&q={Uri.EscapeDataString(Q??"")}";
        var page=await Api.Get<ApiPage>($"{ListPath}?{query}");
        Rows=page.Items; NextPageToken=page.NextPageToken;
    }

    protected string Text(string key)=>Request.Form[key].ToString().Trim();
    protected int Number(string key)=>int.Parse(Text(key),CultureInfo.InvariantCulture);
    protected int? Optional(string key)=>string.IsNullOrWhiteSpace(Text(key))?null:Number(key);
    protected decimal Money(string key)=>decimal.Parse(Text(key),CultureInfo.InvariantCulture);
    protected DateTime Date(string key)=>DateTimeOffset.Parse(Text(key),CultureInfo.InvariantCulture).UtcDateTime;
    protected Guid Key()=>Guid.Parse(Text("requestKey"));

    public async Task<IActionResult> OnPostAsync(string operation)
    {
        if(!Operations.Contains(operation)) return BadRequest();
        try
        {
            var redirect=await ExecuteAsync(operation);
            if(redirect is not null) return redirect;
            TempData["Success"]??="Đã lưu thay đổi.";
        }
        catch(CrmApiException e) { TempData["Error"]=e.Message; }
        catch(Exception e) when(e is FormatException or OverflowException) { TempData["Error"]="Vui lòng nhập đầy đủ các trường và kiểm tra số, ngày tháng."; }
        return RedirectToPage(new {q=Q,pageToken=PageToken});
    }

    // Toàn bộ thao tác ghi của hai giao diện; trang nào được dùng thao tác nào do Operations quyết định.
    protected virtual async Task<IActionResult?> ExecuteAsync(string operation)
    {
        switch(operation)
        {
            // ----- Quản trị -----
            case "account":
                await Api.Send(HttpMethod.Post,"v1/accounts",new {email=Text("email"),fullName=Text("fullName"),password=Text("password"),phone=Text("phone"),role=Text("role")});
                TempData["Success"]="Đã tạo tài khoản."; break;
            case "account-role":
                await Api.Send(HttpMethod.Put,$"v1/accounts/{Number("id")}/role",new {role=Text("role")});
                TempData["Success"]="Đã đổi vai trò. Tài khoản này phải đăng nhập lại để nhận quyền mới."; break;
            case "account-active":
                await Api.Send(HttpMethod.Put,$"v1/accounts/{Number("id")}/active",new {isActive=Text("active")=="true"}); break;
            case "account-delete":
                await Api.Send(HttpMethod.Delete,$"v1/accounts/{Number("id")}");
                TempData["Success"]="Đã xóa tài khoản. Lịch sử giao dịch vẫn được giữ."; return RedirectToPage();
            case "permission":
                await Api.Send(HttpMethod.Put,$"v1/roles/{Uri.EscapeDataString(Text("role"))}/permissions/{Uri.EscapeDataString(Text("code"))}",new {allowed=Text("allowed")=="true"}); break;
            case "supplier":
                await Api.Send(HttpMethod.Post,"v1/suppliers",new {code=Text("code"),name=Text("name"),phone=Text("phone"),email=string.IsNullOrEmpty(Text("email"))?null:Text("email"),productGroup=Text("productGroup")}); break;
            case "supplier-active":
                await Api.Send(HttpMethod.Put,$"v1/suppliers/{Number("id")}/active",new {isActive=Text("active")=="true"}); break;
            case "product":
                await Api.Send(HttpMethod.Post,"v1/products",new {name=Text("name"),description=Text("description"),categoryId=Number("categoryId"),size=Text("size"),price=Money("price")}); break;
            case "product-active":
                await Api.Send(HttpMethod.Put,$"v1/products/{Number("id")}/active",new {isActive=Text("active")=="true"}); break;
            case "price":
                await Api.Send(HttpMethod.Put,$"v1/variants/{Number("variantId")}/price",new {price=Money("price")}); break;
            // ----- Quản lý CRM -----
            case "customer":
                await Api.Send(HttpMethod.Post,"v1/accounts",new {email=Text("email"),fullName=Text("fullName"),password=Text("password"),phone=Text("phone"),role="Customer"});
                TempData["Success"]="Đã thêm khách hàng."; break;
            case "customer-active":
                await Api.Send(HttpMethod.Put,$"v1/customers/{Number("id")}/active",new {isActive=Text("active")=="true"}); break;
            case "customer-delete":
                await Api.Send(HttpMethod.Delete,$"v1/customers/{Number("id")}");
                TempData["Success"]="Đã xóa khách hàng. Đơn hàng, phản hồi và khảo sát vẫn được giữ cho báo cáo."; return RedirectToPage();
            case "points":
                await Api.Send(HttpMethod.Post,$"v1/customers/{Number("id")}:adjustPoints",new {delta=Number("delta"),note=Text("note"),requestKey=Key()}); break;
            case "feedback":
                await Api.Send(HttpMethod.Post,$"v1/feedbacks/{Number("id")}:reply",new {status=Text("status"),responseContent=Text("response")}); break;
            case "survey":
                var questions=Text("questions").Split('\n',StringSplitOptions.RemoveEmptyEntries|StringSplitOptions.TrimEntries)
                    .Select(q=>new {text=q,type="Rating",isRequired=true,ratingMin=1,ratingMax=5}).ToArray();
                await Api.Send(HttpMethod.Post,"v1/surveys",new {title=Text("title"),description=Text("description"),startsAt=Date("startsAt"),endsAt=Date("endsAt"),questions}); break;
            case "publish":
            case "close":
                await Api.Send(HttpMethod.Post,$"v1/surveys/{Number("id")}:{operation}"); break;
            case "invite":
                await Api.Send(HttpMethod.Post,$"v1/surveys/{Number("id")}:invite",new {customerId=Number("customerId")}); break;
            case "results":
                return RedirectToPage(new {surveyId=Number("id")});
            case "promotion":
                await Api.Send(HttpMethod.Post,"v1/promotions",new {name=Text("name"),description=Text("description"),discountType=Text("discountType"),discountValue=Money("discountValue"),maxDiscount=string.IsNullOrWhiteSpace(Text("maxDiscount"))?(decimal?)null:Money("maxDiscount"),minSubtotal=Money("minSubtotal"),startsAt=Date("startsAt"),endsAt=Date("endsAt"),pointsCost=Optional("pointsCost"),totalLimit=Number("totalLimit"),perCustomerLimit=Number("perCustomerLimit")}); break;
            case "issue":
                await Api.Send(HttpMethod.Post,$"v1/promotions/{Number("id")}:issue",new {customerId=Number("customerId"),requestKey=Key()}); break;
            case "promotion-active":
                await Api.Send(HttpMethod.Put,$"v1/promotions/{Number("id")}/active",new {isActive=Text("active")=="true"}); break;
            default: throw new InvalidOperationException(operation);
        }
        return null;
    }
}
