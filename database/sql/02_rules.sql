-- 02_rules.sql – hàm phân quyền và trigger bảo vệ dữ liệu.
-- [GOP-01] Bản gộp: hàm CanBranch / CanScope / CustomerInScope, trigger TR_StaffBranches_Role, TR_Users_Role lấy bản cuối
--          (mô hình một cửa hàng, trước ở 11_single_store.sql); thêm trigger TR_Orders_ShortCode (mã đơn ngắn, trước ở 12),
--          TR_InventoryTransactions_Immutable (14), TR_Orders_Inventory (tự trừ kho, 15), TR_Users_CustomerPhone (16).
SET ANSI_NULLS ON;
SET QUOTED_IDENTIFIER ON;
GO
CREATE OR ALTER FUNCTION dbo.CanBranch(@UserID int,@BranchID int,@Permission varchar(80))
RETURNS bit AS
BEGIN
 DECLARE @ok bit=0;
 IF EXISTS(SELECT 1 FROM dbo.Users u JOIN dbo.Roles r ON r.RoleID=u.RoleID
  JOIN dbo.RolePermissions rp ON rp.RoleID=u.RoleID JOIN dbo.Permissions p ON p.PermissionID=rp.PermissionID
  JOIN dbo.Branches b ON b.BranchID=@BranchID
  WHERE u.UserID=@UserID AND u.IsActive=1 AND p.Code=@Permission AND
  (r.Code='Owner'
   OR (r.Code='BranchManager' AND b.ManagerID=u.UserID)
   OR (r.Code IN ('BranchManager','Staff') AND EXISTS(SELECT 1 FROM dbo.StaffBranches s WHERE s.UserID=u.UserID AND s.BranchID=b.BranchID AND s.IsActive=1)))) SET @ok=1;
 RETURN @ok;
END;
GO
CREATE OR ALTER FUNCTION dbo.IsCustomer(@UserID int,@CustomerID int)
RETURNS bit AS
BEGIN
 RETURN CASE WHEN EXISTS(SELECT 1 FROM dbo.Customers c JOIN dbo.Users u ON u.UserID=c.UserID JOIN dbo.Roles r ON r.RoleID=u.RoleID WHERE c.CustomerID=@CustomerID AND u.UserID=@UserID AND u.IsActive=1 AND r.Code='Customer') THEN 1 ELSE 0 END;
END;
GO
CREATE OR ALTER FUNCTION dbo.CanScope(@UserID int,@Scope varchar(10),@AreaID int,@BranchID int,@Permission varchar(80))
RETURNS bit AS
BEGIN
 IF @Scope='Branch' RETURN dbo.CanBranch(@UserID,@BranchID,@Permission);
 -- Chain/Area: chủ cửa hàng có quyền, hoặc người có quyền tại một cửa hàng đang hoạt động (chỉ có 1 cửa hàng).
 RETURN CASE WHEN EXISTS(SELECT 1 FROM dbo.Users u JOIN dbo.Roles r ON r.RoleID=u.RoleID
   JOIN dbo.RolePermissions rp ON rp.RoleID=r.RoleID JOIN dbo.Permissions p ON p.PermissionID=rp.PermissionID
   WHERE u.UserID=@UserID AND u.IsActive=1 AND p.Code=@Permission AND r.Code='Owner')
  OR EXISTS(SELECT 1 FROM dbo.Branches b WHERE b.IsActive=1 AND dbo.CanBranch(@UserID,b.BranchID,@Permission)=1)
  THEN 1 ELSE 0 END;
END;
GO
CREATE OR ALTER FUNCTION dbo.CustomerInScope(@CustomerID int,@Scope varchar(10),@AreaID int,@BranchID int)
RETURNS bit AS
BEGIN
 RETURN CASE WHEN EXISTS(SELECT 1 FROM dbo.Customers c JOIN dbo.Users u ON u.UserID=c.UserID
  WHERE c.CustomerID=@CustomerID AND u.DeletedAt IS NULL) THEN 1 ELSE 0 END;
