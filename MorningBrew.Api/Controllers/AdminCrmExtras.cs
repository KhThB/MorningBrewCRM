using System.Text.Json;
using Microsoft.AspNetCore.Mvc;
using MorningBrew.Api.Infrastructure;
namespace MorningBrew.Api.Controllers;

// Giai đoạn 3 – hoàn thiện CRM 4.1: lọc phản hồi, mời khảo sát hàng loạt theo danh sách hoặc nhóm khách.
public sealed partial class AdminController
{
    private static readonly string[] FeedbackStatuses = ["New", "Processing", "Resolved"];

    [HttpGet("feedbacks")]
    public async Task<ApiPage> ListFeedbacks([FromQuery] PageRequest page, string? status = null, string? q = null)
    {
        if (!string.IsNullOrEmpty(status) && !FeedbackStatuses.Contains(status))
            throw new ApiException(400, "Trạng thái phản hồi không hợp lệ.");
        const string sql = """
            SELECT f.FeedbackID,u.FullName,f.Type,f.Content,f.Rating,f.Status,f.ResponseContent,f.CreatedAt
            FROM dbo.Feedbacks f JOIN dbo.Customers c ON c.CustomerID=f.CustomerID JOIN dbo.Users u ON u.UserID=c.UserID
            WHERE ((f.BranchID IS NULL AND dbo.CanScope(@actor,'Chain',NULL,NULL,'Feedbacks.Manage')=1)
                OR dbo.CanBranch(@actor,f.BranchID,'Feedbacks.Manage')=1)
            AND (@status='' OR f.Status=@status)
            AND (@q='' OR f.Content LIKE @search OR u.FullName LIKE @search)
            """;
        return await pages.Read(sql, ["FeedbackID"], true, page,
            JsonSerializer.Serialize(new { Actor, section = "feedbacks", status = status ?? "", q = q ?? "" }),
            [("@actor", Actor), ("@status", status ?? ""), ("@q", q ?? ""), ("@search", "%" + (q ?? "") + "%")],
            "feedbacks", "FeedbackID");
    }

    // Mời nhiều khách cùng lúc. Chọn một trong hai: danh sách CustomerIds, hoặc nhóm khách (tất cả /
    // nhóm tuổi / giới tính / thích một sản phẩm). Khách đã được mời thì bỏ qua (InviteSurvey idempotent).
    public record BulkInviteInput(int[]? CustomerIds, bool All = false, int? AgeGroup = null, string? Gender = null, int? ProductId = null);
    [HttpPost("surveys/{id:int}:inviteBulk")]
    public async Task<object> InviteBulk(int id, BulkInviteInput input)
    {
        List<int> targets;
        if (input.CustomerIds is { Length: > 0 })
        {
            if (input.CustomerIds.Length > 500) throw new ApiException(400, "Tối đa 500 khách mỗi lần mời.");
            targets = input.CustomerIds.Distinct().ToList();
        }
        else
        {
            if (!input.All && input.AgeGroup is null && string.IsNullOrEmpty(input.Gender) && input.ProductId is null)
                throw new ApiException(400, "Chọn khách hàng hoặc ít nhất một tiêu chí nhóm.");
            if (input.AgeGroup is < 1 or > 6) throw new ApiException(400, "Nhóm tuổi không hợp lệ.");
            if (input.Gender is not (null or "" or "Male" or "Female" or "Other" or "Undisclosed"))
                throw new ApiException(400, "Giới tính không hợp lệ.");
            var today = DateOnly.FromDateTime(DateTime.UtcNow.AddHours(7)).ToDateTime(TimeOnly.MinValue);
            var rows = await db.Query($"""
                {ScopedCustomers}
                SELECT x.CustomerID FROM grouped x JOIN dbo.Customers c ON c.CustomerID=x.CustomerID
                JOIN dbo.Users u ON u.UserID=c.UserID
                WHERE u.IsActive=1 AND (@age IS NULL OR x.GroupOrder=@age)
                AND (@gender='' OR COALESCE(x.Gender,'Undisclosed')=@gender)
                AND (@product IS NULL OR EXISTS(SELECT 1 FROM dbo.CustomerPreferences p WHERE p.CustomerID=x.CustomerID AND p.ProductID=@product))
                """, ("@actor", Actor), ("@today", today), ("@age", input.AgeGroup), ("@gender", input.Gender ?? ""), ("@product", input.ProductId));
            targets = rows.Select(r => (int)r["CustomerID"]!).ToList();
        }
        if (targets.Count == 0) return new { matched = 0, invited = 0, alreadyInvited = 0 };
        var before = await InvitedCount(id);
        foreach (var customer in targets)
            await db.Procedure("dbo.InviteSurvey", ("@ActorID", Actor), ("@SurveyID", id), ("@CustomerID", customer));
        var invited = await InvitedCount(id) - before;
        await db.Query("INSERT dbo.AuditLogs(ActorID,Action,EntityType,EntityID,Details) VALUES(@actor,'Survey.InviteBulk','Surveys',@id,@details)",
            ("@actor", Actor), ("@id", id), ("@details", $"matched={targets.Count};invited={invited}"));
        return new { matched = targets.Count, invited, alreadyInvited = targets.Count - invited };
    }
    private async Task<int> InvitedCount(int survey)
        => (int)(await db.Query("SELECT COUNT(*) AS N FROM dbo.SurveyRecipients WHERE SurveyID=@id", ("@id", survey))).Single()["N"]!;
}
