using System.ComponentModel.DataAnnotations;
using System.Security.Claims;
using System.Text.Json;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using MorningBrew.Api.Infrastructure;
namespace MorningBrew.Api.Controllers;

[ApiController,Authorize(Roles="Customer"),Route("api/customer")]
public sealed class CustomerController(SqlStore db):ControllerBase
{
    private int Actor=>int.Parse(User.FindFirstValue(ClaimTypes.NameIdentifier)!);
    private async Task<int> CustomerId()
    {
        var rows=await db.Query("SELECT CustomerID FROM dbo.Customers WHERE UserID=@actor",("@actor",Actor));
        if(rows.Count==0) throw new ApiException(404,"Chưa có hồ sơ khách hàng.");
        return (int)rows[0]["CustomerID"]!;
    }
    [HttpGet("profile")]
    public async Task<object> Profile()=> (await db.Query("SELECT c.CustomerID,u.FullName,u.Email,u.Phone,c.DateOfBirth,c.Gender,c.LoyaltyPoints FROM dbo.Customers c JOIN dbo.Users u ON u.UserID=c.UserID WHERE c.CustomerID=@id",("@id",await CustomerId()))).Single();
    public record ProfileInput([Required,MaxLength(120)] string FullName,[MaxLength(20)] string? Phone,DateTime? DateOfBirth,string? Gender);
    [HttpPut("profile")]
    public async Task<IActionResult> Update(ProfileInput input)
    {
        await db.Query("""
            SET XACT_ABORT ON; BEGIN TRAN;
            UPDATE dbo.Users SET FullName=@name,Phone=@phone WHERE UserID=@actor;
            UPDATE dbo.Customers SET DateOfBirth=@birth,Gender=@gender WHERE UserID=@actor;
            COMMIT;
            """,("@actor",Actor),("@name",input.FullName),("@phone",input.Phone),("@birth",input.DateOfBirth),("@gender",input.Gender));
        return NoContent();
    }
    [HttpGet("loyalty")]
    public async Task<object> Loyalty()=> await db.Query("SELECT TOP(200) LoyaltyTransactionID,Kind,Points,Note,CreatedAt FROM dbo.LoyaltyTransactions WHERE CustomerID=@id ORDER BY LoyaltyTransactionID DESC",("@id",await CustomerId()));
    [HttpGet("wallet")]
    public async Task<object> Wallet()=> await db.Query("""
        SELECT TOP(200) cp.CustomerPromotionID,p.Name,cp.Code,
        CASE WHEN cp.Status='Available' AND cp.ExpiresAt<=SYSUTCDATETIME() THEN 'Expired' ELSE cp.Status END AS Status,
        cp.DiscountType,cp.DiscountValue,cp.PointsSpent,cp.ExpiresAt
        FROM dbo.CustomerPromotions cp JOIN dbo.Promotions p ON p.PromotionID=cp.PromotionID
        WHERE cp.CustomerID=@id ORDER BY cp.CustomerPromotionID DESC
        """,("@id",await CustomerId()));
    [HttpGet("promotions")]
    public Task<List<Dictionary<string,object?>>> Promotions()=>db.Query("""
        SELECT TOP(200) PromotionID,Name,Description,Scope,AreaID,BranchID,DiscountType,DiscountValue,MaxDiscount,MinSubtotal,PointsCost,EndsAt
        FROM dbo.Promotions WHERE IsActive=1 AND PointsCost IS NOT NULL AND StartsAt<=SYSUTCDATETIME() AND EndsAt>SYSUTCDATETIME()
        ORDER BY PromotionID DESC
        """);
    public record RedeemInput(Guid RequestKey);
    [HttpPost("promotions/{id:int}/redeem")]
    public async Task<object> Redeem(int id,RedeemInput input)
    {
        if(input.RequestKey==Guid.Empty) throw new ApiException(400,"RequestKey không được rỗng.");
        return await db.Procedure("dbo.IssueVoucher",("@ActorID",Actor),("@CustomerID",await CustomerId()),("@PromotionID",id),("@RequestKey",input.RequestKey));
    }
    [HttpGet("feedbacks")]
    public async Task<object> Feedbacks()=>await db.Query("SELECT TOP(200) FeedbackID,Type,Content,Rating,Status,ResponseContent,CreatedAt FROM dbo.Feedbacks WHERE CustomerID=@id ORDER BY FeedbackID DESC",("@id",await CustomerId()));
    public record FeedbackInput([Required,StringLength(4000,MinimumLength=1)] string Content,[Required] string Type,int? Rating,int? OrderId,int? BranchId,int? ProductId);
    [HttpPost("feedbacks")]
    public async Task<object> Feedback(FeedbackInput input)=>await db.Procedure("dbo.SubmitFeedback",("@ActorID",Actor),("@CustomerID",await CustomerId()),
        ("@Content",input.Content),("@Type",input.Type),("@Rating",input.Rating),("@OrderID",input.OrderId),("@BranchID",input.BranchId),("@ProductID",input.ProductId));
    [HttpGet("surveys")]
    public async Task<object> Surveys()=>await db.Query("""
        SELECT s.SurveyID,s.Title,s.Description,s.Status,s.StartsAt,s.EndsAt,r.CompletedAt
        FROM dbo.SurveyRecipients r JOIN dbo.Surveys s ON s.SurveyID=r.SurveyID WHERE r.CustomerID=@id
        ORDER BY s.SurveyID DESC
        """,("@id",await CustomerId()));
    [HttpGet("surveys/{id:int}")]
    public async Task<object> Survey(int id)
    {
        await db.Query("IF NOT EXISTS(SELECT 1 FROM dbo.SurveyRecipients WHERE SurveyID=@survey AND CustomerID=@id) THROW 51103,'Not invited.',1;",("@survey",id),("@id",await CustomerId()));
        return new {
            questions=await db.Query("SELECT QuestionID,Text,Type,IsRequired,RatingMin,RatingMax FROM dbo.SurveyQuestions WHERE SurveyID=@id ORDER BY DisplayOrder",("@id",id)),
            options=await db.Query("SELECT o.OptionID,o.QuestionID,o.Text FROM dbo.SurveyQuestionOptions o JOIN dbo.SurveyQuestions q ON q.QuestionID=o.QuestionID WHERE q.SurveyID=@id ORDER BY o.DisplayOrder",("@id",id)) };
    }
    public record AnswerInput(int QuestionId,[MaxLength(4000)] string? Text,int? Rating,[MaxLength(30)] int[]? OptionIds);
    public record AnswersInput([Required,MaxLength(40)] AnswerInput[] Answers);
    [HttpPost("surveys/{id:int}/submit")]
    public async Task<IActionResult> Submit(int id,AnswersInput input)
    {
        await db.Procedure("dbo.SubmitSurvey",("@ActorID",Actor),("@CustomerID",await CustomerId()),("@SurveyID",id),
            ("@Answers",JsonSerializer.Serialize(input.Answers,new JsonSerializerOptions(JsonSerializerDefaults.Web))));
        return NoContent();
    }
    [HttpGet("preferences")]
    public async Task<object> Preferences()=>await db.Query("SELECT p.ProductID,p.Name FROM dbo.CustomerPreferences cp JOIN dbo.Products p ON p.ProductID=cp.ProductID WHERE cp.CustomerID=@id",("@id",await CustomerId()));
    public record PreferencesInput([Required,MaxLength(100)] int[] ProductIds);
    [HttpPut("preferences")]
    public async Task<IActionResult> Preferences(PreferencesInput input)
    {
        await db.Query("""
            SET XACT_ABORT ON; BEGIN TRAN;
            DELETE dbo.CustomerPreferences WHERE CustomerID=@id;
            INSERT dbo.CustomerPreferences(CustomerID,ProductID) SELECT @id,CONVERT(int,value) FROM OPENJSON(@products) GROUP BY value;
            COMMIT;
            """,("@id",await CustomerId()),("@products",JsonSerializer.Serialize(input.ProductIds)));return NoContent();
    }
}
