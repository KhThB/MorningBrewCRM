-- 14_inventory.sql  – Kho nguyên liệu (chạy sau 13_ui_review.sql). Idempotent.
-- Tạo 2 bảng: Ingredients (nguyên liệu) + InventoryTransactions (phiếu kho).
-- Thêm quyền Inventory.Manage; seed dữ liệu mẫu để demo trơn tru ngay.
-- [SQL-01] Chặn chạy nhầm database (ví dụ SSMS đang chọn master → lỗi Msg 1767/8197/208 và tạo bảng rác):
-- file không có lệnh USE để giữ đúng database đang chọn; nếu database hiện tại chưa có các bảng MorningBrew
-- (đã chạy 01–13) thì dừng cả file. Cài trọn bộ trong một lần: database/sql_ssms/morningbrew_fullscript.sql.
SET NOEXEC OFF;
DECLARE @db sysname = DB_NAME();
IF OBJECT_ID(N'dbo.Permissions', N'U') IS NULL OR OBJECT_ID(N'dbo.Suppliers', N'U') IS NULL
BEGIN
    RAISERROR(N'>>> DỪNG: database đang chọn [%s] chưa phải MorningBrew CRM (đã chạy 01–13). Chọn đúng database (ví dụ MorningBrewCRM_Crm) rồi chạy lại.', 16, 1, @db);
    SET NOEXEC ON;
END
GO
SET NOCOUNT ON; SET XACT_ABORT ON; SET QUOTED_IDENTIFIER ON;
BEGIN TRAN;

-- ===== 1. BẢNG Ingredients =====
IF OBJECT_ID('dbo.Ingredients','U') IS NULL
CREATE TABLE dbo.[Ingredients] (
    [IngredientID]  int           IDENTITY(1,1) NOT NULL,
    [SupplierID]    int           NULL,                        -- nhà cung cấp chính (nullable)
    [Code]          varchar(30)   NOT NULL,
    [Name]          nvarchar(150) NOT NULL,
    [Unit]          nvarchar(20)  NOT NULL,                    -- kg, lít, hộp …
    [Quantity]      decimal(18,3) NOT NULL CONSTRAINT [DF_Ingredients_Quantity]  DEFAULT (0),
    [MinStock]      decimal(18,3) NOT NULL CONSTRAINT [DF_Ingredients_MinStock]  DEFAULT (0),
    [LastUnitCost]  decimal(18,2) NULL,                       -- đơn giá lần nhập gần nhất
    [IsActive]      bit           NOT NULL CONSTRAINT [DF_Ingredients_IsActive]  DEFAULT (1),
    [StockUpdatedAt] datetime2(3) NULL,
    [CreatedAt]     datetime2(3)  NOT NULL CONSTRAINT [DF_Ingredients_CreatedAt] DEFAULT (SYSUTCDATETIME()),
    CONSTRAINT [PK_Ingredients]   PRIMARY KEY ([IngredientID]),
    CONSTRAINT [UQ_Ingredients_0] UNIQUE ([Code]),
    CONSTRAINT [CK_Ingredients_0] CHECK ([Quantity]  >= 0),
    CONSTRAINT [CK_Ingredients_1] CHECK ([MinStock]  >= 0),
    CONSTRAINT [CK_Ingredients_2] CHECK ([LastUnitCost] IS NULL OR [LastUnitCost] >= 0)
);

