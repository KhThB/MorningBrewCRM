using System.ComponentModel.DataAnnotations;
using System.Text.Json;
using Microsoft.AspNetCore.Mvc;
using MorningBrew.Api.Infrastructure;
namespace MorningBrew.Api.Controllers;

public sealed partial class AdminController
{
    public record QuestionInput([Required,MaxLength(2000)] string Text,[Required] string Type,
        bool IsRequired=true,int? RatingMin=null,int? RatingMax=null,string[]? Options=null);
    public record SurveyInput([Required,MaxLength(250)] string Title,[MaxLength(2000)] string? Description,
        string? Scope,int? AreaId,int? BranchId,DateTimeOffset StartsAt,DateTimeOffset EndsAt,
        [Required,MinLength(1),MaxLength(40)] QuestionInput[] Questions);

    // Một cửa hàng: mặc định phạm vi chung (Chain) để chủ cửa hàng và quản lý cùng thấy, cùng quản lý.
    // Scope/AreaId/BranchId được giữ để tương thích client cũ; không cần gửi.
    // Trả về cả Scope đã chuẩn hóa: client không gửi scope thì phải ghi 'Chain' (cột Scope NOT NULL).
    private static (string Scope, int? Area, int? Branch) NormalizeScope(string? scope, int? areaId, int? branchId) => (string.IsNullOrWhiteSpace(scope) ? "Chain" : scope) switch
    {
        "Chain" => ("Chain", null, null),
        "Area" => throw new ApiException(400, "Hệ thống chỉ có 1 cửa hàng, không dùng phạm vi khu vực."),
        "Branch" => ("Branch", null, branchId ?? throw new ApiException(400, "Phạm vi cửa hàng: thiếu mã cửa hàng.")),
        _ => throw new ApiException(400, "Phạm vi không hợp lệ.")
    };

    private static void ValidateQuestions(QuestionInput[] questions)
    {
        foreach (var q in questions)
        {
            if (q is null || q.Type is not ("Text" or "Rating" or "SingleChoice" or "MultipleChoice") ||
                string.IsNullOrWhiteSpace(q.Text) || q.Text.Length>2000 ||
                (q.Type=="Rating" && (q.RatingMin is null || q.RatingMax is null || q.RatingMin>=q.RatingMax)) ||
                (q.Type is "SingleChoice" or "MultipleChoice" && (q.Options is null || q.Options.Length<2 || q.Options.Length>30 || q.Options.Any(x=>string.IsNullOrWhiteSpace(x)||x.Length>500))))
                throw new ApiException(400,"Câu hỏi hoặc lựa chọn không hợp lệ.");
        }
    }

