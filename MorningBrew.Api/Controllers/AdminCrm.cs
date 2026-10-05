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
        [Required] string Scope,int? AreaId,int? BranchId,DateTimeOffset StartsAt,DateTimeOffset EndsAt,
        [Required,MinLength(1),MaxLength(40)] QuestionInput[] Questions);

    [HttpPost("surveys")]
    public async Task<object> CreateSurvey(SurveyInput input)
    {
        if (input.EndsAt <= input.StartsAt) throw new ApiException(400,"Ngày kết thúc phải sau ngày bắt đầu.");
        foreach (var q in input.Questions)
        {
            if (q.Type is not ("Text" or "Rating" or "SingleChoice" or "MultipleChoice") ||
                string.IsNullOrWhiteSpace(q.Text) || q.Text.Length>2000 ||
                (q.Type=="Rating" && (q.RatingMin is null || q.RatingMax is null || q.RatingMin>=q.RatingMax)) ||
                (q.Type is "SingleChoice" or "MultipleChoice" && (q.Options is null || q.Options.Length<2 || q.Options.Length>30 || q.Options.Any(x=>string.IsNullOrWhiteSpace(x)||x.Length>500))))
                throw new ApiException(400,"Câu hỏi hoặc lựa chọn không hợp lệ.");
        }
        var questions = input.Questions.Select((q,i)=>new { text=q.Text,type=q.Type,required=q.IsRequired,position=i,
            min=q.Type=="Rating"?q.RatingMin:null,max=q.Type=="Rating"?q.RatingMax:null,options=q.Options });
        return await db.Query("""
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
            """, ("@actor",Actor),("@scope",input.Scope),("@area",input.AreaId),("@branch",input.BranchId),
            ("@title",input.Title),("@description",input.Description),("@start",input.StartsAt.UtcDateTime),("@end",input.EndsAt.UtcDateTime),
            ("@questions",JsonSerializer.Serialize(questions)));
    }
    [HttpPost("surveys/{id:int}/publish")]
    public async Task<IActionResult> Publish(int id)
    {
        await db.Procedure("dbo.PublishSurvey",("@ActorID",Actor),("@SurveyID",id)); return NoContent();
    }
    [HttpPost("surveys/{id:int}/close")]
    public async Task<IActionResult> Close(int id)
    {
        await db.Query("""
            UPDATE dbo.Surveys SET Status='Closed' WHERE SurveyID=@id AND Status='Open'
            AND dbo.CanScope(@actor,Scope,AreaID,BranchID,'Surveys.Manage')=1;
            IF @@ROWCOUNT=0 THROW 51103,'Survey not open or outside scope.',1;
            """,("@id",id),("@actor",Actor)); return NoContent();
    }
    public record CustomerInput([Range(1,int.MaxValue)] int CustomerId);
    [HttpPost("surveys/{id:int}/invite")]
    public async Task<IActionResult> Invite(int id,CustomerInput input)
    {
        await db.Procedure("dbo.InviteSurvey",("@ActorID",Actor),("@SurveyID",id),("@CustomerID",input.CustomerId)); return NoContent();
    }
    [HttpGet("surveys/{id:int}/results")]
    public async Task<object> SurveyResults(int id)
    {
        await db.Query("IF NOT EXISTS(SELECT 1 FROM dbo.Surveys WHERE SurveyID=@id AND dbo.CanScope(@actor,Scope,AreaID,BranchID,'Surveys.Manage')=1) THROW 51103,'Scope denied.',1;",("@actor",Actor),("@id",id));
        return await db.Query("""
            SELECT q.QuestionID,q.Text,q.Type,r.CustomerID,r.TextValue,r.RatingValue,
            STRING_AGG(CONVERT(nvarchar(max),o.Text),N', ') AS SelectedOptions
            FROM dbo.SurveyQuestions q LEFT JOIN dbo.SurveyResponses r ON r.QuestionID=q.QuestionID
            LEFT JOIN dbo.SurveyAnswerOptions a ON a.ResponseID=r.ResponseID LEFT JOIN dbo.SurveyQuestionOptions o ON o.OptionID=a.OptionID
            WHERE q.SurveyID=@id GROUP BY q.QuestionID,q.Text,q.Type,r.CustomerID,r.TextValue,r.RatingValue
            ORDER BY q.QuestionID,r.CustomerID
            """,("@id",id));
    }
    public record PromotionInput([Required,MaxLength(150)] string Name,[MaxLength(2000)] string? Description,
        [Required] string Scope,int? AreaId,int? BranchId,[Required] string DiscountType,decimal DiscountValue,
        decimal? MaxDiscount,decimal MinSubtotal,DateTimeOffset StartsAt,DateTimeOffset EndsAt,int? PointsCost,int TotalLimit,int PerCustomerLimit);
    [HttpPost("promotions")]
    public Task<List<Dictionary<string,object?>>> CreatePromotion(PromotionInput input) => db.Query("""
        SET XACT_ABORT ON; BEGIN TRAN;
        IF dbo.CanScope(@actor,@scope,@area,@branch,'Promotions.Manage')=0 THROW 51103,'Scope denied.',1;
        INSERT dbo.Promotions(Name,Description,CreatedBy,Scope,AreaID,BranchID,DiscountType,DiscountValue,MaxDiscount,MinSubtotal,StartsAt,EndsAt,PointsCost,TotalLimit,PerCustomerLimit)
        VALUES(@name,@description,@actor,@scope,@area,@branch,@type,@value,@max,@min,@start,@end,@points,@limit,@per);
        DECLARE @id int=SCOPE_IDENTITY();
        INSERT dbo.AuditLogs(ActorID,BranchID,Action,EntityType,EntityID) VALUES(@actor,@branch,'Promotion.Create','Promotions',@id);
        COMMIT; SELECT @id AS PromotionID;
        """,("@actor",Actor),("@scope",input.Scope),("@area",input.AreaId),("@branch",input.BranchId),
        ("@name",input.Name),("@description",input.Description),("@type",input.DiscountType),("@value",input.DiscountValue),
        ("@max",input.MaxDiscount),("@min",input.MinSubtotal),("@start",input.StartsAt.UtcDateTime),("@end",input.EndsAt.UtcDateTime),
        ("@points",input.PointsCost),("@limit",input.TotalLimit),("@per",input.PerCustomerLimit));
    [HttpPut("promotions/{id:int}/active")]
    public async Task<IActionResult> PromotionActive(int id,ActiveInput input)
    {
        await db.Query("""
            UPDATE dbo.Promotions SET IsActive=@active WHERE PromotionID=@id AND dbo.CanScope(@actor,Scope,AreaID,BranchID,'Promotions.Manage')=1;
            IF @@ROWCOUNT=0 THROW 51103,'Promotion outside scope.',1;
            """,("@actor",Actor),("@id",id),("@active",input.IsActive)); return NoContent();
    }
    public record VoucherInput([Range(1,int.MaxValue)] int CustomerId,Guid RequestKey);
    [HttpPost("promotions/{id:int}/issue")]
    public Task<List<Dictionary<string,object?>>> Issue(int id,VoucherInput input)
    {
        if(input.RequestKey==Guid.Empty) throw new ApiException(400,"RequestKey không được rỗng.");
        return db.Procedure("dbo.IssueVoucher",("@ActorID",Actor),("@PromotionID",id),("@CustomerID",input.CustomerId),("@RequestKey",input.RequestKey));
    }
    public record AdjustInput(int Delta,[Required,StringLength(300,MinimumLength=3)] string Note,Guid RequestKey);
    [HttpPost("customers/{id:int}/points")]
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
