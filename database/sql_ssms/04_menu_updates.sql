-- 04_menu_updates.sql – size, Đá/Nóng, tình trạng món, nhà cung cấp mẫu (cài mới hoặc nâng cấp)
-- =====================================================================================
-- CÁCH CHẠY (SQL Server Management Studio hoặc Azure Data Studio, KHÔNG cần PowerShell):
--   1. Kết nối tới server, ví dụ (localdb)\MSSQLLocalDB.
--   2. File → Open → chọn file này → bấm Execute (F5). Không cần chọn database trước:
--      file tự chuyển sang database [MorningBrewCRM_Crm].
--   3. Chạy lần lượt: 01_core → 02_demo_data (tùy chọn) → 03_crm_updates → 04_menu_updates.
--   4. Chạy xong, tab Messages phải có dòng "=== HOÀN TẤT ...". Nếu thấy dòng ">>> DỪNG"
--      hoặc không có "HOÀN TẤT" thì file đã dừng giữa chừng: đọc lỗi màu đỏ đầu tiên, sửa
--      nguyên nhân rồi mới chạy tiếp. Khi có lỗi, các lệnh phía sau tự động không chạy.
--   Muốn dùng tên database khác: Edit → Find and Replace "MorningBrewCRM_Crm" ở CẢ 4 file
--   (và sửa ConnectionStrings:MorningBrew của API cho khớp).
--   File lưu UTF-8 có BOM để SSMS đọc đúng tiếng Việt.
-- Nội dung SQL giữ nguyên bản database/sql (nhánh db-test); chỉ thêm phần chọn database,
-- kiểm tra điều kiện trước khi chạy và lệnh "dừng khi lỗi" giữa các khối lệnh.
-- =====================================================================================
GO
SET NOEXEC OFF;  -- bỏ trạng thái dừng còn sót từ lần chạy trước trong cùng cửa sổ
GO
USE [MorningBrewCRM_Crm];
GO
IF @@ERROR <> 0
BEGIN
    RAISERROR(N'>>> DỪNG: khối lệnh phía trên bị lỗi. Các lệnh còn lại trong file KHÔNG được chạy. Xem dòng lỗi màu đỏ phía trên.', 16, 1);
    SET NOEXEC ON;
END
GO
IF DB_NAME() <> N'MorningBrewCRM_Crm'
BEGIN
    RAISERROR(N'>>> DỪNG: không tìm thấy database [MorningBrewCRM_Crm].', 16, 1);
    SET NOEXEC ON;
END
IF OBJECT_ID(N'dbo.Suppliers', N'U') IS NULL OR COL_LENGTH(N'dbo.Users', N'DeletedAt') IS NULL OR OBJECT_ID(N'dbo.AreaManagers', N'U') IS NOT NULL
BEGIN
    RAISERROR(N'>>> DỪNG: chưa chạy 03_crm_updates.sql. Chạy 03 trước.', 16, 1);
    SET NOEXEC ON;
END
GO
IF @@ERROR <> 0
BEGIN
    RAISERROR(N'>>> DỪNG: khối lệnh phía trên bị lỗi. Các lệnh còn lại trong file KHÔNG được chạy. Xem dòng lỗi màu đỏ phía trên.', 16, 1);
    SET NOEXEC ON;
END
GO
-- DB-TEST PACKAGE: Preserves the original SQL batch order. See database/README.md.
GO
IF @@ERROR <> 0
BEGIN
    RAISERROR(N'>>> DỪNG: khối lệnh phía trên bị lỗi. Các lệnh còn lại trong file KHÔNG được chạy. Xem dòng lỗi màu đỏ phía trên.', 16, 1);
    SET NOEXEC ON;
END
GO
-- DB-TEST SOURCE: 13_ui_review.sql (historical filename)
-- 13_ui_review.sql – đợt góp ý giao diện 09/11/2026 (chạy sau 12_bugfix.sql). Chạy lại nhiều lần không sao.
--   [UI2-A3]  Chọn món chỉ còn Size + Đá/Nóng: tắt nhóm Đường, Topping; nhóm "Đá" thành "Nhiệt độ" (Đá / Nóng, bắt buộc)
--             cho mọi đồ uống; thêm size L (= giá M + 10.000đ) cho đồ uống chỉ có size M.
--   [UI2-A4a] Hai món dữ liệu mẫu đang "Tạm hết" (Trà sen vàng, Bánh mousse chanh dây) đặt lại "Còn hàng";
--             từ nay chỉnh tình trạng còn hàng ở Admin → Sản phẩm.
--   [UI2-W2]  Dữ liệu mẫu nhà cung cấp (chỉ thêm khi bảng còn trống).
SET NOCOUNT ON; SET XACT_ABORT ON; SET QUOTED_IDENTIFIER ON;
BEGIN TRAN;

-- ===== [UI2-A3] =====
-- Tắt nhóm Đường và Topping (không xóa: đơn cũ vẫn giữ đúng tên tùy chọn đã chọn).
UPDATE dbo.ModifierGroups SET IsActive=0 WHERE Kind IN ('Sugar','Topping') AND IsActive=1;

DECLARE @temp int=(SELECT TOP(1) ModifierGroupID FROM dbo.ModifierGroups WHERE Kind='Ice' ORDER BY ModifierGroupID);
IF @temp IS NULL
BEGIN
    INSERT dbo.ModifierGroups(Name,Kind) VALUES(N'Nhiệt độ','Ice');
    SET @temp=SCOPE_IDENTITY();
