using MorningBrew.Admin.Services;
namespace MorningBrew.Admin.Pages.AdminArea;
public class ProductsModel(CrmApi api,IConfiguration config):WorkspaceBase(api)
{
    // [UI2-IMG] Ảnh tải lên nằm ở API (/uploads/products/...) → ghép địa chỉ API để xem trước trong Admin.
    public string? ImageSrc(string? url)=>url is not null && url.StartsWith("/uploads/") ? (config["Api:BaseUrl"]??"http://localhost:5029/").TrimEnd('/')+url : null;
    public override string Title=>"Sản phẩm & giá";
    public override string Subtitle=>"Danh mục sản phẩm dùng chung và giá bán toàn cửa hàng.";
    protected override string ListPath=>"v1/variants";
    protected override IReadOnlySet<string> Operations {get;}=new HashSet<string>{"product","product-active","price","product-stock","product-image"} /* [UI2-A4a], [UI2-IMG] */;
}