END;
GO
CREATE OR ALTER TRIGGER dbo.TR_Customers_Role ON dbo.Customers AFTER INSERT,UPDATE AS
BEGIN
 SET NOCOUNT ON;
 IF EXISTS(SELECT 1 FROM inserted i JOIN dbo.Users u ON u.UserID=i.UserID JOIN dbo.Roles r ON r.RoleID=u.RoleID WHERE r.Code<>'Customer') THROW 51001,N'Customer profile requires Customer role.',1;
 IF EXISTS(SELECT 1 FROM inserted WHERE DateOfBirth>CONVERT(date,SYSUTCDATETIME())) THROW 51001,N'Date of birth cannot be in the future.',1;
END;
GO
CREATE OR ALTER TRIGGER dbo.TR_StaffBranches_Role ON dbo.StaffBranches AFTER INSERT,UPDATE AS
BEGIN
 SET NOCOUNT ON;
 IF EXISTS(SELECT 1 FROM inserted i JOIN dbo.Users u ON u.UserID=i.UserID JOIN dbo.Roles r ON r.RoleID=u.RoleID WHERE r.Code NOT IN ('Staff','BranchManager'))
  THROW 51001,N'Assignment requires Staff or BranchManager role.',1;
END;
GO
CREATE OR ALTER TRIGGER dbo.TR_Branches_Role ON dbo.Branches AFTER INSERT,UPDATE AS
BEGIN
 SET NOCOUNT ON;
 IF EXISTS(SELECT 1 FROM inserted i JOIN dbo.Users u ON u.UserID=i.ManagerID JOIN dbo.Roles r ON r.RoleID=u.RoleID WHERE r.Code<>'BranchManager') THROW 51001,N'Branch manager requires BranchManager role.',1;
END;
GO
CREATE OR ALTER TRIGGER dbo.TR_Users_Role ON dbo.Users AFTER UPDATE AS
BEGIN
 SET NOCOUNT ON;
 IF UPDATE(RoleID) AND EXISTS(SELECT 1 FROM inserted i JOIN deleted d ON d.UserID=i.UserID WHERE i.RoleID<>d.RoleID AND
 (EXISTS(SELECT 1 FROM dbo.Customers WHERE UserID=i.UserID) OR EXISTS(SELECT 1 FROM dbo.StaffBranches WHERE UserID=i.UserID AND IsActive=1) OR EXISTS(SELECT 1 FROM dbo.Branches WHERE ManagerID=i.UserID)))
 THROW 51001,N'Remove incompatible assignments/profile before changing role.',1;
END;
GO
CREATE OR ALTER TRIGGER dbo.TR_Policy_Overlap ON dbo.LoyaltyPolicies AFTER INSERT,UPDATE AS
BEGIN
 SET NOCOUNT ON;
 IF EXISTS(SELECT 1 FROM dbo.LoyaltyPolicies a WITH(UPDLOCK,HOLDLOCK) JOIN dbo.LoyaltyPolicies b WITH(UPDLOCK,HOLDLOCK) ON a.PolicyID<b.PolicyID AND a.EffectiveFrom<COALESCE(b.EffectiveTo,'9999-12-31') AND b.EffectiveFrom<COALESCE(a.EffectiveTo,'9999-12-31')) THROW 51001,N'Loyalty policy periods cannot overlap.',1;
END;
GO
CREATE OR ALTER TRIGGER dbo.TR_ProductModifierGroups_Limits ON dbo.ProductModifierGroups AFTER INSERT,UPDATE AS
BEGIN
 SET NOCOUNT ON;
 IF EXISTS(SELECT 1 FROM inserted i JOIN dbo.ModifierGroups g ON g.ModifierGroupID=i.ModifierGroupID WHERE g.Kind IN ('Sugar','Ice') AND i.MaxSelections<>1) THROW 51001,N'Sugar/Ice must allow at most one selection.',1;
END;
GO
CREATE OR ALTER TRIGGER dbo.TR_ModifierGroups_Limits ON dbo.ModifierGroups AFTER UPDATE AS
BEGIN
 SET NOCOUNT ON;
 IF EXISTS(SELECT 1 FROM inserted i JOIN dbo.ProductModifierGroups g ON g.ModifierGroupID=i.ModifierGroupID WHERE i.Kind IN ('Sugar','Ice') AND g.MaxSelections<>1) THROW 51001,N'Existing selection limits conflict with group kind.',1;
