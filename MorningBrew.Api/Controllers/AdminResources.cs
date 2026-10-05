using Microsoft.AspNetCore.Mvc;
using MorningBrew.Api.Infrastructure;

namespace MorningBrew.Api.Controllers;

public sealed partial class AdminController
{
    [HttpGet("accounts/{id:int}")]
    public Task<Dictionary<string, object?>> Account(int id) => ReadResource("accounts", id);
    [HttpGet("products/{id:int}")]
    public Task<Dictionary<string, object?>> Product(int id) => ReadResource("products", id);
    [HttpGet("variants/{id:int}")]
    public Task<Dictionary<string, object?>> Variant(int id) => ReadResource("variants", id);
    [HttpGet("suppliers/{id:int}")]
    public Task<Dictionary<string, object?>> Supplier(int id) => ReadResource("suppliers", id);
    [HttpGet("shifts/{id:int}")]
    public Task<Dictionary<string, object?>> Shift(int id) => ReadResource("shifts", id);
    [HttpGet("surveys/{id:int}")]
    public Task<Dictionary<string, object?>> Survey(int id) => ReadResource("surveys", id);
    [HttpGet("promotions/{id:int}")]
    public Task<Dictionary<string, object?>> Promotion(int id) => ReadResource("promotions", id);

    private async Task<Dictionary<string, object?>> ReadResource(string collection, int id)
    {
        if (collection is "accounts" or "suppliers") await RequireOwner();
        var (sql, key) = collection switch
        {
            "accounts" => ("SELECT u.UserID,u.Email,u.FullName,u.Phone,u.IsActive,u.CreatedAt,r.Code AS Role FROM dbo.Users u JOIN dbo.Roles r ON r.RoleID=u.RoleID WHERE u.UserID=@id", "UserID"),
            "products" => ("SELECT ProductID,CategoryID,Name,Description,ImageURL,IsActive FROM dbo.Products WHERE ProductID=@id", "ProductID"),
            "variants" => ("SELECT VariantID,ProductID,Name,BasePrice,DisplayOrder,IsActive FROM dbo.ProductVariants WHERE VariantID=@id", "VariantID"),
            "suppliers" => ("SELECT SupplierID,Code,Name,Phone,Email,ProductGroup,IsActive FROM dbo.Suppliers WHERE SupplierID=@id", "SupplierID"),
            "shifts" => ("SELECT ShiftID,UserID,BranchID,StartsAt,EndsAt,IsCancelled,CreatedBy FROM dbo.StaffShifts WHERE ShiftID=@id AND dbo.CanBranch(@actor,BranchID,'Staff.Manage')=1", "ShiftID"),
            "surveys" => ("SELECT SurveyID,Title,Description,Scope,AreaID,BranchID,StartsAt,EndsAt,Status,CreatedBy FROM dbo.Surveys WHERE SurveyID=@id AND dbo.CanScope(@actor,Scope,AreaID,BranchID,'Surveys.Manage')=1", "SurveyID"),
            "promotions" => ("SELECT PromotionID,Name,Description,Scope,AreaID,BranchID,DiscountType,DiscountValue,MaxDiscount,MinSubtotal,StartsAt,EndsAt,PointsCost,TotalLimit,PerCustomerLimit,IsActive FROM dbo.Promotions WHERE PromotionID=@id AND dbo.CanScope(@actor,Scope,AreaID,BranchID,'Promotions.Manage')=1", "PromotionID"),
            _ => throw new ApiException(404, "Không tìm thấy tài nguyên.")
        };
        var row = (await db.Query(sql, ("@id", id), ("@actor", Actor))).SingleOrDefault()
            ?? throw new ApiException(404, "Không tìm thấy tài nguyên trong phạm vi của bạn.");
        if (collection == "products")
            row["Variants"] = await db.Query("SELECT VariantID,Name,BasePrice,DisplayOrder,IsActive FROM dbo.ProductVariants WHERE ProductID=@id ORDER BY DisplayOrder,VariantID", ("@id", id));
        if (collection == "surveys")
        {
            row["Questions"] = await db.Query("SELECT QuestionID,Text,Type,IsRequired,DisplayOrder,RatingMin,RatingMax FROM dbo.SurveyQuestions WHERE SurveyID=@id ORDER BY DisplayOrder,QuestionID", ("@id", id));
            row["Options"] = await db.Query("SELECT o.OptionID,o.QuestionID,o.Text,o.DisplayOrder FROM dbo.SurveyQuestionOptions o JOIN dbo.SurveyQuestions q ON q.QuestionID=o.QuestionID WHERE q.SurveyID=@id ORDER BY o.QuestionID,o.DisplayOrder,o.OptionID", ("@id", id));
        }
        return ApiContract.Resource(row, collection, key);
    }
}
