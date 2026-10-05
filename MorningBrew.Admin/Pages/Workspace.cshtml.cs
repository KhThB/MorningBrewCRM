using System.Globalization;
using System.Text.Json;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.RazorPages;
using MorningBrew.Admin.Services;
namespace MorningBrew.Admin.Pages;
public class WorkspaceModel(CrmApi api):PageModel
{
    public static readonly Dictionary<string,string> Titles=new() { ["customers"]="Khách hàng",["feedbacks"]="Phản hồi",["surveys"]="Khảo sát",["promotions"]="Ưu đãi",["products"]="Sản phẩm",["accounts"]="Tài khoản",["staff"]="Nhân sự",["shifts"]="Lịch làm việc",["suppliers"]="Nhà cung cấp",["permissions"]="Phân quyền theo vai trò",["reports"]="Báo cáo doanh thu" };
    [BindProperty(SupportsGet=true)] public string Section {get;set;}="customers";
    [BindProperty(SupportsGet=true)] public string? Q {get;set;}
    [BindProperty(SupportsGet=true)] public int Skip {get;set;}
    public string Title=>Titles.GetValueOrDefault(Section,"CRM");
    public List<Dictionary<string,JsonElement>> Rows {get;set;}=[];
    public Dictionary<string,List<Dictionary<string,JsonElement>>> Options {get;set;}=[];
    public List<Dictionary<string,JsonElement>> Choices(string name)=>Options.GetValueOrDefault(name)??[];
    public async Task<IActionResult> OnGetAsync()
    {
        Section=Section.ToLowerInvariant();
        if(!Titles.ContainsKey(Section)) return NotFound();
        try
        {
            var path=Section=="reports"?"api/admin/reports/revenue":$"api/admin/{Section}?skip={Math.Max(0,Skip)}&q={Uri.EscapeDataString(Q??"")}";
            Rows=await api.Get<List<Dictionary<string,JsonElement>>>(path);
            Options=await api.Get<Dictionary<string,List<Dictionary<string,JsonElement>>>>("api/admin/lookups/options");
        }
        catch(CrmApiException e) { TempData["Error"]=e.Message; }
        return Page();
    }
    public async Task<IActionResult> OnPostAsync(string operation)
    {
        if(!Titles.ContainsKey(Section)) return NotFound();
        string Text(string key)=>Request.Form[key].ToString().Trim();
        int Number(string key)=>int.Parse(Text(key),CultureInfo.InvariantCulture);
        int? Optional(string key)=>string.IsNullOrWhiteSpace(Text(key))?null:Number(key);
        decimal Money(string key)=>decimal.Parse(Text(key),CultureInfo.InvariantCulture);
        DateTime Date(string key)=>DateTimeOffset.Parse(Text(key),CultureInfo.InvariantCulture).UtcDateTime;
        Guid Key()=>Guid.Parse(Text("requestKey"));
        try
        {
            if(!ModelState.IsValid) throw new FormatException();
            switch(operation)
            {
                case "supplier":
                    await api.Send(HttpMethod.Post,"api/admin/suppliers",new {code=Text("code"),name=Text("name"),phone=Text("phone"),email=string.IsNullOrEmpty(Text("email"))?null:Text("email"),productGroup=Text("productGroup")});break;
                case "supplier-active":
                    await api.Send(HttpMethod.Put,$"api/admin/suppliers/{Number("id")}/active",new {isActive=Text("active")=="true"});break;
                case "shift":
                    await api.Send(HttpMethod.Post,"api/admin/shifts",new {userId=Number("userId"),branchId=Number("branchId"),startsAt=Date("startsAt"),endsAt=Date("endsAt")});break;
                case "shift-cancel":
                    await api.Send(HttpMethod.Post,$"api/admin/shifts/{Number("id")}/cancel");break;
                case "permission":
                    await api.Send(HttpMethod.Put,"api/admin/permissions",new {role=Text("role"),code=Text("code"),allowed=Text("allowed")=="true"});break;
                case "account":
                    await api.Send(HttpMethod.Post,"api/admin/accounts",new {email=Text("email"),fullName=Text("fullName"),password=Text("password"),phone=Text("phone"),role=Text("role"),branchId=Optional("branchId"),areaId=Optional("areaId")});break;
                case "account-active":
                    await api.Send(HttpMethod.Put,$"api/admin/accounts/{Number("id")}/active",new {isActive=Text("active")=="true"});break;
                case "feedback":
                    await api.Send(HttpMethod.Post,$"api/admin/feedbacks/{Number("id")}/reply",new {status=Text("status"),responseContent=Text("response")});break;
                case "survey":
                    var questions=Text("questions").Split('\n',StringSplitOptions.RemoveEmptyEntries|StringSplitOptions.TrimEntries)
                        .Select(q=>new {text=q,type="Rating",isRequired=true,ratingMin=1,ratingMax=5}).ToArray();
                    await api.Send(HttpMethod.Post,"api/admin/surveys",new {title=Text("title"),description=Text("description"),scope=Text("scope"),areaId=Optional("areaId"),branchId=Optional("branchId"),startsAt=Date("startsAt"),endsAt=Date("endsAt"),questions});break;
                case "publish":
                case "close":
                    await api.Send(HttpMethod.Post,$"api/admin/surveys/{Number("id")}/{operation}");break;
                case "invite":
                    await api.Send(HttpMethod.Post,$"api/admin/surveys/{Number("id")}/invite",new {customerId=Number("customerId")});break;
                case "results":
                    Rows=await api.Get<List<Dictionary<string,JsonElement>>>($"api/admin/surveys/{Number("id")}/results");
                    Options=await api.Get<Dictionary<string,List<Dictionary<string,JsonElement>>>>("api/admin/lookups/options");
                    ViewData["Results"]=true;return Page();
                case "promotion":
                    await api.Send(HttpMethod.Post,"api/admin/promotions",new {name=Text("name"),description=Text("description"),scope=Text("scope"),areaId=Optional("areaId"),branchId=Optional("branchId"),discountType=Text("discountType"),discountValue=Money("discountValue"),maxDiscount=string.IsNullOrWhiteSpace(Text("maxDiscount"))?(decimal?)null:Money("maxDiscount"),minSubtotal=Money("minSubtotal"),startsAt=Date("startsAt"),endsAt=Date("endsAt"),pointsCost=Optional("pointsCost"),totalLimit=Number("totalLimit"),perCustomerLimit=Number("perCustomerLimit")});break;
                case "issue":
                    await api.Send(HttpMethod.Post,$"api/admin/promotions/{Number("id")}/issue",new {customerId=Number("customerId"),requestKey=Key()});break;
                case "promotion-active":
                    await api.Send(HttpMethod.Put,$"api/admin/promotions/{Number("id")}/active",new {isActive=Text("active")=="true"});break;
                case "points":
                    await api.Send(HttpMethod.Post,$"api/admin/customers/{Number("id")}/points",new {delta=Number("delta"),note=Text("note"),requestKey=Key()});break;
                case "product":
                    await api.Send(HttpMethod.Post,"api/admin/products",new {name=Text("name"),description=Text("description"),categoryId=Number("categoryId"),size=Text("size"),price=Money("price")});break;
                case "product-active":
                    await api.Send(HttpMethod.Put,$"api/admin/products/{Number("id")}/active",new {isActive=Text("active")=="true"});break;
                case "price":
                    await api.Send(HttpMethod.Post,"api/admin/products/price",new {branchId=Number("branchId"),variantId=Number("variantId"),price=Money("price"),isAvailable=Text("available")=="true"});break;
                default: return BadRequest();
            }
            TempData["Success"]="Đã lưu thay đổi.";
        }
        catch(CrmApiException e) { TempData["Error"]=e.Message; }
        catch(Exception e) when(e is FormatException or OverflowException) { TempData["Error"]="Vui lòng nhập đầy đủ các trường và kiểm tra số, ngày tháng."; }
        return RedirectToPage(new {section=Section,q=Q,skip=Skip});
    }
}
