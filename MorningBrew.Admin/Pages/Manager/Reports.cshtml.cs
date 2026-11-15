using System.Globalization;
using System.Text.Json;
using Microsoft.AspNetCore.Mvc;
using MorningBrew.Admin.Services;
namespace MorningBrew.Admin.Pages.ManagerArea;
public class ReportsModel(CrmApi api):WorkspaceBase(api)
{
    public override string Title=>"Báo cáo";
    public override string Subtitle=>"Báo cáo khách hàng theo độ tuổi, giới tính, sở thích (yêu cầu 4.1.5) và doanh thu.";
    public Dictionary<string,JsonElement>? Report {get;set;}
    // [UI2-W4] Kỳ doanh thu tính theo giờ Việt Nam, không theo mốc UTC.
    // [WEB-09] Người xem tự chọn kỳ (?from=yyyy-MM-dd&to=yyyy-MM-dd, tối đa 366 ngày) hoặc bấm kỳ có sẵn; mặc định 30 ngày gần nhất.
    [BindProperty(SupportsGet=true,Name="from")] public string? FromText {get;set;}
    [BindProperty(SupportsGet=true,Name="to")] public string? ToText {get;set;}
    public DateTime From {get;set;}
    public DateTime To {get;set;}
    public DateTime Today {get;}=DateTime.UtcNow.AddHours(7).Date;
    public string? PeriodError {get;set;}
    public List<Dictionary<string,JsonElement>> Daily {get;set;}=[]; // [WEB-09] doanh thu từng ngày
    public List<Dictionary<string,JsonElement>> Promotions {get;set;}=[]; // [CRM-04] hiệu quả ưu đãi
    protected override IReadOnlySet<string> Operations {get;}=new HashSet<string>();
    public static string Day(DateTime d)=>d.ToString("yyyy-MM-dd",CultureInfo.InvariantCulture);
    // Các kỳ có sẵn: (nhãn, từ ngày, đến ngày).
    public IEnumerable<(string Label,DateTime From,DateTime To)> Presets()
    {
        var month=new DateTime(Today.Year,Today.Month,1);
        yield return ("7 ngày",Today.AddDays(-6),Today);
        yield return ("30 ngày",Today.AddDays(-29),Today);
        yield return ("Tháng này",month,Today);
        yield return ("Tháng trước",month.AddMonths(-1),month.AddDays(-1));
        yield return ("90 ngày",Today.AddDays(-89),Today);
        yield return ("Năm nay",new DateTime(Today.Year,1,1),Today);
    }
    protected override async Task LoadAsync()
    {
        Report=await Api.Get<Dictionary<string,JsonElement>>("v1/reports/customers");
        From=Today.AddDays(-29); To=Today;
        static DateTime? Parse(string? s)=>DateTime.TryParseExact(s,"yyyy-MM-dd",CultureInfo.InvariantCulture,DateTimeStyles.None,out var d)?d:null;
        if(!string.IsNullOrEmpty(FromText)||!string.IsNullOrEmpty(ToText))
        {
            var from=Parse(FromText); var to=Parse(ToText);
            if(from is null||to is null) PeriodError="Chọn đủ Từ ngày và Đến ngày.";
            else if(from>to) PeriodError="Từ ngày phải trước hoặc bằng Đến ngày.";
            else if((to.Value-from.Value).TotalDays>365) PeriodError="Kỳ báo cáo tối đa 366 ngày.";
            else if(from>Today) PeriodError="Từ ngày không được ở tương lai.";
            else { From=from.Value; To=to.Value>Today?Today:to.Value; }
            if(PeriodError is not null) PeriodError+=" Đang hiển thị 30 ngày gần nhất.";
        }
        string Vn(DateTime d)=>Uri.EscapeDataString(Day(d)+"T00:00:00+07:00");
        var range=$"fromTime={Vn(From)}&toTime={Vn(To.AddDays(1))}";
        Rows=(await Api.Get<ApiPage>($"v1/reports/revenue?{range}")).Items; // [UI2-W4]
        Daily=(await Api.Get<ApiPage>($"v1/reports/revenue/daily?{range}")).Items; // [WEB-09]
        Promotions=(await Api.Get<ApiPage>("v1/reports/promotions")).Items; // [CRM-04]
    }

    // [CRM-07] Xuất toàn bộ báo cáo đang xem (đúng kỳ doanh thu) ra một file Excel nhiều sheet.
    public async Task<IActionResult> OnGetExportAsync()
    {
        try { await LoadAsync(); }
        catch(CrmApiException e) { TempData["Error"]=e.Message; return RedirectToPage(new{from=FromText,to=ToText}); }
        var sheets=new List<XlsxSheet>
        {
            XlsxSheet.From($"Doanh thu {From:dd.MM}-{To:dd.MM.yyyy}",Daily,[("day","Ngày"),("completed_orders","Đơn hoàn tất"),("gross_revenue","Doanh thu (đ)"),("refunded_revenue","Đã hoàn tiền (đ)"),("net_revenue","Doanh thu thuần (đ)")],
                (k,r)=>k=="day"&&DateTime.TryParse(r[k].ToString(),CultureInfo.InvariantCulture,DateTimeStyles.None,out var d)?d.ToString("dd/MM/yyyy"):null),
            XlsxSheet.From("Hiệu quả ưu đãi",Promotions,[("display_name","Ưu đãi"),("is_active","Đang cấp"),("issued","Đã cấp"),("used","Đã dùng"),("unused","Chưa dùng"),
                ("expired","Hết hạn chưa dùng"),("total_discount","Tổng tiền giảm (đ)"),("revenue","Doanh thu đơn có voucher (đ)"),("points_spent","Điểm đã đổi")])
        };
        if(Report is not null)
        {
            sheets.Add(XlsxSheet.From("Nhóm hành vi mua",ReportView.Rows(Report,"segments"),[("segment","Nhóm khách"),("customers","Số khách"),("completed_orders","Đơn hoàn tất"),("total_spent","Tổng chi tiêu (đ)"),("average_spent","Chi tiêu TB / khách (đ)")]));
            sheets.Add(XlsxSheet.From("Độ tuổi",ReportView.Rows(Report,"age_groups"),[("age_group","Nhóm tuổi"),("customers","Số khách")]));
            sheets.Add(XlsxSheet.From("Giới tính",ReportView.Rows(Report,"genders"),[("gender","Giới tính"),("customers","Số khách")]));
            sheets.Add(XlsxSheet.From("Món yêu thích",ReportView.Rows(Report,"products"),[("product","Món"),("category","Danh mục"),("customers","Số khách thích")]));
        }
        return File(XlsxExport.Build(sheets),XlsxExport.ContentType,XlsxExport.FileName("BaoCao"));
    }
}
