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
    // [BUG-12] Từ khóa tìm khách cho các ô chọn khách (Mời khảo sát, Cấp voucher); trống = 200 khách mới nhất.
    [BindProperty(SupportsGet=true)] public string? Cq {get;set;}
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
            Options=await Api.Get<Dictionary<string,List<Dictionary<string,JsonElement>>>>($"v1/lookups/options?customerQ={Uri.EscapeDataString(Cq??"")}"); // [BUG-12]
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
        catch(CrmApiException e) { TempData["Error"]=e.Message; SaveModalState(); }
        catch(Exception e) when(e is FormatException or OverflowException) { TempData["Error"]="Vui lòng nhập đầy đủ các trường và kiểm tra số, ngày tháng."; SaveModalState(); }
        return RedirectToPage(new {q=Q,pageToken=PageToken});
    }

    // [UI-01] Form gửi từ cửa sổ (modal) có trường ẩn "modal" = id cửa sổ. Khi API báo lỗi, ghi lại id và dữ liệu
    // đã nhập để trang mở lại đúng cửa sổ đó (ui.js đọc #modal-state). Không giữ mật khẩu, token chống giả mạo, requestKey.
    private void SaveModalState()
    {
        var modal=Request.HasFormContentType ? Request.Form["modal"].ToString() : "";
        if(string.IsNullOrEmpty(modal)) return;
        var fields=Request.Form.Where(x=>x.Key is not ("__RequestVerificationToken" or "requestKey" or "modal")
                && !x.Key.Contains("password",StringComparison.OrdinalIgnoreCase) && x.Key!="confirm")
            .ToDictionary(x=>x.Key,x=>x.Value.Count>1?(object)x.Value.ToArray():x.Value.ToString());
        TempData["ModalState"]=JsonSerializer.Serialize(new {id=modal,form=fields,error=TempData.Peek("Error")}); // error: hiện ngay trong modal vì toast nằm dưới lớp mờ
    }

    // Bộ tạo khảo sát: mỗi câu hỏi gồm nội dung, loại (Rating/SingleChoice/MultipleChoice/Text),
    // các lựa chọn (mỗi dòng một lựa chọn) và bắt buộc hay không. Dòng trống bị bỏ qua.
    private object[] SurveyQuestions()
    {
        var f=Request.Form;
        var texts=f["q_text"];var types=f["q_type"];var options=f["q_options"];var required=f["q_required"];
        var list=new List<object>();
        for(var i=0;i<texts.Count;i++)
        {
            var text=(texts[i]??"").Trim();
            if(text.Length==0) continue;
            var type=i<types.Count?types[i]??"Rating":"Rating";
            var isRequired=i<required.Count && required[i]=="true";
            var opts=(i<options.Count?options[i]??"":"").Split('\n',StringSplitOptions.RemoveEmptyEntries|StringSplitOptions.TrimEntries);
            list.Add(type switch
            {
                "Rating"=>new {text,type,isRequired,ratingMin=(int?)1,ratingMax=(int?)5,options=(string[]?)null},
                "SingleChoice" or "MultipleChoice"=>new {text,type,isRequired,ratingMin=(int?)null,ratingMax=(int?)null,options=(string[]?)opts},
                _=>new {text,type="Text",isRequired,ratingMin=(int?)null,ratingMax=(int?)null,options=(string[]?)null}
            });
        }
        if(list.Count==0) throw new FormatException();
        return list.ToArray();
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
            case "account-password": // [BUG-05] chủ cửa hàng đặt lại mật khẩu
                if(Text("password")!=Text("confirm")) { TempData["Error"]="Mật khẩu nhập lại không khớp."; return RedirectToPage(); }
                await Api.Send(HttpMethod.Post,$"v1/accounts/{Number("id")}:resetPassword",new {newPassword=Text("password")});
                TempData["Success"]="Đã đặt lại mật khẩu. Tài khoản đó bị đăng xuất khỏi mọi thiết bị."; break;
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
                var product=await Api.Post<JsonElement>("v1/products",new {name=Text("name"),description=Text("description"),categoryId=Number("categoryId"),size=Text("size"),price=Money("price")});
                // [UI2-IMG] Có chọn ảnh thì tải lên ngay sau khi tạo sản phẩm.
                if(Request.Form.Files.GetFile("image") is { Length: >0 } image && product.TryGetProperty("product_id",out var pid))
                {
                    try { await using var s=image.OpenReadStream(); await Api.Upload($"v1/products/{pid.GetInt32()}/image",s,image.FileName,image.ContentType); }
                    catch(CrmApiException e) { TempData["Error"]=$"Đã tạo sản phẩm nhưng chưa lưu được ảnh: {e.Message}"; return RedirectToPage(); }
                }
                TempData["Success"]="Đã tạo sản phẩm."; break;
            case "product-image": // [UI2-IMG] đổi ảnh sản phẩm có sẵn
                if(Request.Form.Files.GetFile("image") is not { Length: >0 } newImage) { TempData["Error"]="Vui lòng chọn file ảnh."; return RedirectToPage(); }
                await using(var s=newImage.OpenReadStream()) { await Api.Upload($"v1/products/{Number("id")}/image",s,newImage.FileName,newImage.ContentType); }
                TempData["Success"]="Đã cập nhật ảnh sản phẩm. App khách thấy ảnh mới sau khoảng 1 phút."; break;
            case "product-active":
                await Api.Send(HttpMethod.Put,$"v1/products/{Number("id")}/active",new {isActive=Text("active")=="true"}); break;
            case "product-stock": // [UI2-A4a] còn hàng / tạm hết (khác "Ngừng bán")
                await Api.Send(HttpMethod.Put,$"v1/products/{Number("id")}/availability",new {isAvailable=Text("available")=="true"});
                TempData["Success"]=Text("available")=="true"?"Đã chuyển sang Còn hàng.":"Đã chuyển sang Tạm hết – app hiện nhãn Tạm hết và không cho đặt món này."; break;
            case "price":
                await Api.Send(HttpMethod.Put,$"v1/variants/{Number("variantId")}/price",new {price=Money("price")}); break;
            // ----- Quản lý CRM -----
            case "customer":
                // [BUG-03] gửi thêm ngày sinh, giới tính (để trống = null)
                await Api.Send(HttpMethod.Post,"v1/accounts",new {email=Text("email"),fullName=Text("fullName"),password=Text("password"),phone=Text("phone"),role="Customer",
                    dateOfBirth=string.IsNullOrEmpty(Text("dateOfBirth"))?null:Text("dateOfBirth"),gender=string.IsNullOrEmpty(Text("gender"))?null:Text("gender")});
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
                var questions=SurveyQuestions();
                await Api.Send(HttpMethod.Post,"v1/surveys",new {title=Text("title"),description=Text("description"),startsAt=Date("startsAt"),endsAt=Date("endsAt"),questions}); break;
            case "survey-update": // [UI2-W5] sửa khảo sát nháp (API chỉ cho khi còn Nháp)
                var updated=SurveyQuestions();
                await Api.Send(HttpMethod.Put,$"v1/surveys/{Number("id")}",new {title=Text("title"),description=Text("description"),startsAt=Date("startsAt"),endsAt=Date("endsAt"),questions=updated});
                TempData["Success"]="Đã lưu khảo sát nháp."; break;
            case "survey-delete": // [UI2-W5+] xóa khảo sát nháp
                await Api.Send(HttpMethod.Delete,$"v1/surveys/{Number("id")}");
                TempData["Success"]="Đã xóa khảo sát nháp."; break;
            case "publish":
            case "close":
                await Api.Send(HttpMethod.Post,$"v1/surveys/{Number("id")}:{operation}"); break;
            case "invite":
                await Api.Send(HttpMethod.Post,$"v1/surveys/{Number("id")}:invite",new {customerId=Number("customerId")}); break;
            case "invite-bulk":
                var form=Request.Form;
                var ids=form["customerIds"].Select(x=>int.Parse(x!,CultureInfo.InvariantCulture)).ToArray();
                var mode=Text("mode");
                object body=mode=="list"
                    ? new {customerIds=ids}
                    : new {all=mode=="all",ageGroup=Optional("ageGroup"),gender=string.IsNullOrEmpty(Text("gender"))?null:Text("gender"),productId=Optional("productId")};
                if(mode=="list" && ids.Length==0) throw new FormatException();
                var result=await Api.Post<JsonElement>($"v1/surveys/{Number("id")}:inviteBulk",body);
                TempData["Success"]=$"Khớp {result.GetProperty("matched")} khách · mời mới {result.GetProperty("invited")} · đã mời trước đó {result.GetProperty("already_invited")}.";
                break;
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
