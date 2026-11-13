using Microsoft.AspNetCore.Mvc;
using MorningBrew.Api.Infrastructure;
namespace MorningBrew.Api.Controllers;

// Yêu cầu CRM 4.1: xóa khách hàng, báo cáo độ tuổi/sở thích, thống kê kết quả khảo sát.
// Yêu cầu II.2: xóa account. Xóa là xóa mềm (dbo.SoftDeleteUser, database/sql/03_crm_updates.sql).
public sealed partial class AdminController
{
    [HttpDelete("accounts/{id:int}")]
    public async Task<IActionResult> DeleteAccount(int id)
    {
        await RequireOwner();
        await db.Procedure("dbo.SoftDeleteUser", ("@ActorID", Actor), ("@UserID", id));
        return NoContent();
    }

    [HttpDelete("customers/{id:int}")]
    public async Task<IActionResult> DeleteCustomer(int id)
    {
        await RequirePermission("Customers.Manage");
        var user = (await db.Query("""
            SELECT c.UserID FROM dbo.Customers c JOIN dbo.Users u ON u.UserID=c.UserID
            WHERE c.CustomerID=@id AND u.DeletedAt IS NULL
            """, ("@id", id))).SingleOrDefault() ?? throw new ApiException(404, "Không tìm thấy khách hàng.");
        await db.Procedure("dbo.SoftDeleteUser", ("@ActorID", Actor), ("@UserID", user["UserID"]));
        return NoContent();
    }

    // Yêu cầu CRM 4.1.3: khóa/mở khóa tài khoản khách hàng (quản lý làm được, không cần quyền quản trị).
    [HttpPut("customers/{id:int}/active")]
    public async Task<IActionResult> CustomerActive(int id, ActiveInput input)
    {
        await RequirePermission("Customers.Manage");
        await db.Query("""
            SET XACT_ABORT ON; BEGIN TRAN;
            DECLARE @user int=(SELECT c.UserID FROM dbo.Customers c JOIN dbo.Users u ON u.UserID=c.UserID
                WHERE c.CustomerID=@id AND u.DeletedAt IS NULL);
            IF @user IS NULL THROW 51104,'Customer not found.',1;
            UPDATE dbo.Users SET IsActive=@active WHERE UserID=@user;
            IF @active=0 UPDATE dbo.UserSessions SET RevokedAt=SYSUTCDATETIME() WHERE UserID=@user AND RevokedAt IS NULL;
            INSERT dbo.AuditLogs(ActorID,Action,EntityType,EntityID) VALUES(@actor,'Customer.Status','Users',@user);
            COMMIT;
            """, ("@id", id), ("@active", input.IsActive), ("@actor", Actor));
        return NoContent();
    }

    // Khách trong phạm vi người xem, chưa bị xóa, kèm tuổi tròn tính theo ngày hiện tại (giờ Việt Nam).
    private const string ScopedCustomers = $"""
        WITH base AS (
            SELECT c.CustomerID,c.DateOfBirth,c.Gender FROM dbo.Customers c JOIN dbo.Users u ON u.UserID=c.UserID
            WHERE u.DeletedAt IS NULL AND {CustomerScope}),
        aged AS (
            SELECT *, CASE WHEN DateOfBirth IS NULL THEN NULL ELSE DATEDIFF(year,DateOfBirth,@today)
                - CASE WHEN DATEADD(year,DATEDIFF(year,DateOfBirth,@today),DateOfBirth)>@today THEN 1 ELSE 0 END END AS Age
            FROM base),
        grouped AS (
            SELECT CustomerID,Gender,Age,CASE WHEN Age IS NULL THEN 6 WHEN Age<18 THEN 1 WHEN Age<25 THEN 2
                WHEN Age<35 THEN 3 WHEN Age<45 THEN 4 ELSE 5 END AS GroupOrder FROM aged)
        """;
    private const string AgeGroupNames = """
        (VALUES(1,N'Dưới 18'),(2,N'18–24'),(3,N'25–34'),(4,N'35–44'),(5,N'45 trở lên'),(6,N'Chưa cung cấp')) g(GroupOrder,AgeGroup)
        """;

