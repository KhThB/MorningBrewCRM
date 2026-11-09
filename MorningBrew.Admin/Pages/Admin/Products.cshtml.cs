using MorningBrew.Admin.Services;
namespace MorningBrew.Admin.Pages.AdminArea;
public class ProductsModel(CrmApi api):WorkspaceBase(api)
{
    public override string Title=>"Sản phẩm & giá";
    public override string Subtitle=>"Danh mục sản phẩm dùng chung và giá bán toàn cửa hàng.";
    protected override string ListPath=>"v1/variants";
    protected override IReadOnlySet<string> Operations {get;}=new HashSet<string>{"product","product-active","price","product-stock"} /* [UI2-A4a] */;
}