    [HttpPost("surveys")]
    public async Task<IActionResult> CreateSurvey(SurveyInput input)
    {
        if (input.EndsAt <= input.StartsAt) throw new ApiException(400,"Ngày kết thúc phải sau ngày bắt đầu.");
        var (scope, area, branch) = NormalizeScope(input.Scope, input.AreaId, input.BranchId);
        ValidateQuestions(input.Questions); // [UI2-W5] dùng chung với UpdateDraftSurvey
        var questions = input.Questions.Select((q,i)=>new { text=q.Text,type=q.Type,required=q.IsRequired,position=i,
            min=q.Type=="Rating"?q.RatingMin:null,max=q.Type=="Rating"?q.RatingMax:null,options=q.Options });
        var created = await db.Query("""
            SET XACT_ABORT ON; BEGIN TRAN;
            IF dbo.CanScope(@actor,@scope,@area,@branch,'Surveys.Manage')=0 THROW 51103,'Scope denied.',1;
            INSERT dbo.Surveys(Title,Description,CreatedBy,Scope,AreaID,BranchID,StartsAt,EndsAt)
            VALUES(@title,@description,@actor,@scope,@area,@branch,@start,@end);
            DECLARE @id int=SCOPE_IDENTITY();
            INSERT dbo.SurveyQuestions(SurveyID,Text,Type,IsRequired,DisplayOrder,RatingMin,RatingMax)
            SELECT @id,text,type,required,position,min,max FROM OPENJSON(@questions)
            WITH(text nvarchar(2000),type varchar(15),required bit,position int,min int,max int);
            INSERT dbo.SurveyQuestionOptions(QuestionID,Text,DisplayOrder)
            SELECT q.QuestionID,o.value,CONVERT(int,o.[key]) FROM OPENJSON(@questions) j
            JOIN dbo.SurveyQuestions q ON q.SurveyID=@id AND q.DisplayOrder=CONVERT(int,JSON_VALUE(j.value,'$.position'))
            CROSS APPLY OPENJSON(j.value,'$.options') o WHERE q.Type IN ('SingleChoice','MultipleChoice');
            INSERT dbo.AuditLogs(ActorID,BranchID,Action,EntityType,EntityID) VALUES(@actor,@branch,'Survey.Create','Surveys',@id);
            COMMIT; SELECT @id AS SurveyID;
            """, ("@actor",Actor),("@scope",scope),("@area",area),("@branch",branch),
            ("@title",input.Title),("@description",input.Description),("@start",input.StartsAt.UtcDateTime),("@end",input.EndsAt.UtcDateTime),
            ("@questions",JsonSerializer.Serialize(questions)));
        var id = (int)created.Single()["SurveyID"]!;
        return Created($"/v1/surveys/{id}", await ReadResource("surveys", id));
    }
    // [UI2-W5] Sửa khảo sát còn ở trạng thái Nháp: cập nhật tiêu đề, mô tả, thời gian và thay toàn bộ câu hỏi.
    // Trigger TR_SurveyQuestions_Freeze / TR_Surveys_Publish vẫn chặn ở tầng database nếu khảo sát đã phát hành.
    [HttpPut("surveys/{id:int}")]
    public async Task<IActionResult> UpdateDraftSurvey(int id, SurveyInput input)
    {
        if (input.EndsAt <= input.StartsAt) throw new ApiException(400,"Ngày kết thúc phải sau ngày bắt đầu.");
        ValidateQuestions(input.Questions);
        var questions = input.Questions.Select((q,i)=>new { text=q.Text,type=q.Type,required=q.IsRequired,position=i,
            min=q.Type=="Rating"?q.RatingMin:null,max=q.Type=="Rating"?q.RatingMax:null,options=q.Options });
        await db.Query("""
            SET XACT_ABORT ON; BEGIN TRAN;
            DECLARE @status varchar(10),@scope varchar(10),@area int,@branch int;
            SELECT @status=Status,@scope=Scope,@area=AreaID,@branch=BranchID FROM dbo.Surveys WITH(UPDLOCK,HOLDLOCK) WHERE SurveyID=@id;
            IF @status IS NULL THROW 51104,N'Không tìm thấy khảo sát.',1;
            IF dbo.CanScope(@actor,@scope,@area,@branch,'Surveys.Manage')=0 THROW 51103,'Scope denied.',1;
            IF @status<>'Draft' THROW 51124,N'Chỉ sửa được khảo sát đang ở trạng thái Nháp.',1;
            UPDATE dbo.Surveys SET Title=@title,Description=@description,StartsAt=@start,EndsAt=@end WHERE SurveyID=@id;
            DELETE o FROM dbo.SurveyQuestionOptions o JOIN dbo.SurveyQuestions q ON q.QuestionID=o.QuestionID WHERE q.SurveyID=@id;
            DELETE dbo.SurveyQuestions WHERE SurveyID=@id;
            INSERT dbo.SurveyQuestions(SurveyID,Text,Type,IsRequired,DisplayOrder,RatingMin,RatingMax)
            SELECT @id,text,type,required,position,min,max FROM OPENJSON(@questions)
            WITH(text nvarchar(2000),type varchar(15),required bit,position int,min int,max int);
            INSERT dbo.SurveyQuestionOptions(QuestionID,Text,DisplayOrder)
            SELECT q.QuestionID,o.value,CONVERT(int,o.[key]) FROM OPENJSON(@questions) j
            JOIN dbo.SurveyQuestions q ON q.SurveyID=@id AND q.DisplayOrder=CONVERT(int,JSON_VALUE(j.value,'$.position'))
            CROSS APPLY OPENJSON(j.value,'$.options') o WHERE q.Type IN ('SingleChoice','MultipleChoice');
            INSERT dbo.AuditLogs(ActorID,BranchID,Action,EntityType,EntityID) VALUES(@actor,@branch,'Survey.Update','Surveys',@id);
            COMMIT;
            """, ("@id",id),("@actor",Actor),("@title",input.Title),("@description",input.Description),
            ("@start",input.StartsAt.UtcDateTime),("@end",input.EndsAt.UtcDateTime),("@questions",JsonSerializer.Serialize(questions)));
        return Ok(await ReadResource("surveys", id));
    }

