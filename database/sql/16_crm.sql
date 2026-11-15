-- 16_crm.sql – đợt nâng cấp Quản lý CRM 15/11/2026 (chạy sau 15_web_admin.sql). Chạy lại nhiều lần không sao.
--   [CRM-00] Bỏ quyền "Quản lý tổ chức" (Organization.Manage): còn sót từ mô hình nhiều chi nhánh, không chức năng nào dùng.
--   [CRM-09] Quyền mới "Loyalty.Adjust" – điều chỉnh điểm thành viên bằng tay. Mặc định chỉ chủ cửa hàng; chủ cửa hàng
--            có thể cấp cho quản lý ở Admin server → Phân quyền (trước đây chỉ chủ cửa hàng, viết cứng trong code).
--   [CRM-02] View dbo.CustomerStats: số đơn hoàn tất, tổng chi tiêu (trừ phần đã hoàn tiền), lần mua đầu / gần nhất và
--            nhóm hành vi mua (Chưa mua / Khách mới / Thân thiết / Đang mua / Lâu không quay lại).
--   [CRM-08] Một số điện thoại chỉ thuộc về một khách hàng (chưa xóa): trigger dbo.TR_Users_CustomerPhone + index tra SĐT.
--            Dữ liệu trùng có sẵn được liệt kê ở tab Messages (không tự xóa); xử lý ở Quản lý CRM → Khách hàng → lọc "Trùng SĐT".
-- [SQL-01] Chặn chạy nhầm database giống 14_inventory.sql.
SET NOEXEC OFF;
DECLARE @db sysname = DB_NAME();
IF OBJECT_ID(N'dbo.Permissions', N'U') IS NULL OR OBJECT_ID(N'dbo.CreateOrder', N'P') IS NULL
BEGIN
    RAISERROR(N'>>> DỪNG: database đang chọn [%s] chưa phải MorningBrew CRM (đã chạy 01–15). Chọn đúng database (ví dụ MorningBrewCRM_Crm) rồi chạy lại.', 16, 1, @db);
    SET NOEXEC ON;
END
GO
SET NOCOUNT ON; SET XACT_ABORT ON; SET QUOTED_IDENTIFIER ON;
BEGIN TRAN;
-- ===== [CRM-00] Bỏ quyền Organization.Manage =====
DELETE rp FROM dbo.RolePermissions rp JOIN dbo.Permissions p ON p.PermissionID=rp.PermissionID WHERE p.Code='Organization.Manage';
DELETE dbo.Permissions WHERE Code='Organization.Manage';
-- ===== [CRM-09] Quyền điều chỉnh điểm =====
IF NOT EXISTS(SELECT 1 FROM dbo.Permissions WHERE Code='Loyalty.Adjust')
    INSERT dbo.Permissions(Code,Description) VALUES('Loyalty.Adjust',N'Điều chỉnh điểm thành viên bằng tay');
INSERT dbo.RolePermissions(RoleID,PermissionID)
SELECT r.RoleID,p.PermissionID FROM dbo.Roles r CROSS JOIN dbo.Permissions p
WHERE r.Code='Owner' AND p.Code='Loyalty.Adjust'
  AND NOT EXISTS(SELECT 1 FROM dbo.RolePermissions x WHERE x.RoleID=r.RoleID AND x.PermissionID=p.PermissionID);
-- ===== [CRM-08] Index tra khách theo SĐT =====
IF NOT EXISTS(SELECT 1 FROM sys.indexes WHERE name='IX_Users_Phone' AND object_id=OBJECT_ID('dbo.Users'))
    CREATE INDEX IX_Users_Phone ON dbo.Users(Phone); -- không dùng filtered index: tránh lỗi SET option khi thủ tục cũ ghi Users
COMMIT;
GO
SET ANSI_NULLS ON; SET QUOTED_IDENTIFIER ON;
GO
-- ===== [CRM-02] Thống kê mua hàng và nhóm hành vi của từng khách =====
-- Ngưỡng: Khách mới = đơn hoàn tất đầu tiên trong 30 ngày; Lâu không quay lại = lần mua cuối quá 60 ngày;
-- Thân thiết = từ 5 đơn hoàn tất và còn mua trong 60 ngày; Đang mua = còn lại. Tổng chi tiêu đã trừ tiền hoàn.
CREATE OR ALTER VIEW dbo.CustomerStats AS
SELECT x.CustomerID,x.CompletedOrders,x.TotalSpent,x.FirstOrderAt,x.LastOrderAt,
    CASE WHEN x.CompletedOrders=0 THEN 'None'
         WHEN x.FirstOrderAt>=DATEADD(day,-30,SYSUTCDATETIME()) THEN 'New'
         WHEN x.LastOrderAt<DATEADD(day,-60,SYSUTCDATETIME()) THEN 'Lapsed'
         WHEN x.CompletedOrders>=5 THEN 'Loyal'
         ELSE 'Active' END AS Segment
FROM (
    SELECT c.CustomerID,COUNT(o.OrderID) AS CompletedOrders,
        CAST(COALESCE(SUM(o.TotalAmount-COALESCE(r.Refunded,0)),0) AS decimal(18,2)) AS TotalSpent,
        MIN(o.CompletedAt) AS FirstOrderAt,MAX(o.CompletedAt) AS LastOrderAt
    FROM dbo.Customers c LEFT JOIN dbo.Orders o ON o.CustomerID=c.CustomerID AND o.Status='Completed'
    -- tiền đã hoàn gộp sẵn theo đơn (SUM không được chứa truy vấn con – lỗi Msg 130)
    LEFT JOIN (SELECT OrderID,SUM(Amount) AS Refunded FROM dbo.Refunds WHERE Status='Succeeded' GROUP BY OrderID) r ON r.OrderID=o.OrderID
    GROUP BY c.CustomerID) x;
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
-- Báo dữ liệu trùng có sẵn (không sửa tự động).
DECLARE @dups int=(SELECT COUNT(*) FROM (SELECT u.Phone FROM dbo.Users u JOIN dbo.Roles r ON r.RoleID=u.RoleID
    WHERE r.Code='Customer' AND u.DeletedAt IS NULL AND u.Phone IS NOT NULL GROUP BY u.Phone HAVING COUNT(*)>1) d);
IF @dups>0 PRINT CONCAT(N'Lưu ý: có ',@dups,N' số điện thoại đang dùng chung cho nhiều khách. Xem ở Quản lý CRM → Khách hàng → lọc "Trùng SĐT".');
GO
SET NOEXEC OFF; -- [SQL-01] trả lại trạng thái bình thường cho cửa sổ SSMS
GO
