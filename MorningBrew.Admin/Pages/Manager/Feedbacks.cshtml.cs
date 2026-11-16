using Microsoft.AspNetCore.Mvc;
using MorningBrew.Admin.Services;
namespace MorningBrew.Admin.Pages.ManagerArea;
public class FeedbacksModel(CrmApi api):WorkspaceBase(api)
{
    public override string Title=>"Phản hồi";
    protected override IReadOnlySet<string> Operations {get;}=new HashSet<string>{"feedback"};
    [BindProperty(SupportsGet=true)] public string? Status {get;set;}
    protected override async Task LoadAsync()
    {
        var page=await Api.Get<ApiPage>($"v1/feedbacks?pageSize=50&pageToken={Uri.EscapeDataString(PageToken??"")}&status={Uri.EscapeDataString(Status??"")}&q={Uri.EscapeDataString(Q??"")}");
        Rows=page.Items; NextPageToken=page.NextPageToken;
    }
}