END;
GO
CREATE OR ALTER TRIGGER dbo.TR_Loyalty_Immutable ON dbo.LoyaltyTransactions AFTER UPDATE,DELETE AS
BEGIN
 IF EXISTS(SELECT 1 FROM deleted) THROW 51001,N'Loyalty ledger is append-only; post a correcting transaction.',1;
END;
GO
CREATE OR ALTER TRIGGER dbo.TR_Orders_Snapshot ON dbo.Orders AFTER UPDATE AS
BEGIN
 IF UPDATE(BranchID) OR UPDATE(CustomerID) OR UPDATE(CreatedBy) OR UPDATE(Source) OR UPDATE(Fulfillment) OR UPDATE(PaymentMode)
 OR UPDATE(ContactName) OR UPDATE(ContactPhone) OR UPDATE(Subtotal) OR UPDATE(DiscountAmount) OR UPDATE(DeliveryFee)
 OR UPDATE(TotalAmount) OR UPDATE(CustomerPromotionID) OR UPDATE(PolicyID) OR UPDATE(AmountPerPoint)
 THROW 51001,N'Order identity and financial snapshots are immutable; cancel and create a new order.',1;
END;
GO
CREATE OR ALTER TRIGGER dbo.TR_OrderDetails_Immutable ON dbo.OrderDetails AFTER UPDATE,DELETE AS
BEGIN
 IF EXISTS(SELECT 1 FROM deleted) THROW 51001,N'Order item snapshots are immutable.',1;
END;
GO
CREATE OR ALTER TRIGGER dbo.TR_OrderItemModifiers_Immutable ON dbo.OrderItemModifiers AFTER UPDATE,DELETE AS
BEGIN
 IF EXISTS(SELECT 1 FROM deleted) THROW 51001,N'Order option snapshots are immutable.',1;
END;
GO
CREATE OR ALTER TRIGGER dbo.TR_Deliveries_Snapshot ON dbo.Deliveries AFTER UPDATE AS
BEGIN
 IF UPDATE(RecipientName) OR UPDATE(Phone) OR UPDATE(AddressText) OR UPDATE(Note) THROW 51001,N'Delivery recipient snapshot is immutable.',1;
END;
GO
CREATE OR ALTER TRIGGER dbo.TR_Promotions_Issued ON dbo.Promotions AFTER UPDATE AS
BEGIN
 IF (UPDATE(Scope) OR UPDATE(AreaID) OR UPDATE(BranchID)) AND EXISTS(SELECT 1 FROM inserted i JOIN dbo.CustomerPromotions cp ON cp.PromotionID=i.PromotionID) THROW 51001,N'Cannot change scope after issuing vouchers.',1;
END;
GO
CREATE OR ALTER TRIGGER dbo.TR_CustomerPromotions_Terms ON dbo.CustomerPromotions AFTER UPDATE AS
BEGIN
 IF UPDATE(CustomerID) OR UPDATE(PromotionID) OR UPDATE(ExpiresAt) OR UPDATE(DiscountType) OR UPDATE(DiscountValue) OR UPDATE(MaxDiscount) OR UPDATE(MinSubtotal) OR UPDATE(PointsSpent)
 THROW 51001,N'Issued voucher terms are immutable.',1;
END;
GO
CREATE OR ALTER TRIGGER dbo.TR_SurveyQuestions_Freeze ON dbo.SurveyQuestions AFTER INSERT,UPDATE,DELETE AS
BEGIN
 SET NOCOUNT ON;
 IF EXISTS(SELECT 1 FROM dbo.Surveys s WHERE s.Status<>'Draft' AND (s.SurveyID IN (SELECT SurveyID FROM inserted) OR s.SurveyID IN (SELECT SurveyID FROM deleted))) THROW 51001,N'Published survey questions cannot change.',1;