    // [UI2-W5+] Xóa khảo sát nháp (chưa phát hành nên chưa có người được mời hay câu trả lời).
    [HttpDelete("surveys/{id:int}")]
    public async Task<IActionResult> DeleteDraftSurvey(int id)
    {
        await db.Query("""
            SET XACT_ABORT ON; BEGIN TRAN;
            DECLARE @status varchar(10),@scope varchar(10),@area int,@branch int;
            SELECT @status=Status,@scope=Scope,@area=AreaID,@branch=BranchID FROM dbo.Surveys WITH(UPDLOCK,HOLDLOCK) WHERE SurveyID=@id;
            IF @status IS NULL THROW 51104,N'Không tìm thấy khảo sát.',1;
            IF dbo.CanScope(@actor,@scope,@area,@branch,'Surveys.Manage')=0 THROW 51103,'Scope denied.',1;
            IF @status<>'Draft' THROW 51124,N'Chỉ xóa được khảo sát đang ở trạng thái Nháp; khảo sát đã phát hành thì dùng "Đóng".',1;
            DELETE o FROM dbo.SurveyQuestionOptions o JOIN dbo.SurveyQuestions q ON q.QuestionID=o.QuestionID WHERE q.SurveyID=@id;
            DELETE dbo.SurveyQuestions WHERE SurveyID=@id;
            DELETE dbo.Notifications WHERE SurveyID=@id;
            DELETE dbo.Surveys WHERE SurveyID=@id;
            INSERT dbo.AuditLogs(ActorID,BranchID,Action,EntityType,EntityID) VALUES(@actor,@branch,'Survey.Delete','Surveys',@id);
            COMMIT;
            """, ("@id",id),("@actor",Actor));
        return NoContent();
    }

