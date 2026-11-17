-- test_17_member_tiers.sql – kiểm thử hạng thành viên [CRM-01] (trước ở 17_member_tiers.sql, nay trong 01–07). Chỉ đọc + ghi trong giao dịch rồi ROLLBACK.
SET NOCOUNT ON; SET XACT_ABORT OFF;
IF OBJECT_ID(N'dbo.CustomerTiers','V') IS NULL BEGIN RAISERROR(N'Database chưa cài bằng fullscript bản mới (01–07).',16,1); RETURN; END;
DECLARE @pass int=0,@fail int=0;
-- TC-SQL-17-01: có hạng mặc định 0đ
IF EXISTS(SELECT 1 FROM dbo.MemberTiers WHERE MinSpent=0) BEGIN SET @pass+=1; PRINT N'PASS TC-SQL-17-01 Có hạng mặc định 0đ'; END
ELSE BEGIN SET @fail+=1; PRINT N'FAIL TC-SQL-17-01 Thiếu hạng 0đ'; END
-- TC-SQL-17-02: mọi khách đều có đúng 1 hạng
IF (SELECT COUNT(*) FROM dbo.CustomerTiers WHERE TierID IS NOT NULL)=(SELECT COUNT(*) FROM dbo.Customers)
   AND (SELECT COUNT(*) FROM dbo.CustomerTiers)=(SELECT COUNT(*) FROM dbo.Customers)
BEGIN SET @pass+=1; PRINT N'PASS TC-SQL-17-02 Mọi khách có đúng một hạng'; END
ELSE BEGIN SET @fail+=1; PRINT N'FAIL TC-SQL-17-02 Có khách thiếu hạng hoặc trùng dòng'; END
-- TC-SQL-17-03: hạng đúng ngưỡng (MinSpent ≤ chi tiêu < mức hạng kế tiếp)
IF NOT EXISTS(SELECT 1 FROM dbo.CustomerTiers ct JOIN dbo.MemberTiers t ON t.TierID=ct.TierID
              WHERE ct.Spent12M<t.MinSpent OR (ct.NextTierSpent IS NOT NULL AND ct.Spent12M>=ct.NextTierSpent))
BEGIN SET @pass+=1; PRINT N'PASS TC-SQL-17-03 Hạng của khách đúng ngưỡng chi tiêu'; END
ELSE BEGIN SET @fail+=1; PRINT N'FAIL TC-SQL-17-03 Hạng sai ngưỡng'; END
-- TC-SQL-17-04: hạ mức hạng Bạc về 1đ → khách có chi tiêu > 0 lên Bạc (trong giao dịch, ROLLBACK)
BEGIN TRAN;
UPDATE dbo.MemberTiers SET MinSpent=1 WHERE Code='Silver';
IF NOT EXISTS(SELECT 1 FROM dbo.CustomerTiers WHERE Spent12M>=1 AND Spent12M<COALESCE((SELECT MIN(MinSpent) FROM dbo.MemberTiers WHERE MinSpent>1),1e12) AND TierCode<>'Silver')
BEGIN SET @pass+=1; PRINT N'PASS TC-SQL-17-04 Đổi mức hạng áp dụng ngay cho khách'; END
ELSE BEGIN SET @fail+=1; PRINT N'FAIL TC-SQL-17-04 Đổi mức hạng chưa áp dụng'; END
ROLLBACK;
SELECT t.Name AS Hang,t.MinSpent,COUNT(ct.CustomerID) AS SoKhach FROM dbo.MemberTiers t LEFT JOIN dbo.CustomerTiers ct ON ct.TierID=t.TierID GROUP BY t.Name,t.MinSpent ORDER BY t.MinSpent;
PRINT CONCAT(N'=== KẾT QUẢ: ',@pass,N' PASS, ',@fail,N' FAIL (dữ liệu không thay đổi) ===');