END;
GO
CREATE OR ALTER TRIGGER dbo.TR_SurveyOptions_Freeze ON dbo.SurveyQuestionOptions AFTER INSERT,UPDATE,DELETE AS
BEGIN
 SET NOCOUNT ON;
 IF EXISTS(SELECT 1 FROM dbo.SurveyQuestions q JOIN dbo.Surveys s ON s.SurveyID=q.SurveyID WHERE (q.QuestionID IN (SELECT QuestionID FROM inserted) OR q.QuestionID IN (SELECT QuestionID FROM deleted)) AND s.Status<>'Draft') THROW 51001,N'Published survey options cannot change.',1;
 IF EXISTS(SELECT 1 FROM inserted i JOIN dbo.SurveyQuestions q ON q.QuestionID=i.QuestionID WHERE q.Type NOT IN ('SingleChoice','MultipleChoice')) THROW 51001,N'Only choice questions have options.',1;
END;
GO
CREATE OR ALTER TRIGGER dbo.TR_Surveys_Publish ON dbo.Surveys AFTER UPDATE,DELETE AS
BEGIN
 SET NOCOUNT ON;
 IF EXISTS(SELECT 1 FROM deleted d LEFT JOIN inserted i ON i.SurveyID=d.SurveyID WHERE d.Status<>'Draft' AND (i.SurveyID IS NULL OR i.Status='Draft' OR (d.Status='Closed' AND i.Status<>'Closed'))) THROW 51001,N'Published surveys cannot be reset or deleted.',1;
 IF (UPDATE(Scope) OR UPDATE(AreaID) OR UPDATE(BranchID) OR UPDATE(StartsAt) OR UPDATE(EndsAt)) AND EXISTS(SELECT 1 FROM deleted WHERE Status<>'Draft') THROW 51001,N'Published survey scope and schedule are frozen.',1;
 IF EXISTS(SELECT 1 FROM inserted s WHERE s.Status='Open' AND (NOT EXISTS(SELECT 1 FROM dbo.SurveyQuestions q WHERE q.SurveyID=s.SurveyID) OR EXISTS(SELECT 1 FROM dbo.SurveyQuestions q WHERE q.SurveyID=s.SurveyID AND q.Type IN ('SingleChoice','MultipleChoice') AND (SELECT COUNT(*) FROM dbo.SurveyQuestionOptions o WHERE o.QuestionID=q.QuestionID)<2))) THROW 51001,N'Survey needs questions and at least two options per choice question.',1;
END;
GO
-- [Mã đơn ngắn] Mã đơn cũ 'MB-' + GUID (35 ký tự) quá dài để đọc / gọi tại quầy.
-- Mã mới: MB + ngày đặt (giờ Việt Nam, yyMMdd) + '-' + số thứ tự đơn 4 chữ số, ví dụ MB261108-0028 (13 ký tự).
-- Duy nhất vì gắn với OrderID. Trigger chạy sau khi CreateOrder thêm đơn nên không phải sửa procedure.
CREATE OR ALTER TRIGGER dbo.TR_Orders_ShortCode ON dbo.Orders AFTER INSERT AS
BEGIN
 SET NOCOUNT ON;
 UPDATE o SET OrderCode=CONCAT('MB',FORMAT(DATEADD(hour,7,i.CreatedAt),'yyMMdd'),'-',
   CASE WHEN i.OrderID>9999 THEN CONVERT(varchar(12),i.OrderID) ELSE RIGHT(CONCAT('0000',i.OrderID),4) END)
 FROM dbo.Orders o JOIN inserted i ON i.OrderID=o.OrderID;
END;
GO
CREATE OR ALTER TRIGGER dbo.TR_InventoryTransactions_Immutable
ON dbo.InventoryTransactions AFTER UPDATE, DELETE AS
BEGIN
    SET NOCOUNT ON;
    IF EXISTS(SELECT 1 FROM deleted)
        THROW 51001, N'Phiếu kho là append-only; sai sót thì lập phiếu kiểm kê.', 1;