    [HttpGet("reports/customers")]
    public async Task<object> CustomerReport()
    {
        var today = DateOnly.FromDateTime(DateTime.UtcNow.AddHours(7));
        (string, object?)[] args = [("@actor", Actor), ("@today", today.ToDateTime(TimeOnly.MinValue))];
        var totals = (await db.Query($"""
            {ScopedCustomers}
            SELECT (SELECT COUNT(*) FROM grouped) AS Customers,
            (SELECT CAST(AVG(CAST(Age AS decimal(9,2))) AS decimal(9,1)) FROM grouped) AS AverageAge,
            (SELECT COUNT(DISTINCT p.CustomerID) FROM dbo.CustomerPreferences p JOIN grouped x ON x.CustomerID=p.CustomerID) AS WithPreferences
            """, args)).Single();
        var ageGroups = await db.Query($"""
            {ScopedCustomers}
            SELECT g.GroupOrder,g.AgeGroup,COUNT(x.CustomerID) AS Customers
            FROM {AgeGroupNames} LEFT JOIN grouped x ON x.GroupOrder=g.GroupOrder
            GROUP BY g.GroupOrder,g.AgeGroup ORDER BY g.GroupOrder
            """, args);
        var genders = await db.Query($"""
            {ScopedCustomers}
            SELECT COALESCE(Gender,'Undisclosed') AS Gender,COUNT(*) AS Customers FROM grouped
            GROUP BY COALESCE(Gender,'Undisclosed') ORDER BY Customers DESC
            """, args);
        var products = await db.Query($"""
            {ScopedCustomers}
            SELECT TOP(10) p.ProductID,p.Name AS Product,cat.Name AS Category,COUNT(*) AS Customers
            FROM grouped x JOIN dbo.CustomerPreferences cp ON cp.CustomerID=x.CustomerID
            JOIN dbo.Products p ON p.ProductID=cp.ProductID JOIN dbo.Categories cat ON cat.CategoryID=p.CategoryID
            GROUP BY p.ProductID,p.Name,cat.Name ORDER BY Customers DESC,p.Name
            """, args);
        var categories = await db.Query($"""
            {ScopedCustomers}
            SELECT cat.Name AS Category,COUNT(DISTINCT x.CustomerID) AS Customers
            FROM grouped x JOIN dbo.CustomerPreferences cp ON cp.CustomerID=x.CustomerID
            JOIN dbo.Products p ON p.ProductID=cp.ProductID JOIN dbo.Categories cat ON cat.CategoryID=p.CategoryID
            GROUP BY cat.Name ORDER BY Customers DESC,cat.Name
            """, args);
        // Món được thích nhiều nhất trong từng nhóm tuổi.
        var favoriteByAge = await db.Query($"""
            {ScopedCustomers}, ranked AS (
                SELECT x.GroupOrder,p.Name AS Product,COUNT(*) AS Customers,
                ROW_NUMBER() OVER(PARTITION BY x.GroupOrder ORDER BY COUNT(*) DESC,p.Name) AS Position
                FROM grouped x JOIN dbo.CustomerPreferences cp ON cp.CustomerID=x.CustomerID
                JOIN dbo.Products p ON p.ProductID=cp.ProductID GROUP BY x.GroupOrder,p.Name)
            SELECT g.GroupOrder,g.AgeGroup,r.Product,r.Customers FROM {AgeGroupNames}
            JOIN ranked r ON r.GroupOrder=g.GroupOrder AND r.Position=1 ORDER BY g.GroupOrder
            """, args);
        return new { totals, ageGroups, genders, products, categories, favoriteByAge };
    }

    [HttpGet("surveys/{id:int}/summary")]
    public async Task<object> SurveySummary(int id)
    {
        var survey = (await db.Query("""
            SELECT s.SurveyID,s.Title,s.Status,s.StartsAt,s.EndsAt,
            (SELECT COUNT(*) FROM dbo.SurveyRecipients r WHERE r.SurveyID=s.SurveyID) AS Invited,
            (SELECT COUNT(*) FROM dbo.SurveyRecipients r WHERE r.SurveyID=s.SurveyID AND r.CompletedAt IS NOT NULL) AS Completed,
            (SELECT COUNT(DISTINCT CustomerID) FROM dbo.SurveyResponses r WHERE r.SurveyID=s.SurveyID) AS Respondents
            FROM dbo.Surveys s WHERE s.SurveyID=@id AND dbo.CanScope(@actor,s.Scope,s.AreaID,s.BranchID,'Surveys.Manage')=1
            """, ("@id", id), ("@actor", Actor))).SingleOrDefault()
            ?? throw new ApiException(404, "Không tìm thấy khảo sát trong phạm vi của bạn.");
        var questions = await db.Query("""
            SELECT q.QuestionID,q.DisplayOrder,q.Text,q.Type,q.RatingMin,q.RatingMax,
            COUNT(r.ResponseID) AS Responses,CAST(AVG(CAST(r.RatingValue AS decimal(9,2))) AS decimal(9,2)) AS AverageRating
            FROM dbo.SurveyQuestions q LEFT JOIN dbo.SurveyResponses r ON r.QuestionID=q.QuestionID
            WHERE q.SurveyID=@id GROUP BY q.QuestionID,q.DisplayOrder,q.Text,q.Type,q.RatingMin,q.RatingMax
            ORDER BY q.DisplayOrder,q.QuestionID
            """, ("@id", id));
        var options = await db.Query("""
            SELECT o.QuestionID,o.OptionID,o.Text,COUNT(a.ResponseID) AS Votes
            FROM dbo.SurveyQuestionOptions o JOIN dbo.SurveyQuestions q ON q.QuestionID=o.QuestionID
            LEFT JOIN dbo.SurveyAnswerOptions a ON a.OptionID=o.OptionID
            WHERE q.SurveyID=@id GROUP BY o.QuestionID,o.OptionID,o.Text,o.DisplayOrder
            ORDER BY o.QuestionID,o.DisplayOrder,o.OptionID
            """, ("@id", id));
        var ratings = await db.Query("""
            SELECT r.QuestionID,r.RatingValue,COUNT(*) AS Votes FROM dbo.SurveyResponses r
            WHERE r.SurveyID=@id AND r.RatingValue IS NOT NULL GROUP BY r.QuestionID,r.RatingValue
            ORDER BY r.QuestionID,r.RatingValue
            """, ("@id", id));
        var texts = await db.Query("""
            SELECT TOP(100) r.QuestionID,r.TextValue,r.SubmittedAt FROM dbo.SurveyResponses r
            WHERE r.SurveyID=@id AND NULLIF(LTRIM(RTRIM(r.TextValue)),N'') IS NOT NULL ORDER BY r.SubmittedAt DESC
            """, ("@id", id));
        return new { survey, questions, options, ratings, texts };
    }
}
