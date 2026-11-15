using System.Text.Json;
using Microsoft.AspNetCore.Mvc;
using MorningBrew.Admin.Services;
namespace MorningBrew.Admin.Pages.ManagerArea;
public class SurveysModel(CrmApi api):WorkspaceBase(api)
{
    public override string Title=>SurveyId.HasValue?"Kết quả khảo sát":"Khảo sát";
    public override string Subtitle=>"Tạo khảo sát, gửi đến khách hàng và thống kê kết quả (yêu cầu 4.1.6–4.1.7).";
    [BindProperty(SupportsGet=true)] public int? SurveyId {get;set;}
    public Dictionary<string,JsonElement>? Summary {get;set;}
    // [UI2-W5] ?edit={id}: nạp khảo sát nháp để mở lại cửa sổ soạn với đủ câu hỏi.
    [BindProperty(SupportsGet=true)] public int? Edit {get;set;}
    public string? EditJson {get;set;}
    protected override IReadOnlySet<string> Operations {get;}=new HashSet<string>{"survey","publish","close","invite","invite-bulk","results","survey-update","survey-delete"} /* [UI2-W5] */;
    public static readonly (int Id,string Name)[] AgeGroups={(1,"Dưới 18"),(2,"18–24"),(3,"25–34"),(4,"35–44"),(5,"45 trở lên"),(6,"Chưa cung cấp")};
    // [UI2-W5] Dữ liệu khảo sát nháp → JSON cho trang điền lại form (giờ đổi sang giờ máy cho ô datetime-local).
    private async Task<string?> LoadDraftAsync(int id)
    {
        var s=await Api.Get<Dictionary<string,JsonElement>>($"v1/surveys/{id}");
        if(ReportView.Text(s,"status")!="Draft"){ TempData["Error"]="Chỉ sửa được khảo sát đang ở trạng thái Nháp."; return null; }
        static string Local(string v)=>DateTimeOffset.TryParse(v,System.Globalization.CultureInfo.InvariantCulture,System.Globalization.DateTimeStyles.AssumeUniversal,out var t)?t.ToLocalTime().ToString("yyyy-MM-ddTHH:mm"):"";
        var options=ReportView.Rows(s,"options");
        var questions=ReportView.Rows(s,"questions").Select(q=>new{
            text=ReportView.Text(q,"text"), type=ReportView.Text(q,"type"), required=q.TryGetValue("is_required",out var r)&&r.ValueKind==JsonValueKind.True,
            options=string.Join("\n",options.Where(o=>ReportView.Int(o,"question_id")==ReportView.Int(q,"question_id")).Select(o=>ReportView.Text(o,"text")))});
        return JsonSerializer.Serialize(new{id,title=ReportView.Text(s,"title"),description=ReportView.Text(s,"description"),
            startsAt=Local(ReportView.Text(s,"start_time")),endsAt=Local(ReportView.Text(s,"end_time")),questions});
    }

    // [CRM-07] Xuất kết quả khảo sát ra Excel: sheet tổng hợp theo câu hỏi / lựa chọn + sheet từng câu trả lời (mọi trang).
    public async Task<IActionResult> OnGetExportAsync(int surveyId)
    {
        try
        {
            var summary=await Api.Get<Dictionary<string,JsonElement>>($"v1/surveys/{surveyId}/summary");
            var rows=new List<Dictionary<string,JsonElement>>(); string? token="";
            for(var i=0;i<200 && token is not null;i++)
            {
                var page=await Api.Get<ApiPage>($"v1/surveys/{surveyId}/results?pageSize=100&pageToken={Uri.EscapeDataString(token)}");
                rows.AddRange(page.Items); token=string.IsNullOrEmpty(page.NextPageToken)?null:page.NextPageToken;
            }
            var questions=ReportView.Rows(summary,"questions");
            string Question(int id)=>ReportView.Text(questions.FirstOrDefault(q=>ReportView.Int(q,"question_id")==id)??[],"text");
            var sheets=new List<XlsxSheet>
            {
                XlsxSheet.From("Tổng hợp câu hỏi",questions,[("display_order","STT"),("text","Câu hỏi"),("type","Loại"),("responses","Số câu trả lời"),("average_rating","Điểm TB")],
                    (k,r)=>k=="display_order"?ReportView.Int(r,k)+1:null),
                XlsxSheet.From("Lựa chọn",ReportView.Rows(summary,"options"),[("question_id","Câu hỏi"),("text","Lựa chọn"),("votes","Số lượt chọn")],
                    (k,r)=>k=="question_id"?Question(ReportView.Int(r,k)):null),
                XlsxSheet.From("Câu trả lời",rows.Where(r=>r.TryGetValue("customer_id",out var c)&&c.ValueKind!=JsonValueKind.Null),
                    [("customer_id","Mã khách"),("text","Câu hỏi"),("type","Loại"),("rating_value","Điểm"),("selected_options","Lựa chọn"),("text_value","Trả lời tự luận")])
            };
            return File(XlsxExport.Build(sheets),XlsxExport.ContentType,XlsxExport.FileName($"KhaoSat{surveyId}"));
        }
        catch(CrmApiException e) { TempData["Error"]=e.Message; return RedirectToPage(new{surveyId}); }
    }

    protected override async Task LoadAsync()
    {
        if(SurveyId.HasValue)
        {
            var results=await Api.Get<ApiPage>($"v1/surveys/{SurveyId}/results?pageToken={Uri.EscapeDataString(PageToken??"")}");
            Rows=results.Items; NextPageToken=results.NextPageToken;
            Summary=await Api.Get<Dictionary<string,JsonElement>>($"v1/surveys/{SurveyId}/summary");
        }
        else
        {
            var page=await Api.Get<ApiPage>($"v1/surveys?pageSize=50&pageToken={Uri.EscapeDataString(PageToken??"")}");
            Rows=page.Items; NextPageToken=page.NextPageToken;
            if(Edit.HasValue) EditJson=await LoadDraftAsync(Edit.Value); // [UI2-W5]
        }
    }
}
