using System.Globalization;
using System.Text.Json;
using Microsoft.AspNetCore.Mvc;
using MorningBrew.Admin.Services;
namespace MorningBrew.Admin.Pages.ManagerArea;

// [KHO] Quản lý kho nguyên liệu: tồn kho, nhập kho, xuất dùng, hủy hỏng, kiểm kê, lịch sử phiếu kho.
public class InventoryModel(CrmApi api):WorkspaceBase(api)
{
    public override string Title=>"Kho nguyên liệu";
    public override string Subtitle=>"Tồn kho, nhập hàng từ nhà cung cấp, xuất dùng, hủy hỏng và kiểm kê.";
    protected override bool Searchable=>true;
    protected override IReadOnlySet<string> Operations {get;}=new HashSet<string>{"ingredient","ingredient-update","ingredient-active","stock"};

    // tab = stock (tồn kho) | history (lịch sử phiếu)
    [BindProperty(SupportsGet=true)] public string Tab {get;set;}="stock";
    [BindProperty(SupportsGet=true)] public bool Low {get;set;}
    [BindProperty(SupportsGet=true)] public int? IngredientId {get;set;}
    [BindProperty(SupportsGet=true)] public string? Kind {get;set;}
    [BindProperty(SupportsGet=true)] public string? From {get;set;}
    [BindProperty(SupportsGet=true)] public string? To {get;set;}

    public Dictionary<string,JsonElement> Summary {get;set;}=[];
    public List<Dictionary<string,JsonElement>> IngredientOptions {get;set;}=[];
    public List<Dictionary<string,JsonElement>> SupplierOptions {get;set;}=[];
    public bool History=>Tab=="history";

    public static string KindName(string? kind)=>kind switch
    {
        "Import"=>"Nhập kho","Export"=>"Xuất dùng","Waste"=>"Hủy / hư hỏng","Adjust"=>"Kiểm kê",_=>kind??""
    };
    // [CLEAN-06] bỏ KindCss (trang Inventory.cshtml không dùng)

    public string Stat(string key)=>Summary.TryGetValue(key,out var v)?v.ToString():"0";

    protected override async Task LoadAsync()
    {
        Summary=await Api.Get<Dictionary<string,JsonElement>>("v1/inventory/summary");
        IngredientOptions=List(Summary,"ingredient_options");
        SupplierOptions=List(Summary,"supplier_options");

        if(History)
        {
            var query=$"pageSize=50&pageToken={Uri.EscapeDataString(PageToken??"")}&kind={Uri.EscapeDataString(Kind??"")}";
            if(IngredientId is int ingredient) query+=$"&ingredientId={ingredient}";
            if(VietnamDate(From) is DateTime from) query+=$"&fromTime={Uri.EscapeDataString(from.ToString("O"))}";
            if(VietnamDate(To) is DateTime to) query+=$"&toTime={Uri.EscapeDataString(to.AddDays(1).ToString("O"))}";
            var page=await Api.Get<ApiPage>($"v1/inventory/transactions?{query}");
            Rows=page.Items; NextPageToken=page.NextPageToken;
        }
        else
        {
            var page=await Api.Get<ApiPage>($"v1/inventory/ingredients?q={Uri.EscapeDataString(Q??"")}&lowOnly={(Low?"true":"false")}&includeInactive=true");
            Rows=page.Items;
        }
    }

    // Ngày (yyyy-MM-dd) theo giờ Việt Nam → mốc UTC đầu ngày.
    private static DateTime? VietnamDate(string? date)
        => DateOnly.TryParseExact(date,"yyyy-MM-dd",CultureInfo.InvariantCulture,DateTimeStyles.None,out var d)
            ? new DateTimeOffset(d.ToDateTime(TimeOnly.MinValue),TimeSpan.FromHours(7)).UtcDateTime : null;

    private static List<Dictionary<string,JsonElement>> List(Dictionary<string,JsonElement> source,string key)
        => source.TryGetValue(key,out var v) && v.ValueKind==JsonValueKind.Array ? v.Deserialize<List<Dictionary<string,JsonElement>>>() ?? new() : new();

    private string? Blank(string key)=>string.IsNullOrWhiteSpace(Text(key))?null:Text(key);
    private decimal? OptionalMoney(string key)=>string.IsNullOrWhiteSpace(Text(key))?null:Money(key);

    protected override async Task<IActionResult?> ExecuteAsync(string operation)
    {
        switch(operation)
        {
            case "ingredient":
                await Api.Send(HttpMethod.Post,"v1/inventory/ingredients",new{code=Text("code"),name=Text("name"),unit=Text("unit"),minStock=OptionalMoney("minStock")??0,supplierId=Optional("supplierId")});
                TempData["Success"]="Đã thêm nguyên liệu."; break;
            case "ingredient-update":
                await Api.Send(HttpMethod.Put,$"v1/inventory/ingredients/{Number("id")}",new{code=Text("code"),name=Text("name"),unit=Text("unit"),minStock=OptionalMoney("minStock")??0,supplierId=Optional("supplierId")});
                TempData["Success"]="Đã cập nhật nguyên liệu."; break;
            case "ingredient-active":
                await Api.Send(HttpMethod.Put,$"v1/inventory/ingredients/{Number("id")}/active",new{isActive=Text("active")=="true"});
                TempData["Success"]=Text("active")=="true"?"Đã dùng lại nguyên liệu.":"Đã ngừng dùng nguyên liệu."; break;
            case "stock":
                var kind=Text("kind");
                var result=await Api.Post<JsonElement>("v1/inventory/transactions",new{
                    ingredientId=Number("ingredientId"),kind,quantity=Money("quantity"),
                    unitCost=kind=="Import"?OptionalMoney("unitCost"):null,
                    supplierId=kind=="Import"?Optional("supplierId"):null,
                    note=Blank("note")});
                var balance=result.TryGetProperty("balance_after",out var b)?b.GetDecimal().ToString("0.###",CultureInfo.InvariantCulture):"?";
                TempData["Success"]=$"Đã ghi phiếu {KindName(kind).ToLowerInvariant()}. Tồn kho mới: {balance}.";
                break;
        }
        return RedirectToPage(new{tab=Text("tab") is { Length: >0 } t ? t : Tab,q=Q,low=Low});
    }
}