-- ===== 2. BẢNG InventoryTransactions (phiếu kho – append-only) =====
IF OBJECT_ID('dbo.InventoryTransactions','U') IS NULL
CREATE TABLE dbo.[InventoryTransactions] (
    [TransactionID]   int           IDENTITY(1,1) NOT NULL,
    [IngredientID]    int           NOT NULL,
    [SupplierID]      int           NULL,                     -- chỉ có khi Kind='Import'
    [ActorID]         int           NULL,                     -- người thực hiện
    [Kind]            varchar(10)   NOT NULL,                 -- Import | Export | Waste | Adjust
    [QuantityChange]  decimal(18,3) NOT NULL,                 -- dương=nhập, âm=xuất/hủy, ± adjust
    [BalanceAfter]    decimal(18,3) NOT NULL,                 -- tồn kho SAU phiếu
    [UnitCost]        decimal(18,2) NULL,                     -- đơn giá (chỉ Import)
    [TotalCost]       AS CASE WHEN [Kind]='Import' AND [UnitCost] IS NOT NULL THEN ABS([QuantityChange])*[UnitCost] ELSE NULL END,
    [Note]            nvarchar(300) NULL,
    [CreatedAt]       datetime2(3)  NOT NULL CONSTRAINT [DF_InventoryTransactions_CreatedAt] DEFAULT (SYSUTCDATETIME()),
    CONSTRAINT [PK_InventoryTransactions]   PRIMARY KEY ([TransactionID]),
    CONSTRAINT [CK_InventoryTransactions_0] CHECK ([Kind] IN ('Import','Export','Waste','Adjust')),
    CONSTRAINT [CK_InventoryTransactions_1] CHECK ([BalanceAfter] >= 0),
    CONSTRAINT [CK_InventoryTransactions_2] CHECK ([UnitCost] IS NULL OR [UnitCost] >= 0),
    CONSTRAINT [CK_InventoryTransactions_3] CHECK ([Kind] <> 'Import' OR [QuantityChange] > 0),
    CONSTRAINT [CK_InventoryTransactions_4] CHECK ([Kind] NOT IN ('Export','Waste') OR [QuantityChange] < 0)
);

-- ===== 3. FOREIGN KEYS (chỉ thêm nếu chưa có) =====
IF OBJECT_ID('dbo.Ingredients','U') IS NOT NULL
AND NOT EXISTS(SELECT 1 FROM sys.foreign_keys WHERE name='FK_Ingredients_SupplierID')
    ALTER TABLE dbo.[Ingredients] ADD CONSTRAINT [FK_Ingredients_SupplierID]
        FOREIGN KEY ([SupplierID]) REFERENCES dbo.[Suppliers]([SupplierID]);

IF OBJECT_ID('dbo.InventoryTransactions','U') IS NOT NULL
AND NOT EXISTS(SELECT 1 FROM sys.foreign_keys WHERE name='FK_InventoryTransactions_IngredientID')
    ALTER TABLE dbo.[InventoryTransactions] ADD CONSTRAINT [FK_InventoryTransactions_IngredientID]
        FOREIGN KEY ([IngredientID]) REFERENCES dbo.[Ingredients]([IngredientID]);

IF OBJECT_ID('dbo.InventoryTransactions','U') IS NOT NULL
AND NOT EXISTS(SELECT 1 FROM sys.foreign_keys WHERE name='FK_InventoryTransactions_SupplierID')
    ALTER TABLE dbo.[InventoryTransactions] ADD CONSTRAINT [FK_InventoryTransactions_SupplierID]
        FOREIGN KEY ([SupplierID]) REFERENCES dbo.[Suppliers]([SupplierID]);

IF OBJECT_ID('dbo.InventoryTransactions','U') IS NOT NULL
AND NOT EXISTS(SELECT 1 FROM sys.foreign_keys WHERE name='FK_InventoryTransactions_ActorID')
    ALTER TABLE dbo.[InventoryTransactions] ADD CONSTRAINT [FK_InventoryTransactions_ActorID]
        FOREIGN KEY ([ActorID]) REFERENCES dbo.[Users]([UserID]);

-- ===== 4. INDEX =====
IF NOT EXISTS(SELECT 1 FROM sys.indexes WHERE name='IX_Ingredients_19' AND object_id=OBJECT_ID('dbo.Ingredients'))
    CREATE INDEX IX_Ingredients_19 ON dbo.[Ingredients]([IsActive],[Name]);

IF NOT EXISTS(SELECT 1 FROM sys.indexes WHERE name='IX_InventoryTransactions_20' AND object_id=OBJECT_ID('dbo.InventoryTransactions'))
    CREATE INDEX IX_InventoryTransactions_20 ON dbo.[InventoryTransactions]([IngredientID],[CreatedAt] DESC);

IF NOT EXISTS(SELECT 1 FROM sys.indexes WHERE name='IX_InventoryTransactions_21' AND object_id=OBJECT_ID('dbo.InventoryTransactions'))
    CREATE INDEX IX_InventoryTransactions_21 ON dbo.[InventoryTransactions]([CreatedAt] DESC);