    [HttpPost("surveys/{id:int}:publish")]
    public async Task<IActionResult> Publish(int id)
    {
        await db.Procedure("dbo.PublishSurvey",("@ActorID",Actor),("@SurveyID",id)); return NoContent();
    }
    [HttpPost("surveys/{id:int}:close")]
    public async Task<IActionResult> Close(int id)
    {
        // [BUG-06] Tách "không có quyền" (51103 → 403) khỏi "sai trạng thái" (51124 → 409, thông báo tiếng Việt).
        await db.Query("""
            SET XACT_ABORT ON; BEGIN TRAN;
            DECLARE @status varchar(10),@scope varchar(10),@area int,@branch int;
            SELECT @status=Status,@scope=Scope,@area=AreaID,@branch=BranchID FROM dbo.Surveys WITH(UPDLOCK,HOLDLOCK) WHERE SurveyID=@id;
            IF @status IS NULL THROW 51104,N'Không tìm thấy khảo sát.',1;
            IF dbo.CanScope(@actor,@scope,@area,@branch,'Surveys.Manage')=0 THROW 51103,'Survey outside scope.',1;
            IF @status<>'Open' THROW 51124,N'Chỉ đóng được khảo sát đang mở.',1;
            UPDATE dbo.Surveys SET Status='Closed' WHERE SurveyID=@id;
            INSERT dbo.AuditLogs(ActorID,BranchID,Action,EntityType,EntityID) VALUES(@actor,@branch,'Survey.Close','Surveys',@id);
            COMMIT;
            """,("@id",id),("@actor",Actor)); return NoContent();
    }
    public record CustomerInput([Range(1,int.MaxValue)] int CustomerId);
    [HttpPost("surveys/{id:int}:invite")]
    public async Task<IActionResult> Invite(int id,CustomerInput input)
    {
        await db.Procedure("dbo.InviteSurvey",("@ActorID",Actor),("@SurveyID",id),("@CustomerID",input.CustomerId)); return NoContent();
    }
    [HttpGet("surveys/{id:int}/results")]
    public async Task<ApiPage> SurveyResults(int id, [FromQuery] PageRequest page)
    {
        await db.Query("IF NOT EXISTS(SELECT 1 FROM dbo.Surveys WHERE SurveyID=@id AND dbo.CanScope(@actor,Scope,AreaID,BranchID,'Surveys.Manage')=1) THROW 51103,'Scope denied.',1;",("@actor",Actor),("@id",id));
        var result = await pages.Read("""
            SELECT q.QuestionID,q.Text,q.Type,r.CustomerID,COALESCE(r.CustomerID,0) AS CursorCustomerID,r.TextValue,r.RatingValue,
            STRING_AGG(CONVERT(nvarchar(max),o.Text),N', ') AS SelectedOptions
            FROM dbo.SurveyQuestions q LEFT JOIN dbo.SurveyResponses r ON r.QuestionID=q.QuestionID
            LEFT JOIN dbo.SurveyAnswerOptions a ON a.ResponseID=r.ResponseID LEFT JOIN dbo.SurveyQuestionOptions o ON o.OptionID=a.OptionID
            WHERE q.SurveyID=@id GROUP BY q.QuestionID,q.Text,q.Type,r.CustomerID,r.TextValue,r.RatingValue
            """,["QuestionID","CursorCustomerID"],false,page,$"{Actor}:surveyResults:{id}",[("@id",id)]);
        foreach (var row in result.Items) row.Remove("CursorCustomerID");
        return result;
    }
    public record PromotionInput([Required,MaxLength(150)] string Name,[MaxLength(2000)] string? Description,
        string? Scope,int? AreaId,int? BranchId,[Required] string DiscountType,decimal DiscountValue,
        decimal? MaxDiscount,decimal MinSubtotal,DateTimeOffset StartsAt,DateTimeOffset EndsAt,int? PointsCost,int TotalLimit,int PerCustomerLimit);
    [HttpPost("promotions")]
    public async Task<IActionResult> CreatePromotion(PromotionInput input)
    {
        var (scope, area, branch) = NormalizeScope(input.Scope, input.AreaId, input.BranchId);
        var created = await db.Query("""
        SET XACT_ABORT ON; BEGIN TRAN;
        IF dbo.CanScope(@actor,@scope,@area,@branch,'Promotions.Manage')=0 THROW 51103,'Scope denied.',1;
        INSERT dbo.Promotions(Name,Description,CreatedBy,Scope,AreaID,BranchID,DiscountType,DiscountValue,MaxDiscount,MinSubtotal,StartsAt,EndsAt,PointsCost,TotalLimit,PerCustomerLimit)
        VALUES(@name,@description,@actor,@scope,@area,@branch,@type,@value,@max,@min,@start,@end,@points,@limit,@per);
        DECLARE @id int=SCOPE_IDENTITY();
        INSERT dbo.AuditLogs(ActorID,BranchID,Action,EntityType,EntityID) VALUES(@actor,@branch,'Promotion.Create','Promotions',@id);
        COMMIT; SELECT @id AS PromotionID;
        """,("@actor",Actor),("@scope",scope),("@area",area),("@branch",branch),
        ("@name",input.Name),("@description",input.Description),("@type",input.DiscountType),("@value",input.DiscountValue),
        ("@max",input.MaxDiscount),("@min",input.MinSubtotal),("@start",input.StartsAt.UtcDateTime),("@end",input.EndsAt.UtcDateTime),
        ("@points",input.PointsCost),("@limit",input.TotalLimit),("@per",input.PerCustomerLimit));
        var id = (int)created.Single()["PromotionID"]!;
        return Created($"/v1/promotions/{id}", await ReadResource("promotions", id));
    }
    [HttpPut("promotions/{id:int}/active")]
    public async Task<IActionResult> PromotionActive(int id,ActiveInput input)
    {
        await db.Query("""
            UPDATE dbo.Promotions SET IsActive=@active WHERE PromotionID=@id AND dbo.CanScope(@actor,Scope,AreaID,BranchID,'Promotions.Manage')=1;
            IF @@ROWCOUNT=0 THROW 51103,'Promotion outside scope.',1;
            """,("@actor",Actor),("@id",id),("@active",input.IsActive)); return NoContent();
    }
    public record VoucherInput([Range(1,int.MaxValue)] int CustomerId,Guid RequestKey);
    [HttpPost("promotions/{id:int}:issue")]
    public async Task<Dictionary<string,object?>> Issue(int id,VoucherInput input)
    {
        if(input.RequestKey==Guid.Empty) throw new ApiException(400,"RequestKey không được rỗng.");
        var rows = await db.Procedure("dbo.IssueVoucher",("@ActorID",Actor),("@PromotionID",id),("@CustomerID",input.CustomerId),("@RequestKey",input.RequestKey));
        return await VouchersController.Read(db,Actor,(int)rows.Single()["CustomerPromotionID"]!);
    }
    public record AdjustInput(int Delta,[Required,StringLength(300,MinimumLength=3)] string Note,Guid RequestKey);
    [HttpPost("customers/{id:int}:adjustPoints")]
    public async Task<IActionResult> AdjustPoints(int id,AdjustInput input)
    {
        await RequireOwner();
        if(input.Delta==0||Math.Abs((long)input.Delta)>1000000||input.RequestKey==Guid.Empty) throw new ApiException(400,"Điểm điều chỉnh hoặc RequestKey không hợp lệ.");
        await db.Query("""
            SET XACT_ABORT ON; BEGIN TRAN;
            DECLARE @balance int;
            SELECT @balance=LoyaltyPoints FROM dbo.Customers WITH(UPDLOCK,HOLDLOCK) WHERE CustomerID=@id;
            IF @balance IS NULL THROW 51100,'Customer not found.',1;
            IF EXISTS(SELECT 1 FROM dbo.LoyaltyTransactions WHERE RequestKey=@key) BEGIN
                IF NOT EXISTS(SELECT 1 FROM dbo.LoyaltyTransactions WHERE RequestKey=@key AND CustomerID=@id AND Kind='Adjust' AND Points=@delta AND Note=@note)
                    THROW 51109,'Request key belongs to another adjustment.',1;
                COMMIT; RETURN;
            END;
            IF CONVERT(bigint,@balance)+@delta NOT BETWEEN 0 AND 2147483647 THROW 51100,'Invalid point balance.',1;
            INSERT dbo.LoyaltyTransactions(CustomerID,Kind,Points,RequestKey,Note) VALUES(@id,'Adjust',@delta,@key,@note);
            UPDATE dbo.Customers SET LoyaltyPoints=LoyaltyPoints+@delta WHERE CustomerID=@id;
            INSERT dbo.AuditLogs(ActorID,Action,EntityType,EntityID,Details) VALUES(@actor,'Loyalty.Adjust','Customers',@id,@note);
            COMMIT;
            """,("@actor",Actor),("@id",id),("@delta",input.Delta),("@key",input.RequestKey),("@note",input.Note));
        return NoContent();
    }
}
