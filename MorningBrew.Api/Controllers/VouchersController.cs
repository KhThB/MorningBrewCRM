using System.Security.Claims;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using MorningBrew.Api.Infrastructure;

namespace MorningBrew.Api.Controllers;

[ApiController, Route("v1/vouchers"), Authorize(Roles="Owner,AreaManager,BranchManager,Customer")]
public sealed class VouchersController(SqlStore db) : ControllerBase
{
    [HttpGet("{id:int}")]
    public Task<Dictionary<string,object?>> Get(int id) => Read(db,int.Parse(User.FindFirstValue(ClaimTypes.NameIdentifier)!),id);

    internal static async Task<Dictionary<string,object?>> Read(SqlStore db,int actor,int id)
    {
        var row = (await db.Query("""
            SELECT cp.CustomerPromotionID,cp.CustomerID,cp.PromotionID,p.Name,cp.Code,
            CASE WHEN cp.Status='Available' AND cp.ExpiresAt<=SYSUTCDATETIME() THEN 'Expired' ELSE cp.Status END AS Status,
            cp.DiscountType,cp.DiscountValue,cp.MaxDiscount,cp.MinSubtotal,cp.PointsSpent,cp.ExpiresAt
            FROM dbo.CustomerPromotions cp JOIN dbo.Promotions p ON p.PromotionID=cp.PromotionID
            WHERE cp.CustomerPromotionID=@id AND (dbo.IsCustomer(@actor,cp.CustomerID)=1 OR
                (dbo.CanScope(@actor,p.Scope,p.AreaID,p.BranchID,'Promotions.Manage')=1
                AND dbo.CustomerInScope(cp.CustomerID,p.Scope,p.AreaID,p.BranchID)=1))
            """,("@id",id),("@actor",actor))).SingleOrDefault() ?? throw new ApiException(404,"Không tìm thấy voucher trong phạm vi của bạn.");
        return ApiContract.Resource(row,"vouchers","CustomerPromotionID");
    }
}