-- ===== 5. TRIGGER: phiếu kho là append-only (không sửa, không xóa) =====
COMMIT; -- commit DDL trước để tạo trigger
GO
SET ANSI_NULLS ON; SET QUOTED_IDENTIFIER ON;
GO
CREATE OR ALTER TRIGGER dbo.TR_InventoryTransactions_Immutable
ON dbo.InventoryTransactions AFTER UPDATE, DELETE AS
BEGIN
    SET NOCOUNT ON;
    IF EXISTS(SELECT 1 FROM deleted)
        THROW 51001, N'Phiếu kho là append-only; sai sót thì lập phiếu kiểm kê.', 1;
END;
GO
BEGIN TRAN;

-- ===== 6. QUYỀN Inventory.Manage =====
IF NOT EXISTS(SELECT 1 FROM dbo.Permissions WHERE Code='Inventory.Manage')
    INSERT dbo.Permissions(Code,Description) VALUES('Inventory.Manage',N'Quản lý kho nguyên liệu');

-- Cấp quyền cho Owner (đã có mọi quyền) và BranchManager (nếu chưa có)
INSERT dbo.RolePermissions(RoleID,PermissionID)
SELECT r.RoleID, p.PermissionID
FROM dbo.Roles r CROSS JOIN dbo.Permissions p
WHERE p.Code = 'Inventory.Manage'
  AND r.Code IN ('Owner','BranchManager')
  AND NOT EXISTS(
      SELECT 1 FROM dbo.RolePermissions rp
      WHERE rp.RoleID=r.RoleID AND rp.PermissionID=p.PermissionID
  );

