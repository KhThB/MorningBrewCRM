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
    protected override IReadOnlySet<string> Operations {get;}=new HashSet<string>{"survey","publish","close","invite","invite-bulk","results"};
    public static readonly (int Id,string Name)[] AgeGroups={(1,"Dưới 18"),(2,"18–24"),(3,"25–34"),(4,"35–44"),(5,"45 trở lên"),(6,"Chưa cung cấp")};
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
        }
    }
}
