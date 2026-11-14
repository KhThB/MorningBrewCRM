using MorningBrew.Admin.Services;
namespace MorningBrew.Admin.Pages.ManagerArea;
public class PromotionsModel(CrmApi api):WorkspaceBase(api)
{
    public override string Title=>"Ưu đãi";
    public override string Subtitle=>"Chương trình ưu đãi và voucher cho khách hàng.";
    protected override string ListPath=>"v1/promotions";
    protected override bool Searchable=>true; // [WEB-11]
    protected override IReadOnlySet<string> Operations {get;}=new HashSet<string>{"promotion","issue","promotion-active","promotion-update","issue-bulk"} /* [WEB-11] */;
    protected override async Task<Microsoft.AspNetCore.Mvc.IActionResult?> ExecuteAsync(string operation)
        // [WEB-11] cùng một cửa sổ Cấp voucher: "Một khách hàng" dùng thao tác issue, các cách còn lại dùng issue-bulk
        => await base.ExecuteAsync(operation=="issue" && Text("mode") is "list" or "segment" or "all" ? "issue-bulk" : operation);
}
