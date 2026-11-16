-- Đợt sửa lỗi theo docs/BugReport_MorningBrew_App-Admin.xlsx. Chạy lặp lại được, áp dụng sau 01–11.
-- Database đã có:
--   sqlcmd -S "(localdb)\MSSQLLocalDB" -d MorningBrewCRM_Crm -E -C -I -b -f 65001 -i database\sql\12_bugfix.sql
SET NOCOUNT ON; SET XACT_ABORT ON; SET QUOTED_IDENTIFIER ON; SET ANSI_NULLS ON;
GO
-- [BUG-06] Tách lỗi "sai trạng thái" (51124 → HTTP 409, thông báo tiếng Việt) khỏi lỗi "thiếu quyền" (51103 → 403).
-- [BUG-07] Không cho phát hành khảo sát đã quá ngày kết thúc.
CREATE OR ALTER PROCEDURE dbo.PublishSurvey @ActorID int,@SurveyID int
AS
BEGIN
 SET NOCOUNT ON; SET XACT_ABORT ON;
 BEGIN TRY
 BEGIN TRAN;
 DECLARE @status varchar(10),@scope varchar(10),@area int,@branch int,@ends datetime2(3);
 SELECT @status=Status,@scope=Scope,@area=AreaID,@branch=BranchID,@ends=EndsAt FROM dbo.Surveys WITH(UPDLOCK,HOLDLOCK) WHERE SurveyID=@SurveyID;
 IF @status IS NULL THROW 51104,N'Không tìm thấy khảo sát.',1;
 IF dbo.CanScope(@ActorID,@scope,@area,@branch,'Surveys.Manage')=0 THROW 51103,N'Survey outside scope.',1;
 IF @status<>'Draft' THROW 51124,N'Khảo sát đã được phát hành hoặc đã đóng, không phát hành lại được.',1;
 IF @ends<=SYSUTCDATETIME() THROW 51124,N'Khảo sát đã quá ngày kết thúc, không thể phát hành.',1; -- [BUG-07]
 UPDATE dbo.Surveys SET Status='Open' WHERE SurveyID=@SurveyID;
 INSERT dbo.AuditLogs(ActorID,BranchID,Action,EntityType,EntityID) VALUES(@ActorID,@branch,'Survey.Publish','Surveys',@SurveyID);
 COMMIT;
 END TRY BEGIN CATCH IF @@TRANCOUNT>0 ROLLBACK; THROW; END CATCH;
END;
GO
-- [BUG-06] Mời vào khảo sát chưa phát hành / đã đóng / hết hạn → 51124 thay vì 51103.
CREATE OR ALTER PROCEDURE dbo.InviteSurvey @ActorID int,@SurveyID int,@CustomerID int
AS
BEGIN
 SET NOCOUNT ON; SET XACT_ABORT ON;
 BEGIN TRY
 BEGIN TRAN;
 DECLARE @status varchar(10),@scope varchar(10),@area int,@branch int,@ends datetime2(3);
 SELECT @status=Status,@scope=Scope,@area=AreaID,@branch=BranchID,@ends=EndsAt FROM dbo.Surveys WITH(UPDLOCK,HOLDLOCK) WHERE SurveyID=@SurveyID;
 IF @status IS NULL THROW 51104,N'Không tìm thấy khảo sát.',1;
 IF dbo.CanScope(@ActorID,@scope,@area,@branch,'Surveys.Manage')=0 THROW 51103,N'Survey outside scope.',1;
 IF @status<>'Open' OR @ends<=SYSUTCDATETIME() THROW 51124,N'Khảo sát chưa phát hành, đã đóng hoặc đã hết hạn nên không mời thêm được.',1;
 IF dbo.CustomerInScope(@CustomerID,@scope,@area,@branch)=0 THROW 51104,N'Không tìm thấy khách hàng.',1;
 IF EXISTS(SELECT 1 FROM dbo.SurveyRecipients WHERE SurveyID=@SurveyID AND CustomerID=@CustomerID) BEGIN COMMIT; RETURN; END;
 INSERT dbo.SurveyRecipients(SurveyID,CustomerID) VALUES(@SurveyID,@CustomerID);
 INSERT dbo.Notifications(UserID,Title,Body,SurveyID) SELECT c.UserID,N'Khảo sát mới',s.Title,@SurveyID FROM dbo.Customers c CROSS JOIN dbo.Surveys s WHERE c.CustomerID=@CustomerID AND s.SurveyID=@SurveyID;
 COMMIT;
 END TRY BEGIN CATCH IF @@TRANCOUNT>0 ROLLBACK; THROW; END CATCH;
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
-- Đổi mã các đơn đã có sang dạng ngắn (chạy lại không đổi gì vì chỉ áp dụng cho mã dạng cũ 'MB-…').
UPDATE dbo.Orders SET OrderCode=CONCAT('MB',FORMAT(DATEADD(hour,7,CreatedAt),'yyMMdd'),'-',
  CASE WHEN OrderID>9999 THEN CONVERT(varchar(12),OrderID) ELSE RIGHT(CONCAT('0000',OrderID),4) END)
WHERE OrderCode LIKE 'MB-%';
GO
-- [BUG-09] Không cho sửa phản hồi đã giải quyết (trước đây trả lời lại sẽ ghi đè câu trả lời cũ).
CREATE OR ALTER PROCEDURE dbo.HandleFeedback @ActorID int,@FeedbackID int,@Status varchar(12),@Response nvarchar(4000)=NULL
AS
BEGIN
 SET NOCOUNT ON; SET XACT_ABORT ON;
 BEGIN TRY
 BEGIN TRAN;
 DECLARE @branch int,@customer int,@old varchar(12);
 SELECT @branch=BranchID,@customer=CustomerID,@old=Status FROM dbo.Feedbacks WITH(UPDLOCK,HOLDLOCK) WHERE FeedbackID=@FeedbackID;
 IF @customer IS NULL OR (@branch IS NULL AND dbo.CanScope(@ActorID,'Chain',NULL,NULL,'Feedbacks.Manage')=0) OR (@branch IS NOT NULL AND dbo.CanBranch(@ActorID,@branch,'Feedbacks.Manage')=0) THROW 51103,N'Feedback is outside scope.',1;
 -- [BUG-09] Phản hồi đã giải quyết thì khóa: không đổi trạng thái, không ghi đè câu trả lời đã gửi khách.
 IF @old='Resolved' THROW 51124,N'Phản hồi đã được giải quyết, không thể sửa trạng thái hoặc câu trả lời.',1;
 IF @Status NOT IN ('Processing','Resolved') OR (@Status='Resolved' AND NULLIF(@Response,'') IS NULL) THROW 51100,N'Resolved feedback needs a response.',1;
 UPDATE dbo.Feedbacks SET Status=@Status,HandledBy=@ActorID,ResponseContent=@Response,HandledAt=SYSUTCDATETIME() WHERE FeedbackID=@FeedbackID;
 INSERT dbo.Notifications(UserID,Title,Body) SELECT UserID,N'Cập nhật phản hồi',COALESCE(@Response,N'Phản hồi đang được xử lý.') FROM dbo.Customers WHERE CustomerID=@customer;
 INSERT dbo.AuditLogs(ActorID,BranchID,Action,EntityType,EntityID,Details) VALUES(@ActorID,@branch,'Feedback.Handle','Feedbacks',@FeedbackID,@Status);
 COMMIT;
 END TRY BEGIN CATCH IF @@TRANCOUNT>0 ROLLBACK; THROW; END CATCH;
END;
GO
