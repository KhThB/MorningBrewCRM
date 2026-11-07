using MorningBrew.Admin.Services;
namespace MorningBrew.Admin.Pages.ManagerArea;
public class FeedbacksModel(CrmApi api):WorkspaceBase(api)
{
    public override string Title=>"Phản hồi";
    public override string Subtitle=>"Tiếp nhận và trả lời phản hồi của khách hàng (yêu cầu 4.1.4).";
    protected override string ListPath=>"v1/feedbacks";
    protected override IReadOnlySet<string> Operations {get;}=new HashSet<string>{"feedback"};
}
