using System.Text.Json;
using MorningBrew.Admin.Services;
namespace MorningBrew.Admin.Pages.ManagerArea;

// [REV-09] Chuyển từ Admin server sang Quản lý CRM (thực đơn, giá bán thuộc quản lý bán hàng – tách khỏi Admin theo yêu cầu đồ án).
// Quản lý chỉ xem; thêm / sửa / đổi giá / ảnh / size / danh mục / ngừng bán vẫn chỉ chủ cửa hàng (quyền Catalog.Manage, API kiểm tra lại).
public class ProductsModel(CrmApi api,IConfiguration config):WorkspaceBase(api)
{
    // [UI2-IMG] Ảnh tải lên nằm ở API (/uploads/products/...) → ghép địa chỉ API để xem trước trong Admin.
    public string? ImageSrc(string? url)=>url is not null && url.StartsWith("/uploads/") ? (config["Api:BaseUrl"]??"http://localhost:5029/").TrimEnd('/')+url : null;
    public override string Title=>"Sản phẩm & giá";
    protected override string ListPath=>"v1/variants";
    protected override bool Searchable=>true; // [WEB-13]
    protected override IReadOnlySet<string> Operations {get;}=new HashSet<string>{"product","product-active","price","product-image", /* [REV-06] bỏ product-stock */ /* [UI2-A4a], [UI2-IMG] */
        "product-update","variant-add","variant-active","category","category-update","category-active"} /* [WEB-03] */;
    public List<Dictionary<string,JsonElement>> Categories {get;set;}=[]; // [WEB-03] cả danh mục đang ẩn
    public bool CanEdit=>IsOwner; // [REV-09] Catalog.Manage là quyền bảo vệ, chỉ chủ cửa hàng có
    public override string Subtitle=>CanEdit?"Thêm món, size, giá, ảnh, danh mục; ngừng bán. Báo tạm hết hằng ngày ở Kho nguyên liệu → Tình trạng món.":"Chỉ xem: giá và thực đơn do chủ cửa hàng cập nhật. Báo tạm hết ở Kho nguyên liệu → Tình trạng món.";
    protected override async Task LoadAsync()
    {
        await base.LoadAsync();
        if(CanEdit) Categories=(await Api.Get<ApiPage>("v1/categories")).Items; // [REV-09] API danh mục chỉ cho chủ cửa hàng
    }
}
