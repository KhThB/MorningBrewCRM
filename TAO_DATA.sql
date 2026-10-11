USE [MorningBrewCRM_AdminMC];
GO

SET NOCOUNT ON;
SET XACT_ABORT ON;
SET ANSI_NULLS ON;
SET QUOTED_IDENTIFIER ON;

BEGIN TRY
    BEGIN TRANSACTION;

    -- Ngăn hai lần chạy đồng thời tạo trùng dữ liệu.
    DECLARE @LockResult int;

    EXEC @LockResult = sys.sp_getapplock
        @Resource = N'MorningBrew.Seed.HCM16.v1',
        @LockMode = 'Exclusive',
        @LockOwner = 'Transaction',
        @LockTimeout = 10000;

    IF @LockResult < 0
        THROW 51000, N'Không lấy được khóa tạo dữ liệu. Thử lại sau.', 1;

    -- Nếu đã chạy thành công bộ dữ liệu trước đó thì bỏ qua.
    IF EXISTS (
        SELECT 1
        FROM dbo.AuditLogs
        WHERE Action = 'Seed.HCM16.v1'
    )
    BEGIN
        PRINT N'Bộ dữ liệu này đã được tạo. Không tạo trùng hoặc đặt lại mật khẩu.';
        COMMIT TRANSACTION;
        RETURN;
    END;

    -- Lấy vai trò có sẵn, không tự đổi quyền.
    DECLARE @AreaRole int =
        (SELECT RoleID FROM dbo.Roles WHERE Code = 'AreaManager');

    DECLARE @BranchRole int =
        (SELECT RoleID FROM dbo.Roles WHERE Code = 'BranchManager');

    DECLARE @StaffRole int =
        (SELECT RoleID FROM dbo.Roles WHERE Code = 'Staff');

    IF @AreaRole IS NULL OR @BranchRole IS NULL OR @StaffRole IS NULL
        THROW 51000, N'Database thiếu vai trò quản lý hoặc nhân viên.', 1;

    -- Giữ nguyên tài khoản chủ doanh nghiệp.
    DECLARE @OwnerID int = (
        SELECT MIN(u.UserID)
        FROM dbo.Users u
        JOIN dbo.Roles r ON r.RoleID = u.RoleID
        WHERE r.Code = 'Owner'
          AND u.IsActive = 1
    );

    IF @OwnerID IS NULL
        THROW 51000, N'Chưa có tài khoản chủ doanh nghiệp đang hoạt động.', 1;

    -- Lưu dữ liệu tài khoản cũ để đối chiếu trước khi hoàn tất.
    SELECT
        UserID,
        RoleID,
        Email,
        NormalizedEmail,
        PasswordHash,
        FullName,
        Phone,
        IsActive,
        CreatedAt
    INTO #OldUsers
    FROM dbo.Users;

    -- =====================================================
    -- 1. DANH SÁCH 16 QUẬN, MỖI QUẬN 5 ĐƯỜNG
    -- Mốc địa giới: sau năm 2021, trước sắp xếp năm 2025.
    -- Không bao gồm 5 huyện và TP Thủ Đức.
    -- Quán, nhân sự và số nhà là dữ liệu giả lập.
    -- =====================================================

    CREATE TABLE #Districts (
        ID int PRIMARY KEY,
        Code varchar(12) NOT NULL UNIQUE,
        Name nvarchar(120) NOT NULL UNIQUE,
        Streets nvarchar(max) NOT NULL
    );

    INSERT INTO #Districts(ID, Code, Name, Streets)
    VALUES
    (
        1, 'Q01', N'Quận 1',
        N'["Nguyễn Huệ","Lê Lợi","Đồng Khởi","Hàm Nghi","Tôn Đức Thắng"]'
    ),
    (
        2, 'Q03', N'Quận 3',
        N'["Võ Văn Tần","Nguyễn Đình Chiểu","Cao Thắng","Bà Huyện Thanh Quan","Tú Xương"]'
    ),
    (
        3, 'Q04', N'Quận 4',
        N'["Hoàng Diệu","Khánh Hội","Bến Vân Đồn","Tôn Đản","Đoàn Văn Bơ"]'
    ),
    (
        4, 'Q05', N'Quận 5',
        N'["Trần Hưng Đạo","Nguyễn Trãi","An Dương Vương","Châu Văn Liêm","Hải Thượng Lãn Ông"]'
    ),
    (
        5, 'Q06', N'Quận 6',
        N'["Hậu Giang","Bà Hom","Bình Phú","Phạm Văn Chí","Minh Phụng"]'
    ),
    (
        6, 'Q07', N'Quận 7',
        N'["Nguyễn Thị Thập","Huỳnh Tấn Phát","Nguyễn Lương Bằng","Nguyễn Đức Cảnh","Tân Mỹ"]'
    ),
    (
        7, 'Q08', N'Quận 8',
        N'["Dương Bá Trạc","Phạm Thế Hiển","Tạ Quang Bửu","Âu Dương Lân","Bùi Minh Trực"]'
    ),
    (
        8, 'Q10', N'Quận 10',
        N'["Sư Vạn Hạnh","Thành Thái","Tô Hiến Thành","Ba Tháng Hai","Nguyễn Tri Phương"]'
    ),
    (
        9, 'Q11', N'Quận 11',
        N'["Lạc Long Quân","Bình Thới","Lê Đại Hành","Ông Ích Khiêm","Hàn Hải Nguyên"]'
    ),
    (
        10, 'Q12', N'Quận 12',
        N'["Nguyễn Ảnh Thủ","Lê Văn Khương","Hà Huy Giáp","Tô Ký","Dương Thị Mười"]'
    ),
    (
        11, 'BTAN', N'Quận Bình Tân',
        N'["Tên Lửa","Kinh Dương Vương","Lê Văn Quới","Bình Long","Hồ Học Lãm"]'
    ),
    (
        12, 'BTHANH', N'Quận Bình Thạnh',
        N'["Xô Viết Nghệ Tĩnh","Đinh Bộ Lĩnh","Ung Văn Khiêm","Nguyễn Gia Trí","Bình Quới"]'
    ),
    (
        13, 'GV', N'Quận Gò Vấp',
        N'["Quang Trung","Phan Văn Trị","Nguyễn Oanh","Lê Đức Thọ","Nguyễn Văn Lượng"]'
    ),
    (
        14, 'PN', N'Quận Phú Nhuận',
        N'["Phan Xích Long","Phan Đăng Lưu","Huỳnh Văn Bánh","Hoa Sứ","Trương Quốc Dung"]'
    ),
    (
        15, 'TB', N'Quận Tân Bình',
        N'["Cộng Hòa","Hoàng Văn Thụ","Trường Sơn","Bàu Cát","Út Tịch"]'
    ),
    (
        16, 'TP', N'Quận Tân Phú',
        N'["Tân Sơn Nhì","Lũy Bán Bích","Tân Kỳ Tân Quý","Gò Dầu","Tây Thạnh"]'
    );

    -- =====================================================
    -- 2. TẠO DANH SÁCH 80 QUÁN TRONG BẢNG TẠM
    -- =====================================================

    CREATE TABLE #Shops (
        ID int PRIMARY KEY,
        DistrictID int NOT NULL,
        Code varchar(30) NOT NULL UNIQUE,
        Name nvarchar(150) NOT NULL,
        Address nvarchar(500) NOT NULL
    );

    INSERT INTO #Shops(ID, DistrictID, Code, Name, Address)
    SELECT
        (d.ID - 1) * 5 + CONVERT(int, j.[key]) + 1,
        d.ID,
        CONCAT(
            'MBHCM-',
            d.Code,
            '-',
            RIGHT('0' + CONVERT(varchar(2), CONVERT(int, j.[key]) + 1), 2)
        ),
        N'Morning Brew ' + CONVERT(nvarchar(150), j.[value]),
        CONCAT(
            32 + CONVERT(int, j.[key]) * 12,
            N' ',
            j.[value],
            N', ',
            d.Name,
            N', TP.HCM (địa chỉ giả lập)'
        )
    FROM #Districts d
    CROSS APPLY OPENJSON(d.Streets) j;

    -- =====================================================
    -- 3. TẠO 800 HỌ TÊN NHÂN VIÊN KHÁC NHAU
    -- 20 họ × 40 tên = 800 họ tên.
    -- =====================================================

    CREATE TABLE #FamilyNames (
        ID int PRIMARY KEY,
        Name nvarchar(30) NOT NULL
    );

    INSERT INTO #FamilyNames(ID, Name)
    VALUES
        (1, N'Nguyễn'),
        (2, N'Trần'),
        (3, N'Lê'),
        (4, N'Phạm'),
        (5, N'Hoàng'),
        (6, N'Huỳnh'),
        (7, N'Phan'),
        (8, N'Vũ'),
        (9, N'Võ'),
        (10, N'Đặng'),
        (11, N'Bùi'),
        (12, N'Đỗ'),
        (13, N'Hồ'),
        (14, N'Ngô'),
        (15, N'Dương'),
        (16, N'Lý'),
        (17, N'Đinh'),
        (18, N'Trương'),
        (19, N'Đào'),
        (20, N'Mai');

    CREATE TABLE #GivenNames (
        ID int PRIMARY KEY,
        Name nvarchar(60) NOT NULL
    );

    INSERT INTO #GivenNames(ID, Name)
    VALUES
        (1, N'Minh An'),
        (2, N'Quốc Anh'),
        (3, N'Tuấn Anh'),
        (4, N'Đức Bảo'),
        (5, N'Gia Bảo'),
        (6, N'Minh Châu'),
        (7, N'Ngọc Diệp'),
        (8, N'Thùy Dung'),
        (9, N'Tiến Dũng'),
        (10, N'Khánh Duy'),
        (11, N'Hoàng Giang'),
        (12, N'Thu Hà'),
        (13, N'Ngọc Hân'),
        (14, N'Thanh Hằng'),
        (15, N'Quang Hậu'),
        (16, N'Trung Hiếu'),
        (17, N'Minh Hoàng'),
        (18, N'Gia Huy'),
        (19, N'Thanh Hương'),
        (20, N'Bảo Khang'),
        (21, N'Minh Khánh'),
        (22, N'Đăng Khoa'),
        (23, N'Anh Kiệt'),
        (24, N'Ngọc Lan'),
        (25, N'Hoàng Linh'),
        (26, N'Khánh Linh'),
        (27, N'Nhật Minh'),
        (28, N'Hoài Nam'),
        (29, N'Kim Ngân'),
        (30, N'Thảo Nguyên'),
        (31, N'Quỳnh Như'),
        (32, N'Minh Phúc'),
        (33, N'Đức Quân'),
        (34, N'Ngọc Quỳnh'),
        (35, N'Thanh Sơn'),
        (36, N'Phương Thảo'),
        (37, N'Anh Thư'),
        (38, N'Ngọc Trâm'),
        (39, N'Minh Trí'),
        (40, N'Hoàng Yến');

    CREATE TABLE #Accounts (
        AccountKey varchar(40) PRIMARY KEY,
        RoleID int NOT NULL,
        RoleCode varchar(30) NOT NULL,
        FullName nvarchar(120) NOT NULL,
        Email nvarchar(254) NULL,
        DistrictID int NOT NULL,
        ShopID int NULL
    );

    -- 16 quản lý khu vực: Morning Brew + tên quận.
    INSERT INTO #Accounts (
        AccountKey, RoleID, RoleCode, FullName, DistrictID, ShopID
    )
    SELECT
        CONCAT('AREA-', ID),
        @AreaRole,
        'AreaManager',
        N'Morning Brew ' + Name,
        ID,
        NULL
    FROM #Districts;

    -- 80 quản lý chi nhánh: Morning Brew + tên đường.
    INSERT INTO #Accounts (
        AccountKey, RoleID, RoleCode, FullName, DistrictID, ShopID
    )
    SELECT
        CONCAT('BRANCH-', ID),
        @BranchRole,
        'BranchManager',
        Name,
        DistrictID,
        ID
    FROM #Shops;

    -- Mỗi quán 10 nhân viên.
    DECLARE @EmployeeNumber int = 1;

    WHILE @EmployeeNumber <= 800
    BEGIN
        DECLARE @ShopID int = ((@EmployeeNumber - 1) / 10) + 1;

        -- Trộn thứ tự họ tên để nhân viên cùng quán có nhiều họ khác nhau.
        DECLARE @NameIndex int = ((@EmployeeNumber - 1) * 137) % 800;

        DECLARE @FamilyID int = (@NameIndex / 40) + 1;
        DECLARE @GivenID int = (@NameIndex % 40) + 1;

        INSERT INTO #Accounts (
            AccountKey,
            RoleID,
            RoleCode,
            FullName,
            DistrictID,
            ShopID
        )
        SELECT
            CONCAT('STAFF-', @EmployeeNumber),
            @StaffRole,
            'Staff',
            f.Name + N' ' + g.Name,
            s.DistrictID,
            s.ID
        FROM #FamilyNames f
        CROSS JOIN #GivenNames g
        JOIN #Shops s ON s.ID = @ShopID
        WHERE f.ID = @FamilyID
          AND g.ID = @GivenID;

        SET @EmployeeNumber += 1;
    END;

    -- =====================================================
    -- 4. EMAIL = TÊN KHÔNG DẤU, KHÔNG KHOẢNG TRẮNG
    -- =====================================================

    UPDATE #Accounts
    SET Email = LOWER(REPLACE(FullName, N' ', N''));

    CREATE TABLE #AccentGroups (
        ID int PRIMARY KEY,
        Letters nvarchar(30) NOT NULL,
        Replacement nchar(1) NOT NULL
    );

    INSERT INTO #AccentGroups(ID, Letters, Replacement)
    VALUES
        (1, N'àáạảãâầấậẩẫăằắặẳẵ', N'a'),
        (2, N'èéẹẻẽêềếệểễ', N'e'),
        (3, N'ìíịỉĩ', N'i'),
        (4, N'òóọỏõôồốộổỗơờớợởỡ', N'o'),
        (5, N'ùúụủũưừứựửữ', N'u'),
        (6, N'ỳýỵỷỹ', N'y'),
        (7, N'đ', N'd');

    DECLARE @GroupID int = 1;

    WHILE @GroupID <= 7
    BEGIN
        DECLARE @Letters nvarchar(30);
        DECLARE @Replacement nchar(1);
        DECLARE @Position int = 1;

        SELECT
            @Letters = Letters,
            @Replacement = Replacement
        FROM #AccentGroups
        WHERE ID = @GroupID;

        WHILE @Position <= LEN(@Letters)
        BEGIN
            UPDATE #Accounts
            SET Email = REPLACE(
                Email COLLATE Latin1_General_100_BIN2,
                SUBSTRING(@Letters, @Position, 1),
                @Replacement
            );

            SET @Position += 1;
        END;

        SET @GroupID += 1;
    END;

    UPDATE #Accounts
    SET Email = Email + N'@morningbrew.test';

    -- =====================================================
    -- 5. KIỂM TRA TRÙNG TRƯỚC KHI GHI DATABASE
    -- =====================================================

    IF (SELECT COUNT(*) FROM #Accounts) <> 896
        THROW 51000, N'Số lượng tài khoản tạo ra không đúng 896.', 1;

    IF EXISTS (
        SELECT Email
        FROM #Accounts
        GROUP BY Email
        HAVING COUNT(*) > 1
    )
        THROW 51000, N'Bộ dữ liệu có email bị trùng.', 1;

    IF EXISTS (
        SELECT 1
        FROM #Accounts a
        JOIN dbo.Users u ON u.NormalizedEmail = UPPER(a.Email)
    )
        THROW 51000, N'Email đã tồn tại trong database. Chưa ghi dữ liệu mới.', 1;

    IF EXISTS (
        SELECT 1
        FROM #Districts d
        JOIN dbo.Areas a ON a.Name = d.Name
    )
        THROW 51000, N'Tên khu vực đã tồn tại. Chưa ghi dữ liệu mới.', 1;

    IF EXISTS (
        SELECT 1
        FROM #Shops s
        JOIN dbo.Branches b
            ON b.Code = s.Code
            OR b.Name = s.Name
    )
        THROW 51000, N'Mã hoặc tên quán đã tồn tại. Chưa ghi dữ liệu mới.', 1;

    -- =====================================================
    -- 6. THÊM TÀI KHOẢN
    -- Hash mật khẩu MorningBrew!2026 theo ASP.NET Identity V3.
    -- Bản SQL ngắn này dùng chung hash cho tài khoản giả lập.
    -- Không thay đổi mật khẩu tài khoản cũ.
    -- =====================================================

    DECLARE @PasswordHash varchar(512) =
        'AQAAAAIAAYagAAAAEE8G2F5hjA72TxlV9NrR092YRGzNf1+QMFGndUjRlWxuS/DMviEjHdzD5daFtHJW2w==';

    INSERT INTO dbo.Users (
        RoleID,
        Email,
        NormalizedEmail,
        PasswordHash,
        FullName,
        Phone
    )
    SELECT
        RoleID,
        Email,
        UPPER(Email),
        @PasswordHash,
        FullName,
        NULL
    FROM #Accounts;

    -- =====================================================
    -- 7. THÊM KHU VỰC VÀ PHÂN CÔNG QUẢN LÝ KHU VỰC
    -- =====================================================

    INSERT INTO dbo.Areas(Name)
    SELECT Name
    FROM #Districts;

    INSERT INTO dbo.AreaManagers(AreaID, UserID)
    SELECT
        ar.AreaID,
        u.UserID
    FROM #Accounts ac
    JOIN #Districts d ON d.ID = ac.DistrictID
    JOIN dbo.Areas ar ON ar.Name = d.Name
    JOIN dbo.Users u ON u.NormalizedEmail = UPPER(ac.Email)
    WHERE ac.RoleCode = 'AreaManager';

    -- =====================================================
    -- 8. THÊM 80 QUÁN VÀ QUẢN LÝ CHI NHÁNH
    -- =====================================================

    INSERT INTO dbo.Branches (
        AreaID,
        ManagerID,
        Code,
        Name,
        Address,
        Phone,
        OpeningTime,
        ClosingTime,
        DeliveryFee
    )
    SELECT
        ar.AreaID,
        u.UserID,
        s.Code,
        s.Name,
        s.Address,
        '0000000000',
        '06:00',
        '23:00',
        15000
    FROM #Shops s
    JOIN #Districts d ON d.ID = s.DistrictID
    JOIN dbo.Areas ar ON ar.Name = d.Name
    JOIN #Accounts ac
        ON ac.ShopID = s.ID
       AND ac.RoleCode = 'BranchManager'
    JOIN dbo.Users u ON u.NormalizedEmail = UPPER(ac.Email);

    -- =====================================================
    -- 9. PHÂN CÔNG 10 NHÂN VIÊN CHO MỖI QUÁN
    -- =====================================================

    INSERT INTO dbo.StaffBranches(UserID, BranchID)
    SELECT
        u.UserID,
        b.BranchID
    FROM #Accounts ac
    JOIN #Shops s ON s.ID = ac.ShopID
    JOIN dbo.Branches b ON b.Code = s.Code
    JOIN dbo.Users u ON u.NormalizedEmail = UPPER(ac.Email)
    WHERE ac.RoleCode = 'Staff';

    -- =====================================================
    -- 10. KIỂM TRA KẾT QUẢ TRƯỚC KHI LƯU
    -- =====================================================

    IF (
        SELECT COUNT(*)
        FROM #Shops s
        JOIN dbo.Branches b ON b.Code = s.Code
    ) <> 80
        THROW 51000, N'Không tạo đủ 80 quán.', 1;

    IF (
        SELECT COUNT(*)
        FROM #Accounts ac
        JOIN dbo.Users u ON u.NormalizedEmail = UPPER(ac.Email)
    ) <> 896
        THROW 51000, N'Không tạo đủ 896 tài khoản.', 1;

    IF EXISTS (
        SELECT b.BranchID
        FROM #Shops s
        JOIN dbo.Branches b ON b.Code = s.Code
        LEFT JOIN dbo.StaffBranches sb
            ON sb.BranchID = b.BranchID
           AND sb.IsActive = 1
        GROUP BY b.BranchID
        HAVING COUNT(sb.UserID) <> 10
    )
        THROW 51000, N'Có quán không đủ đúng 10 nhân viên.', 1;

    -- Mọi tài khoản cũ phải giữ nguyên.
    IF EXISTS (
        SELECT
            UserID, RoleID, Email, NormalizedEmail,
            PasswordHash, FullName, Phone, IsActive, CreatedAt
        FROM #OldUsers

        EXCEPT

        SELECT
            UserID, RoleID, Email, NormalizedEmail,
            PasswordHash, FullName, Phone, IsActive, CreatedAt
        FROM dbo.Users
    )
        THROW 51000, N'Tài khoản cũ bị thay đổi. Hủy giao dịch.', 1;

    -- Đánh dấu để lần chạy sau không tạo trùng.
    INSERT INTO dbo.AuditLogs (
        ActorID,
        Action,
        EntityType,
        Details
    )
    VALUES (
        @OwnerID,
        'Seed.HCM16.v1',
        'SeedBatch',
        N'Tạo 16 khu vực, 80 quán, 800 nhân viên, 80 quản lý chi nhánh, 16 quản lý khu vực. Giữ nguyên tài khoản cũ.'
    );

    COMMIT TRANSACTION;

    PRINT N'TẠO DỮ LIỆU THÀNH CÔNG.';
    PRINT N'16 khu vực — 80 quán — 896 tài khoản mới.';
    PRINT N'Mật khẩu tài khoản mới: MorningBrew!2026';
    PRINT N'Tài khoản chủ doanh nghiệp giữ nguyên.';

    -- Thống kê theo vai trò.
    SELECT
        RoleCode AS VaiTro,
        COUNT(*) AS SoTaiKhoanMoi
    FROM #Accounts
    GROUP BY RoleCode;

    -- Danh sách tài khoản vừa tạo.
    SELECT
        d.Name AS KhuVuc,
        s.Name AS Quan,
        ac.RoleCode AS VaiTro,
        ac.FullName AS TenHienThi,
        ac.Email AS EmailDangNhap
    FROM #Accounts ac
    JOIN #Districts d ON d.ID = ac.DistrictID
    LEFT JOIN #Shops s ON s.ID = ac.ShopID
    ORDER BY
        d.ID,
        CASE ac.RoleCode
            WHEN 'AreaManager' THEN 1
            WHEN 'BranchManager' THEN 2
            ELSE 3
        END,
        s.ID,
        ac.FullName;

END TRY
BEGIN CATCH
    IF XACT_STATE() <> 0
        ROLLBACK TRANSACTION;

    THROW;
END CATCH;
GO