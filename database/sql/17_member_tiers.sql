-- 17_member_tiers.sql – hạng thành viên (chạy sau 16_crm.sql). Chạy lại nhiều lần không sao.
--   [CRM-01] Trước đây hạng chỉ là phần hiển thị trong app (tính từ điểm, không lưu ở database) nên web không lọc,
--            không báo cáo, không ưu đãi theo hạng được. Nay:
--            * Bảng dbo.MemberTiers: các hạng và mức chi tiêu tối thiểu (quản lý sửa được ở Quản lý CRM → Hạng thành viên).
--            * View dbo.CustomerTiers: hạng hiện tại của từng khách = hạng cao nhất có MinSpent ≤ chi tiêu 12 tháng gần nhất
--              (đơn hoàn tất, đã trừ tiền hoàn) + hạng kế tiếp và mức cần đạt. Tính trực tiếp nên luôn đúng, không cần job.
--            Hạng mặc định: Thành viên 0đ · Bạc 1.000.000đ · Vàng 3.000.000đ · Kim cương 6.000.000đ (chi tiêu 12 tháng).
-- [SQL-01] Chặn chạy nhầm database giống 14_inventory.sql.
SET NOEXEC OFF;
DECLARE @db sysname = DB_NAME();
IF OBJECT_ID(N'dbo.Customers', N'U') IS NULL OR OBJECT_ID(N'dbo.CreateOrder', N'P') IS NULL
BEGIN
    RAISERROR(N'>>> DỪNG: database đang chọn [%s] chưa phải MorningBrew CRM (đã chạy 01–16). Chọn đúng database (ví dụ MorningBrewCRM_Crm) rồi chạy lại.', 16, 1, @db);
    SET NOEXEC ON;
END
GO
SET NOCOUNT ON; SET XACT_ABORT ON; SET QUOTED_IDENTIFIER ON;
BEGIN TRAN;
IF OBJECT_ID('dbo.MemberTiers','U') IS NULL
CREATE TABLE dbo.[MemberTiers] (
    [TierID]       int           IDENTITY(1,1) NOT NULL,
    [Code]         varchar(20)   NOT NULL,                 -- mã cố định (Member, Silver, …) – dùng để lọc, không hiển thị
    [Name]         nvarchar(50)  NOT NULL,                 -- tên hiển thị trên web và app
    [MinSpent]     decimal(18,2) NOT NULL,                 -- chi tiêu 12 tháng tối thiểu để đạt hạng
    [Benefit]      nvarchar(300) NULL,                     -- mô tả quyền lợi (hiển thị cho khách)
    [CreatedAt]    datetime2(3)  NOT NULL CONSTRAINT [DF_MemberTiers_CreatedAt] DEFAULT (SYSUTCDATETIME()),
    CONSTRAINT [PK_MemberTiers]   PRIMARY KEY ([TierID]),
    CONSTRAINT [UQ_MemberTiers_0] UNIQUE ([Code]),
    CONSTRAINT [UQ_MemberTiers_1] UNIQUE ([Name]),
    CONSTRAINT [UQ_MemberTiers_2] UNIQUE ([MinSpent]),
    CONSTRAINT [CK_MemberTiers_0] CHECK ([MinSpent] >= 0)
);
IF NOT EXISTS(SELECT 1 FROM dbo.MemberTiers)
    INSERT dbo.MemberTiers(Code,Name,MinSpent,Benefit) VALUES
    ('Member', N'Thành viên',       0, N'Tích điểm cho mọi đơn hoàn tất, đổi điểm lấy voucher.'),
    ('Silver', N'Bạc',        1000000, N'Ưu tiên nhận voucher riêng hạng Bạc.'),
    ('Gold',   N'Vàng',       3000000, N'Voucher sinh nhật và ưu đãi riêng hạng Vàng.'),
    ('Diamond',N'Kim cương',  6000000, N'Ưu đãi cao nhất, được mời thử món mới đầu tiên.');
COMMIT;
GO
SET ANSI_NULLS ON; SET QUOTED_IDENTIFIER ON;
GO
-- ===== Hạng hiện tại của từng khách =====
CREATE OR ALTER VIEW dbo.CustomerTiers AS
WITH spend AS (
    SELECT c.CustomerID,CAST(COALESCE(SUM(o.TotalAmount-COALESCE(r.Refunded,0)),0) AS decimal(18,2)) AS Spent12M
    FROM dbo.Customers c
    LEFT JOIN dbo.Orders o ON o.CustomerID=c.CustomerID AND o.Status='Completed' AND o.CompletedAt>=DATEADD(month,-12,SYSUTCDATETIME())
    LEFT JOIN (SELECT OrderID,SUM(Amount) AS Refunded FROM dbo.Refunds WHERE Status='Succeeded' GROUP BY OrderID) r ON r.OrderID=o.OrderID
    GROUP BY c.CustomerID)
SELECT s.CustomerID,s.Spent12M,t.TierID,t.Code AS TierCode,t.Name AS TierName,t.Benefit AS TierBenefit,
       n.Code AS NextTierCode,n.Name AS NextTierName,n.MinSpent AS NextTierSpent
FROM spend s
OUTER APPLY (SELECT TOP(1) x.TierID,x.Code,x.Name,x.Benefit FROM dbo.MemberTiers x WHERE x.MinSpent<=s.Spent12M ORDER BY x.MinSpent DESC) t
OUTER APPLY (SELECT TOP(1) y.Code,y.Name,y.MinSpent FROM dbo.MemberTiers y WHERE y.MinSpent>s.Spent12M ORDER BY y.MinSpent) n;
GO
SET NOEXEC OFF; -- [SQL-01] trả lại trạng thái bình thường cho cửa sổ SSMS
GO
