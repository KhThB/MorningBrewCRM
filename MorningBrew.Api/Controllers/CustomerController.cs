using System.ComponentModel.DataAnnotations;
using System.Security.Claims;
using System.Text.Json;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using MorningBrew.Api.Infrastructure;
namespace MorningBrew.Api.Controllers;

[ApiController,Authorize(Roles="Customer"),Route("v1/me")]
public sealed class CustomerController(SqlStore db, PageReader pages):ControllerBase
{
    private int Actor=>int.Parse(User.FindFirstValue(ClaimTypes.NameIdentifier)!);
    private async Task<int> CustomerId()
    {
        var rows=await db.Query("SELECT CustomerID FROM dbo.Customers WHERE UserID=@actor",("@actor",Actor));
        if(rows.Count==0) throw new ApiException(404,"Chưa có hồ sơ khách hàng.");
        return (int)rows[0]["CustomerID"]!;
    }
    [HttpGet("profile")]
    public async Task<Dictionary<string,object?>> Profile()
    {
        var row = (await db.Query("""
            SELECT c.CustomerID,u.FullName,u.Email,u.Phone,c.DateOfBirth,c.Gender,c.LoyaltyPoints,
            c.CreatedAt,u.Version AS UserVersion,c.Version AS CustomerVersion
            FROM dbo.Customers c JOIN dbo.Users u ON u.UserID=c.UserID WHERE c.CustomerID=@id
            """,("@id",await CustomerId()))).Single();
        var etag = "\"" + Convert.ToHexString((byte[])row["UserVersion"]!) + "." + Convert.ToHexString((byte[])row["CustomerVersion"]!) + "\"";
        row.Remove("UserVersion"); row.Remove("CustomerVersion");
        row["Etag"] = etag; Response.Headers.ETag = etag;
        return ApiContract.Resource(row,"customers","CustomerID");
    }
    [HttpPatch("profile")]
    [ProducesResponseType(typeof(Dictionary<string,object?>),200)]
    [ProducesResponseType(typeof(ErrorResponse),412)]
    [ProducesResponseType(typeof(ErrorResponse),428)]
    public async Task<Dictionary<string,object?>> Update([FromBody] JsonElement input,
        [FromHeader(Name="If-Match")] string? ifMatch)
    {
        var patch = ProfilePatch.Parse(input);
        var versions = ProfilePatch.Versions(ifMatch ?? "");
        await db.Query("""
            SET XACT_ABORT ON; BEGIN TRAN;
            DECLARE @uv binary(8),@cv binary(8);
            SELECT @uv=Version FROM dbo.Users WITH(UPDLOCK,HOLDLOCK) WHERE UserID=@actor;
            SELECT @cv=Version FROM dbo.Customers WITH(UPDLOCK,HOLDLOCK) WHERE UserID=@actor;
            IF @uv IS NULL OR @cv IS NULL THROW 51104,'Profile not found.',1;
            IF @uv<>@uvExpected OR @cv<>@cvExpected THROW 51112,'Stale profile.',1;
            UPDATE dbo.Users SET FullName=CASE WHEN @hasName=1 THEN @name ELSE FullName END,
                Phone=CASE WHEN @hasPhone=1 THEN @phone ELSE Phone END WHERE UserID=@actor;
            UPDATE dbo.Customers SET DateOfBirth=CASE WHEN @hasBirth=1 THEN @birth ELSE DateOfBirth END,
                Gender=CASE WHEN @hasGender=1 THEN @gender ELSE Gender END WHERE UserID=@actor;
            COMMIT;
            """,("@actor",Actor),("@uvExpected",versions.User),("@cvExpected",versions.Customer),
            ("@hasName",patch.HasFullName),("@name",patch.FullName),("@hasPhone",patch.HasPhone),("@phone",patch.Phone),
            ("@hasBirth",patch.HasBirth),("@birth",patch.Birth),("@hasGender",patch.HasGender),("@gender",patch.Gender));
        return await Profile();
    }
    private Task<ApiPage> Page(string sql, string key, PageRequest page, string collection, int customerId)
        => pages.Read(sql,[key],true,page,$"{Actor}:{collection}",[("@id",customerId)],collection,key);
    [HttpGet("loyalty")]
    public async Task<ApiPage> Loyalty([FromQuery] PageRequest page)=>await Page(
        "SELECT LoyaltyTransactionID,Kind,Points,Note,CreatedAt FROM dbo.LoyaltyTransactions WHERE CustomerID=@id",
        "LoyaltyTransactionID",page,"loyaltyTransactions",await CustomerId());
    [HttpGet("wallet")]
    public async Task<ApiPage> Wallet([FromQuery] PageRequest page)=>await Page("""
        SELECT cp.CustomerPromotionID,p.Name,cp.Code,
        CASE WHEN cp.Status='Available' AND cp.ExpiresAt<=SYSUTCDATETIME() THEN 'Expired' ELSE cp.Status END AS Status,
        cp.DiscountType,cp.DiscountValue,cp.PointsSpent,cp.ExpiresAt
        FROM dbo.CustomerPromotions cp JOIN dbo.Promotions p ON p.PromotionID=cp.PromotionID
        WHERE cp.CustomerID=@id
        ""","CustomerPromotionID",page,"vouchers",await CustomerId());
    [HttpGet("promotions")]
    public async Task<ApiPage> Promotions([FromQuery] PageRequest page)=>await Page("""
        SELECT PromotionID,Name,Description,Scope,AreaID,BranchID,DiscountType,DiscountValue,MaxDiscount,MinSubtotal,PointsCost,EndsAt
        FROM dbo.Promotions WHERE IsActive=1 AND PointsCost IS NOT NULL AND StartsAt<=SYSUTCDATETIME() AND EndsAt>SYSUTCDATETIME()
        ""","PromotionID",page,"promotions",await CustomerId());
    public record RedeemInput(Guid RequestKey);
    [HttpPost("promotions/{id:int}:redeem")]
    public async Task<object> Redeem(int id,RedeemInput input)
    {
        if(input.RequestKey==Guid.Empty) throw new ApiException(400,"RequestKey không được rỗng.");
        var rows = await db.Procedure("dbo.IssueVoucher",("@ActorID",Actor),("@CustomerID",await CustomerId()),("@PromotionID",id),("@RequestKey",input.RequestKey));
        return await VouchersController.Read(db,Actor,(int)rows.Single()["CustomerPromotionID"]!);
    }
    [HttpGet("feedbacks")]
    public async Task<ApiPage> Feedbacks([FromQuery] PageRequest page)=>await Page(
        "SELECT FeedbackID,Type,Content,Rating,Status,ResponseContent,CreatedAt FROM dbo.Feedbacks WHERE CustomerID=@id",
        "FeedbackID",page,"feedbacks",await CustomerId());
    [HttpGet("feedbacks/{id:int}")]
    public async Task<Dictionary<string,object?>> Feedback(int id)
    {
        var row = (await db.Query("SELECT FeedbackID,Type,Content,Rating,Status,ResponseContent,CreatedAt FROM dbo.Feedbacks WHERE FeedbackID=@feedback AND CustomerID=@customer",
            ("@feedback",id),("@customer",await CustomerId()))).SingleOrDefault() ?? throw new ApiException(404,"Feedback not found.");
        return ApiContract.Resource(row,"feedbacks","FeedbackID");
    }
    public record FeedbackInput([Required,StringLength(4000,MinimumLength=1)] string Content,[Required] string Type,int? Rating,int? OrderId,int? BranchId,int? ProductId);
    [HttpPost("feedbacks")]
    public async Task<IActionResult> Feedback(FeedbackInput input)
    {
        var rows = await db.Procedure("dbo.SubmitFeedback",("@ActorID",Actor),("@CustomerID",await CustomerId()),
            ("@Content",input.Content),("@Type",input.Type),("@Rating",input.Rating),("@OrderID",input.OrderId),("@BranchID",input.BranchId),("@ProductID",input.ProductId));
        var id = (int)rows.Single()["FeedbackID"]!;
        return Created($"/v1/me/feedbacks/{id}",await Feedback(id));
    }
    [HttpGet("surveys")]
    public async Task<ApiPage> Surveys([FromQuery] PageRequest page)=>await Page("""
        SELECT s.SurveyID,s.Title,s.Description,s.Status,s.StartsAt,s.EndsAt,r.CompletedAt
        FROM dbo.SurveyRecipients r JOIN dbo.Surveys s ON s.SurveyID=r.SurveyID WHERE r.CustomerID=@id
        ""","SurveyID",page,"surveys",await CustomerId());
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
    [HttpPost("surveys/{id:int}:submit")]
    public async Task<IActionResult> Submit(int id,AnswersInput input)
    {
        await db.Procedure("dbo.SubmitSurvey",("@ActorID",Actor),("@CustomerID",await CustomerId()),("@SurveyID",id),
            ("@Answers",JsonSerializer.Serialize(input.Answers,new JsonSerializerOptions(JsonSerializerDefaults.Web))));
        return NoContent();
    }
    [HttpGet("preferences")]
    public async Task<ApiPage> Preferences([FromQuery] PageRequest page)=>await Page("SELECT p.ProductID,p.Name FROM dbo.CustomerPreferences cp JOIN dbo.Products p ON p.ProductID=cp.ProductID WHERE cp.CustomerID=@id","ProductID",page,"preferences",await CustomerId());
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