-- ===== 7. DỮ LIỆU MẪU (chỉ thêm khi bảng còn trống) =====
IF NOT EXISTS(SELECT 1 FROM dbo.Ingredients)
BEGIN
    -- Giả sử SupplierID của NCC-CF01, NCC-SU01, NCC-TR01, NCC-TC01, NCC-BK01 từ 13_ui_review
    DECLARE @cf  int = (SELECT SupplierID FROM dbo.Suppliers WHERE Code='NCC-CF01'),
            @su  int = (SELECT SupplierID FROM dbo.Suppliers WHERE Code='NCC-SU01'),
            @tr  int = (SELECT SupplierID FROM dbo.Suppliers WHERE Code='NCC-TR01'),
            @tc  int = (SELECT SupplierID FROM dbo.Suppliers WHERE Code='NCC-TC01'),
            @bk  int = (SELECT SupplierID FROM dbo.Suppliers WHERE Code='NCC-BK01');

    INSERT dbo.Ingredients(Code,Name,Unit,Quantity,MinStock,LastUnitCost,SupplierID,StockUpdatedAt) VALUES
    -- Cà phê
    ('NL-CF01', N'Cà phê Robusta',         N'kg',    15.500, 5.000,  180000, @cf, SYSUTCDATETIME()),
    ('NL-CF02', N'Cà phê Arabica',          N'kg',     8.200, 3.000,  320000, @cf, SYSUTCDATETIME()),
    -- Sữa
    ('NL-SU01', N'Sữa tươi không đường',    N'lít',   24.000, 10.000,  32000, @su, SYSUTCDATETIME()),
    ('NL-SU02', N'Sữa đặc có đường',        N'hộp',   48.000, 20.000,  22000, @su, SYSUTCDATETIME()),
    ('NL-SU03', N'Kem béo (whipping cream)',N'lít',    6.000,  3.000,  95000, @su, SYSUTCDATETIME()),
    -- Trà
    ('NL-TR01', N'Trà lài khô',             N'kg',     3.500,  1.500, 280000, @tr, SYSUTCDATETIME()),
    ('NL-TR02', N'Trà xanh bột (matcha)',   N'kg',     1.200,  0.500, 650000, @tr, SYSUTCDATETIME()),
    -- Trái cây
    ('NL-TC01', N'Đào tươi (peach)',        N'kg',     8.000,  3.000,  45000, @tc, SYSUTCDATETIME()),
    ('NL-TC02', N'Chanh tươi',              N'kg',     5.500,  2.000,  18000, @tc, SYSUTCDATETIME()),
    ('NL-TC03', N'Cam tươi',               N'kg',     4.000,  2.000,  22000, @tc, SYSUTCDATETIME()),
    -- Bánh & nguyên liệu làm bánh
    ('NL-BK01', N'Bột mì đa dụng',         N'kg',    10.000,  5.000,  18000, @bk, SYSUTCDATETIME()),
    ('NL-BK02', N'Cream cheese',           N'kg',     3.000,  1.000, 320000, @bk, SYSUTCDATETIME()),
    ('NL-BK03', N'Đường cát trắng',        N'kg',    12.000,  5.000,  18000, NULL, SYSUTCDATETIME()),
    -- Vật tư / bao bì (không có NCC tương ứng trong seed)
    ('NL-PP01', N'Ly nhựa 500ml',          N'cái',  350.000, 100.000,   800, NULL, SYSUTCDATETIME()),
    ('NL-PP02', N'Ống hút giấy',           N'cái',  500.000, 100.000,   200, NULL, SYSUTCDATETIME()),
    ('NL-PP03', N'Túi giấy take-away',     N'cái',  200.000,  50.000,  2500, NULL, SYSUTCDATETIME());

    -- Ghi phiếu nhập kho mẫu cho từng nguyên liệu vừa thêm
    -- (BalanceAfter = Quantity, Kind='Import', QuantityChange = Quantity)
    INSERT dbo.InventoryTransactions(IngredientID,Kind,QuantityChange,BalanceAfter,UnitCost,Note,CreatedAt)
    SELECT IngredientID,'Import',Quantity,Quantity,LastUnitCost,
           N'Nhập kho khởi tạo dữ liệu demo',
           DATEADD(day,-7,SYSUTCDATETIME())   -- 7 ngày trước để "tiền nhập 30 ngày" có số
    FROM dbo.Ingredients;

    -- Một số phiếu xuất dùng + hủy để lịch sử trông tự nhiên
    DECLARE @cf01 int = (SELECT IngredientID FROM dbo.Ingredients WHERE Code='NL-CF01'),
            @su01 int = (SELECT IngredientID FROM dbo.Ingredients WHERE Code='NL-SU01'),
            @su02 int = (SELECT IngredientID FROM dbo.Ingredients WHERE Code='NL-SU02'),
            @tr01 int = (SELECT IngredientID FROM dbo.Ingredients WHERE Code='NL-TR01');

    -- Xuất cà phê để pha (3 ngày trước)
    INSERT dbo.InventoryTransactions(IngredientID,Kind,QuantityChange,BalanceAfter,Note,CreatedAt)
    VALUES (@cf01,'Export',-2.000,13.500,N'Xuất pha chế quầy',DATEADD(day,-3,SYSUTCDATETIME())),
           (@su01,'Export',-5.000,19.000,N'Xuất pha chế quầy',DATEADD(day,-3,SYSUTCDATETIME())),
           (@su02,'Export',-8.000,40.000,N'Xuất pha chế quầy',DATEADD(day,-3,SYSUTCDATETIME())),
           (@tr01,'Export',-0.500, 3.000,N'Xuất pha chế quầy',DATEADD(day,-3,SYSUTCDATETIME()));

    -- Hủy hỏng sữa (hôm qua)
    INSERT dbo.InventoryTransactions(IngredientID,Kind,QuantityChange,BalanceAfter,Note,CreatedAt)
    VALUES (@su01,'Waste',-0.500,18.500,N'Sữa hết hạn, đổ bỏ',DATEADD(day,-1,SYSUTCDATETIME()));

    -- Cập nhật Quantity về số thực tế sau các phiếu xuất/hủy demo
    UPDATE dbo.Ingredients SET Quantity=13.500, StockUpdatedAt=SYSUTCDATETIME() WHERE IngredientID=@cf01;
    UPDATE dbo.Ingredients SET Quantity=18.500, StockUpdatedAt=SYSUTCDATETIME() WHERE IngredientID=@su01;
    UPDATE dbo.Ingredients SET Quantity=40.000, StockUpdatedAt=SYSUTCDATETIME() WHERE IngredientID=@su02;
    UPDATE dbo.Ingredients SET Quantity= 3.000, StockUpdatedAt=SYSUTCDATETIME() WHERE IngredientID=@tr01;
END;

COMMIT;
GO
SET NOEXEC OFF; -- [SQL-01] trả lại trạng thái bình thường cho cửa sổ SSMS
GO
