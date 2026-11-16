using Microsoft.AspNetCore.Mvc;
using MorningBrew.Api.Infrastructure;
namespace MorningBrew.Api.Controllers;

// [WEB-11] Ưu đãi: sửa chương trình đã tạo và cấp voucher hàng loạt (theo danh sách khách hoặc nhóm khách,
// cùng tiêu chí với mời khảo sát hàng loạt). Voucher đã cấp giữ nguyên điều kiện lúc cấp (TR_CustomerPromotions_Terms).
public sealed partial class AdminController
{
    [HttpPut("promotions/{id:int}")]
    public async Task<Dictionary<string, object?>> UpdatePromotion(int id, PromotionInput input)
    {
        if (input.EndsAt <= input.StartsAt) throw new ApiException(400, "Ngày kết thúc phải sau ngày bắt đầu.");
        if (input.DiscountType is not ("Amount" or "Percent")) throw new ApiException(400, "Hình thức giảm không hợp lệ.");
        await db.Query("""
            SET XACT_ABORT ON; BEGIN TRAN;
            DECLARE @issued int;
            IF NOT EXISTS(SELECT 1 FROM dbo.Promotions WITH(UPDLOCK,HOLDLOCK) WHERE PromotionID=@id) THROW 51104,N'Không tìm thấy ưu đãi.',1;
            IF NOT EXISTS(SELECT 1 FROM dbo.Promotions WHERE PromotionID=@id AND dbo.CanScope(@actor,Scope,AreaID,BranchID,'Promotions.Manage')=1) THROW 51103,'Promotion outside scope.',1;
            SET @issued=(SELECT COUNT(*) FROM dbo.CustomerPromotions WHERE PromotionID=@id);
            IF @limit<@issued THROW 51120,N'Số lượng toàn chương trình không được nhỏ hơn số voucher đã cấp.',1;
            UPDATE dbo.Promotions SET Name=@name,Description=@description,DiscountType=@type,DiscountValue=@value,MaxDiscount=@max,
                MinSubtotal=@min,StartsAt=@start,EndsAt=@end,PointsCost=@points,TotalLimit=@limit,PerCustomerLimit=@per WHERE PromotionID=@id;
            INSERT dbo.AuditLogs(ActorID,Action,EntityType,EntityID,Details) VALUES(@actor,'Promotion.Update','Promotions',@id,CONCAT(N'đã cấp ',@issued));
            COMMIT;
            """, ("@id", id), ("@actor", Actor), ("@name", input.Name.Trim()), ("@description", input.Description), ("@type", input.DiscountType),
            ("@value", input.DiscountValue), ("@max", input.MaxDiscount), ("@min", input.MinSubtotal), ("@start", input.StartsAt.UtcDateTime),
            ("@end", input.EndsAt.UtcDateTime), ("@points", input.PointsCost), ("@limit", input.TotalLimit), ("@per", input.PerCustomerLimit));
        return await ReadResource("promotions", id);
    }

    public record BulkIssueInput(int[]? CustomerIds, bool All = false, int? AgeGroup = null, string? Gender = null, int? ProductId = null,
        string? Segment = null, // [CRM-02] cấp theo nhóm hành vi mua (vd khách lâu không quay lại)
        string? Tier = null); // [CRM-01] cấp theo hạng thành viên (vd tặng voucher cho hạng Vàng)
    [HttpPost("promotions/{id:int}:issueBulk")]
    public async Task<object> IssueBulk(int id, BulkIssueInput input)
    {
        var targets = await BulkTargets(new BulkInviteInput(input.CustomerIds, input.All, input.AgeGroup, input.Gender, input.ProductId, input.Segment, input.Tier));
        int issued = 0, skipped = 0;
        string? reason = null;
        foreach (var customer in targets)
        {
            try
            {
                await db.Procedure("dbo.IssueVoucher", ("@ActorID", Actor), ("@PromotionID", id), ("@CustomerID", customer), ("@RequestKey", Guid.NewGuid()));
                issued++;
            }
            catch (Microsoft.Data.SqlClient.SqlException e) when (e.Number is 51100 or 51103)
            {
                // Khách đã đủ lượt / chương trình hết lượt / ngoài thời gian / khách ngoài phạm vi: bỏ qua khách đó, ghi lại lý do đầu tiên.
                skipped++; reason ??= OrderQueries.Translate(e.Message);
            }
        }
        if (targets.Count > 0)
            await db.Query("INSERT dbo.AuditLogs(ActorID,Action,EntityType,EntityID,Details) VALUES(@actor,'Promotion.IssueBulk','Promotions',@id,@details)",
                ("@actor", Actor), ("@id", id), ("@details", $"matched={targets.Count};issued={issued};skipped={skipped}"));
        return new { matched = targets.Count, issued, skipped, reason };
    }
}
