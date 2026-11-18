-- test_16_crm.sql – kiểm thử SQL cho quyền, CustomerStats, SĐT khách [CRM-00][CRM-02][CRM-08][CRM-09] (trước ở 16_crm.sql, nay trong 01–07) trên database có dữ liệu mẫu.
-- KHÔNG làm thay đổi dữ liệu: các thao tác ghi đều nằm trong giao dịch riêng và ROLLBACK (lỗi trong trigger hủy cả giao dịch nên mỗi ca một giao dịch). SSMS: chọn database → F5 → đọc tab Messages.
SET NOCOUNT ON; SET XACT_ABORT OFF;
IF OBJECT_ID(N'dbo.CustomerStats','V') IS NULL OR OBJECT_ID(N'dbo.TR_Users_CustomerPhone','TR') IS NULL
BEGIN RAISERROR(N'Database chưa cài bằng fullscript bản mới (01–07).',16,1); RETURN; END;
DECLARE @pass int=0,@fail int=0,@n int,@err int;
DECLARE @customerRole int=(SELECT RoleID FROM dbo.Roles WHERE Code='Customer'),@staffRole int=(SELECT RoleID FROM dbo.Roles WHERE Code='Staff');
DECLARE @lanPhone varchar(20)=(SELECT Phone FROM dbo.Users WHERE Email='lan@morningbrew.test'),
        @minh int=(SELECT UserID FROM dbo.Users WHERE Email='minh@morningbrew.test');
-- TC-SQL-16-01: bỏ quyền Organization.Manage
IF NOT EXISTS(SELECT 1 FROM dbo.Permissions WHERE Code='Organization.Manage') BEGIN SET @pass+=1; PRINT N'PASS TC-SQL-16-01 Không còn quyền Organization.Manage'; END
ELSE BEGIN SET @fail+=1; PRINT N'FAIL TC-SQL-16-01 Vẫn còn quyền Organization.Manage'; END
-- TC-SQL-16-02: quyền Loyalty.Adjust có cho chủ cửa hàng, quản lý mặc định chưa có
IF EXISTS(SELECT 1 FROM dbo.RolePermissions rp JOIN dbo.Roles r ON r.RoleID=rp.RoleID JOIN dbo.Permissions p ON p.PermissionID=rp.PermissionID WHERE r.Code='Owner' AND p.Code='Loyalty.Adjust')
BEGIN SET @pass+=1; PRINT N'PASS TC-SQL-16-02 Chủ cửa hàng có quyền Loyalty.Adjust'; END
ELSE BEGIN SET @fail+=1; PRINT N'FAIL TC-SQL-16-02 Thiếu quyền Loyalty.Adjust cho chủ cửa hàng'; END
-- TC-SQL-16-03: CustomerStats có đủ khách, nhóm hợp lệ, tổng chi tiêu không âm
SELECT @n=COUNT(*) FROM dbo.CustomerStats s WHERE s.Segment NOT IN ('None','New','Active','Loyal','Lapsed') OR s.TotalSpent<0;
IF @n=0 AND (SELECT COUNT(*) FROM dbo.CustomerStats)=(SELECT COUNT(*) FROM dbo.Customers)
BEGIN SET @pass+=1; PRINT N'PASS TC-SQL-16-03 CustomerStats đủ khách, nhóm và tổng chi tiêu hợp lệ'; END
ELSE BEGIN SET @fail+=1; PRINT N'FAIL TC-SQL-16-03 CustomerStats sai'; END
SELECT Segment,COUNT(*) AS SoKhach,SUM(TotalSpent) AS TongChiTieu FROM dbo.CustomerStats GROUP BY Segment;
-- TC-SQL-16-04: tạo khách mới trùng SĐT → bị chặn
BEGIN TRAN;
BEGIN TRY
    INSERT dbo.Users(RoleID,Email,NormalizedEmail,FullName,Phone,PasswordHash) VALUES(@customerRole,'trung.sdt@test.vn','TRUNG.SDT@TEST.VN',N'Trùng SĐT',@lanPhone,'x');
    SET @fail+=1; PRINT N'FAIL TC-SQL-16-04 Tạo được khách trùng SĐT';
END TRY BEGIN CATCH
    IF ERROR_NUMBER()=51120 BEGIN SET @pass+=1; PRINT N'PASS TC-SQL-16-04 Tạo khách trùng SĐT → lỗi 51120'; END
    ELSE BEGIN SET @fail+=1; PRINT CONCAT(N'FAIL TC-SQL-16-04 Lỗi khác: ',ERROR_MESSAGE()); END
END CATCH;
IF @@TRANCOUNT>0 ROLLBACK;
-- TC-SQL-16-05: sửa SĐT khách khác thành SĐT đã có → bị chặn
BEGIN TRAN;
BEGIN TRY
    UPDATE dbo.Users SET Phone=@lanPhone WHERE UserID=@minh;
    SET @fail+=1; PRINT N'FAIL TC-SQL-16-05 Sửa được SĐT trùng';
END TRY BEGIN CATCH
    IF ERROR_NUMBER()=51120 BEGIN SET @pass+=1; PRINT N'PASS TC-SQL-16-05 Sửa SĐT khách thành số đã có → lỗi 51120'; END
    ELSE BEGIN SET @fail+=1; PRINT CONCAT(N'FAIL TC-SQL-16-05 Lỗi khác: ',ERROR_MESSAGE()); END
END CATCH;
IF @@TRANCOUNT>0 ROLLBACK;
-- TC-SQL-16-06: nhân viên được phép trùng SĐT với khách
BEGIN TRAN;
BEGIN TRY
    INSERT dbo.Users(RoleID,Email,NormalizedEmail,FullName,Phone,PasswordHash) VALUES(@staffRole,'nv.trung@test.vn','NV.TRUNG@TEST.VN',N'NV trùng SĐT',@lanPhone,'x');
    SET @pass+=1; PRINT N'PASS TC-SQL-16-06 Nhân viên dùng chung SĐT với khách vẫn được';
END TRY BEGIN CATCH
    SET @fail+=1; PRINT CONCAT(N'FAIL TC-SQL-16-06 ',ERROR_MESSAGE());
END CATCH;
IF @@TRANCOUNT>0 ROLLBACK;
PRINT CONCAT(N'=== KẾT QUẢ: ',@pass,N' PASS, ',@fail,N' FAIL (đã ROLLBACK, dữ liệu không thay đổi) ===');