END;
GO
-- ===== [WEB-14] Đơn hoàn tất → tự trừ kho theo định mức =====
-- Chạy trong cùng giao dịch với dbo.AdvanceOrder (POS và web Quản lý đều hoàn tất đơn qua thủ tục này).
-- Không bao giờ báo lỗi để chặn việc hoàn tất đơn: thiếu hàng thì trừ về 0 và ghi chú để kiểm kê.
CREATE OR ALTER TRIGGER dbo.TR_Orders_Inventory ON dbo.Orders AFTER UPDATE AS
BEGIN
    SET NOCOUNT ON;
    IF NOT UPDATE(Status) RETURN;
    DECLARE @done TABLE(OrderID int PRIMARY KEY,OrderCode varchar(40));
    INSERT @done SELECT i.OrderID,i.OrderCode FROM inserted i JOIN deleted d ON d.OrderID=i.OrderID
    WHERE i.Status='Completed' AND d.Status<>'Completed';
    IF NOT EXISTS(SELECT 1 FROM @done) RETURN;

    DECLARE @note nvarchar(300)=LEFT(CONCAT(N'Bán hàng – đơn ',(SELECT STRING_AGG(COALESCE(OrderCode,CONVERT(varchar(20),OrderID)),', ') FROM @done)),240);
    DECLARE @use TABLE(IngredientID int PRIMARY KEY,Need decimal(18,3));
    INSERT @use
    SELECT r.IngredientID,SUM(od.Quantity*r.Quantity)
    FROM @done x JOIN dbo.OrderDetails od ON od.OrderID=x.OrderID
    JOIN dbo.RecipeItems r ON r.VariantID=od.VariantID
    JOIN dbo.Ingredients g ON g.IngredientID=r.IngredientID AND g.IsActive=1
    GROUP BY r.IngredientID;
    IF NOT EXISTS(SELECT 1 FROM @use) RETURN;

    DECLARE @moved TABLE(IngredientID int,Delta decimal(18,3),Balance decimal(18,3),Short decimal(18,3));
    UPDATE g SET Quantity=CASE WHEN g.Quantity>=u.Need THEN g.Quantity-u.Need ELSE 0 END,StockUpdatedAt=SYSUTCDATETIME()
    OUTPUT inserted.IngredientID,inserted.Quantity-deleted.Quantity,inserted.Quantity,u.Need-(deleted.Quantity-inserted.Quantity) INTO @moved
    FROM dbo.Ingredients g WITH(UPDLOCK,HOLDLOCK) JOIN @use u ON u.IngredientID=g.IngredientID;

    INSERT dbo.InventoryTransactions(IngredientID,ActorID,Kind,QuantityChange,BalanceAfter,Note)
    SELECT IngredientID,NULL,'Export',Delta,Balance,
           CONCAT(@note,CASE WHEN Short>0 THEN CONCAT(N' (thiếu ',FORMAT(Short,'0.###','vi-VN'),N' – cần kiểm kê)') END)
    -- Tồn đã bằng 0 vẫn cần phiếu tự động ghi lượng thiếu; không tạo biến động kho giả.
    -- CHECK của InventoryTransactions chỉ cho phép Export=0 khi là phiếu thiếu kho tự động.
    FROM @moved WHERE Delta<0 OR Short>0;
END;
GO
-- ===== [CRM-08] Một SĐT chỉ thuộc một khách hàng chưa xóa =====
-- Áp dụng cho mọi đường ghi: đăng ký app, sửa hồ sơ app, tạo / sửa khách trên web. Nhân viên không bị ràng buộc.
CREATE OR ALTER TRIGGER dbo.TR_Users_CustomerPhone ON dbo.Users AFTER INSERT,UPDATE AS
BEGIN
    SET NOCOUNT ON;
    IF NOT (UPDATE(Phone) OR UPDATE(RoleID) OR UPDATE(DeletedAt)) RETURN;
    DECLARE @customer int=(SELECT RoleID FROM dbo.Roles WHERE Code='Customer');
    IF EXISTS(SELECT 1 FROM inserted i JOIN dbo.Users u ON u.Phone=i.Phone AND u.UserID<>i.UserID
              WHERE i.Phone IS NOT NULL AND i.RoleID=@customer AND u.RoleID=@customer AND i.DeletedAt IS NULL AND u.DeletedAt IS NULL)
        THROW 51120,N'Số điện thoại này đã được dùng cho một khách hàng khác.',1;
END;
GO
