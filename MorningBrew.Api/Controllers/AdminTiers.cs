using System.ComponentModel.DataAnnotations;
using Microsoft.AspNetCore.Mvc;
using MorningBrew.Api.Infrastructure;
namespace MorningBrew.Api.Controllers;

// [CRM-01] Hạng thành viên (dbo.MemberTiers, 01_schema.sql; dữ liệu mẫu 06_seed.sql): xem, thêm, sửa, xóa hạng.
// Hạng của khách tính trực tiếp trong view dbo.CustomerTiers theo chi tiêu 12 tháng, nên đổi mức ở đây áp dụng ngay.
// Quyền: Promotions.Manage (chương trình khách hàng thân thiết đi cùng ưu đãi).
public sealed partial class AdminController
{
    [HttpGet("tiers")]
    public async Task<ApiPage> ListTiers()
    {
        await RequirePermission("Promotions.Manage");
        var rows = await db.Query($"""
            SELECT t.TierID,t.Code,t.Name,t.MinSpent,t.Benefit,
            (SELECT COUNT(*) FROM dbo.CustomerTiers ct JOIN dbo.Customers c ON c.CustomerID=ct.CustomerID JOIN dbo.Users u ON u.UserID=c.UserID
             WHERE ct.TierID=t.TierID AND u.DeletedAt IS NULL AND {CustomerScope}) AS Customers
            FROM dbo.MemberTiers t ORDER BY t.MinSpent
            """, ("@actor", Actor));
        return new ApiPage(rows.Select(r => ApiContract.Resource(r, "tiers", "TierID")).ToList(), null);
    }

    public record TierInput([Required, StringLength(50, MinimumLength = 2)] string Name, [Range(0, 1000000000)] decimal MinSpent, [MaxLength(300)] string? Benefit);
    [HttpPost("tiers")]
    public async Task<IActionResult> CreateTier(TierInput input)
    {
        await RequirePermission("Promotions.Manage");
        var created = await db.Query("""
            SET XACT_ABORT ON; BEGIN TRAN;
            IF (SELECT COUNT(*) FROM dbo.MemberTiers WITH(UPDLOCK,HOLDLOCK))>=6 THROW 51120,N'Tối đa 6 hạng thành viên.',1;
            IF @min<=0 THROW 51120,N'Hạng mới phải có mức chi tiêu lớn hơn 0 (hạng 0đ là hạng mặc định).',1;
            IF EXISTS(SELECT 1 FROM dbo.MemberTiers WHERE Name=@name) THROW 51120,N'Tên hạng đã tồn tại.',1;
            IF EXISTS(SELECT 1 FROM dbo.MemberTiers WHERE MinSpent=@min) THROW 51120,N'Đã có hạng với mức chi tiêu này.',1;
            DECLARE @code varchar(20)=CONCAT('Tier',(SELECT COALESCE(MAX(TierID),0)+1 FROM dbo.MemberTiers));
            INSERT dbo.MemberTiers(Code,Name,MinSpent,Benefit) VALUES(@code,@name,@min,@benefit);
            DECLARE @id int=SCOPE_IDENTITY();
            INSERT dbo.AuditLogs(ActorID,Action,EntityType,EntityID,Details) VALUES(@actor,'Tier.Create','MemberTiers',@id,@name);
            COMMIT; SELECT @id AS TierID;
            """, ("@name", input.Name.Trim()), ("@min", input.MinSpent), ("@benefit", string.IsNullOrWhiteSpace(input.Benefit) ? null : input.Benefit.Trim()), ("@actor", Actor));
        return Created("/v1/tiers", new { tierId = created.Single()["TierID"] });
    }

    [HttpPut("tiers/{id:int}")]
    public async Task<IActionResult> UpdateTier(int id, TierInput input)
    {
        await RequirePermission("Promotions.Manage");
        await db.Query("""
            SET XACT_ABORT ON; BEGIN TRAN;
            DECLARE @old decimal(18,2)=(SELECT MinSpent FROM dbo.MemberTiers WITH(UPDLOCK,HOLDLOCK) WHERE TierID=@id);
            IF @old IS NULL THROW 51104,N'Không tìm thấy hạng.',1;
            IF @old=0 AND @min<>0 THROW 51120,N'Hạng mặc định luôn có mức 0đ để mọi khách đều có hạng.',1;
            IF @old<>0 AND @min<=0 THROW 51120,N'Chỉ hạng mặc định có mức 0đ.',1;
            IF EXISTS(SELECT 1 FROM dbo.MemberTiers WHERE Name=@name AND TierID<>@id) THROW 51120,N'Tên hạng đã tồn tại.',1;
            IF EXISTS(SELECT 1 FROM dbo.MemberTiers WHERE MinSpent=@min AND TierID<>@id) THROW 51120,N'Đã có hạng với mức chi tiêu này.',1;
            UPDATE dbo.MemberTiers SET Name=@name,MinSpent=@min,Benefit=@benefit WHERE TierID=@id;
            INSERT dbo.AuditLogs(ActorID,Action,EntityType,EntityID,Details) VALUES(@actor,'Tier.Update','MemberTiers',@id,CONCAT(@name,': ',@min));
            COMMIT;
            """, ("@id", id), ("@name", input.Name.Trim()), ("@min", input.MinSpent), ("@benefit", string.IsNullOrWhiteSpace(input.Benefit) ? null : input.Benefit.Trim()), ("@actor", Actor));
        return NoContent();
    }

    [HttpDelete("tiers/{id:int}")]
    public async Task<IActionResult> DeleteTier(int id)
    {
        await RequirePermission("Promotions.Manage");
        await db.Query("""
            SET XACT_ABORT ON; BEGIN TRAN;
            DECLARE @min decimal(18,2),@name nvarchar(50);
            SELECT @min=MinSpent,@name=Name FROM dbo.MemberTiers WITH(UPDLOCK,HOLDLOCK) WHERE TierID=@id;
            IF @min IS NULL THROW 51104,N'Không tìm thấy hạng.',1;
            IF @min=0 THROW 51120,N'Không xóa được hạng mặc định (0đ).',1;
            DELETE dbo.MemberTiers WHERE TierID=@id;
            INSERT dbo.AuditLogs(ActorID,Action,EntityType,EntityID,Details) VALUES(@actor,'Tier.Delete','MemberTiers',@id,@name);
            COMMIT;
            """, ("@id", id), ("@actor", Actor));
        return NoContent();
    }
}