END
UPDATE dbo.ModifierGroups SET Name=N'Nhiệt độ',IsActive=1 WHERE ModifierGroupID=@temp;
-- Lựa chọn cũ (50% đá, 100% đá) ngừng dùng; thêm "Đá" và "Nóng".
UPDATE dbo.ModifierOptions SET IsActive=0 WHERE ModifierGroupID=@temp AND Name NOT IN (N'Đá',N'Nóng');
IF NOT EXISTS(SELECT 1 FROM dbo.ModifierOptions WHERE ModifierGroupID=@temp AND Name=N'Đá')
    INSERT dbo.ModifierOptions(ModifierGroupID,Name,ExtraPrice,DisplayOrder) VALUES(@temp,N'Đá',0,1);
IF NOT EXISTS(SELECT 1 FROM dbo.ModifierOptions WHERE ModifierGroupID=@temp AND Name=N'Nóng')
    INSERT dbo.ModifierOptions(ModifierGroupID,Name,ExtraPrice,DisplayOrder) VALUES(@temp,N'Nóng',0,2);
UPDATE dbo.ModifierOptions SET IsActive=1 WHERE ModifierGroupID=@temp AND Name IN (N'Đá',N'Nóng');

-- Đồ uống = sản phẩm thuộc danh mục Cà phê / Trà.
DECLARE @drinks TABLE(ProductID int PRIMARY KEY);
INSERT @drinks SELECT p.ProductID FROM dbo.Products p JOIN dbo.Categories c ON c.CategoryID=p.CategoryID
WHERE c.Name IN (N'Cà phê',N'Trà');

UPDATE pg SET MinSelections=1,MaxSelections=1 FROM dbo.ProductModifierGroups pg JOIN @drinks d ON d.ProductID=pg.ProductID WHERE pg.ModifierGroupID=@temp;
INSERT dbo.ProductModifierGroups(ProductID,ModifierGroupID,MinSelections,MaxSelections)
SELECT d.ProductID,@temp,1,1 FROM @drinks d
WHERE NOT EXISTS(SELECT 1 FROM dbo.ProductModifierGroups pg WHERE pg.ProductID=d.ProductID AND pg.ModifierGroupID=@temp);

-- Size L cho đồ uống chỉ có size M.
INSERT dbo.ProductVariants(ProductID,Name,BasePrice,DisplayOrder)
SELECT m.ProductID,'L',m.BasePrice+10000,2 FROM dbo.ProductVariants m JOIN @drinks d ON d.ProductID=m.ProductID
WHERE m.Name='M' AND m.IsActive=1 AND NOT EXISTS(SELECT 1 FROM dbo.ProductVariants l WHERE l.ProductID=m.ProductID AND l.Name='L');
UPDATE v SET DisplayOrder=CASE v.Name WHEN 'M' THEN 1 WHEN 'L' THEN 2 ELSE v.DisplayOrder END
FROM dbo.ProductVariants v JOIN @drinks d ON d.ProductID=v.ProductID;

-- ===== [UI2-A4a] =====
UPDATE bp SET IsAvailable=1 FROM dbo.BranchProducts bp JOIN dbo.Products p ON p.ProductID=bp.ProductID
WHERE p.Name IN (N'Trà sen vàng',N'Bánh mousse chanh dây') AND bp.IsAvailable=0;

-- ===== [UI2-W2] =====
IF OBJECT_ID('dbo.Suppliers','U') IS NOT NULL AND NOT EXISTS(SELECT 1 FROM dbo.Suppliers)
INSERT dbo.Suppliers(Code,Name,Phone,Email,ProductGroup,IsActive) VALUES
('NCC-CF01',N'Công ty TNHH Cà phê Cầu Đất','0263 3812 456','kinhdoanh@caudatcoffee.test',N'Cà phê hạt Robusta, Arabica',1),
('NCC-SU01',N'Nhà phân phối Sữa Phương Nam','028 3896 1122','order@suaphuongnam.test',N'Sữa tươi, sữa đặc, kem béo',1),
('NCC-TR01',N'Trà Thái Nguyên Xanh','0208 3655 789','sales@trathainguyen.test',N'Trà lài, trà xanh, trà sen',1),
('NCC-TC01',N'Vựa trái cây Bốn Mùa','0909 456 123','bonmua.fruit@mail.test',N'Đào, vải, cam, chanh dây',1),
('NCC-BK01',N'Lò bánh Ngọc Lan','028 3512 7788','ngoclan.bakery@mail.test',N'Bánh croissant, tiramisu, mousse',1),
('NCC-BB01',N'Bao bì Minh Phát','028 3754 2299','minhphat.pack@mail.test',N'Ly nhựa, ống hút, túi giấy',0);

COMMIT;
GO
IF @@ERROR <> 0
BEGIN
    RAISERROR(N'>>> DỪNG: khối lệnh phía trên bị lỗi. Các lệnh còn lại trong file KHÔNG được chạy. Xem dòng lỗi màu đỏ phía trên.', 16, 1);
    SET NOEXEC ON;
END
GO
PRINT N'=== HOÀN TẤT 04_menu_updates.sql trên database [MorningBrewCRM_Crm]. Database sẵn sàng cho API.';
SELECT (SELECT COUNT(*) FROM sys.tables) AS SoBang, (SELECT COUNT(*) FROM sys.procedures) AS SoThuTuc, (SELECT COUNT(*) FROM sys.triggers WHERE parent_class = 1) AS SoTrigger;
GO
SET NOEXEC OFF;
GO
