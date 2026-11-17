-- 04_crm.sql – nghiệp vụ CRM: khảo sát, địa chỉ, phản hồi, xóa mềm tài khoản.
-- [GOP-01] Bản gộp: PublishSurvey, InviteSurvey, HandleFeedback lấy bản sửa lỗi (trước ở 12_bugfix.sql);
--          bỏ SetBranchPrice (giá theo chi nhánh đã ngừng); thêm SoftDeleteUser bản cuối (trước ở 10 và 11).
SET ANSI_NULLS ON;
SET QUOTED_IDENTIFIER ON;
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
-- Whole form, atomic submission. Omit unanswered optional questions.
-- Answers: [{"questionId":1,"optionIds":[1,2]},{"questionId":2,"rating":5},{"questionId":3,"text":"..."}]
CREATE OR ALTER PROCEDURE dbo.SubmitSurvey @ActorID int,@SurveyID int,@CustomerID int,@Answers nvarchar(max)
AS
BEGIN
 SET NOCOUNT ON; SET XACT_ABORT ON;
 BEGIN TRY
 BEGIN TRAN;
 IF dbo.IsCustomer(@ActorID,@CustomerID)=0 THROW 51103,N'Cannot submit for another customer.',1;
 IF NOT EXISTS(SELECT 1 FROM dbo.Surveys WITH(UPDLOCK,HOLDLOCK) WHERE SurveyID=@SurveyID AND Status='Open' AND StartsAt<=SYSUTCDATETIME() AND EndsAt>SYSUTCDATETIME()) THROW 51100,N'Survey is not open.',1;
 IF NOT EXISTS(SELECT 1 FROM dbo.SurveyRecipients WITH(UPDLOCK,HOLDLOCK) WHERE SurveyID=@SurveyID AND CustomerID=@CustomerID AND CompletedAt IS NULL) THROW 51109,N'No invitation or already submitted.',1;
 IF ISJSON(@Answers)<>1 OR LEFT(LTRIM(@Answers),1)<>'[' THROW 51100,N'Answers must be an array.',1;
 DECLARE @a TABLE(QuestionID int PRIMARY KEY,TextValue nvarchar(4000),RatingValue int,Options nvarchar(max));
 INSERT @a SELECT questionId,[text],rating,COALESCE(optionIds,N'[]') FROM OPENJSON(@Answers) WITH(questionId int,[text] nvarchar(4000),rating int,optionIds nvarchar(max) AS JSON);
 IF EXISTS(SELECT 1 FROM @a a LEFT JOIN dbo.SurveyQuestions q ON q.QuestionID=a.QuestionID AND q.SurveyID=@SurveyID WHERE q.QuestionID IS NULL) THROW 51100,N'Question does not belong to survey.',1;
 IF EXISTS(SELECT 1 FROM dbo.SurveyQuestions q WHERE q.SurveyID=@SurveyID AND q.IsRequired=1 AND NOT EXISTS(SELECT 1 FROM @a a WHERE a.QuestionID=q.QuestionID)) THROW 51100,N'Required question is missing.',1;
 IF EXISTS(SELECT 1 FROM @a WHERE LEFT(LTRIM(Options),1)<>'[') THROW 51100,N'Option list must be an array.',1;
 DECLARE @o TABLE(QuestionID int,OptionID int,PRIMARY KEY(QuestionID,OptionID));
 IF EXISTS(SELECT 1 FROM @a a CROSS APPLY OPENJSON(a.Options) o WHERE o.type<>2 OR TRY_CONVERT(int,o.value) IS NULL) THROW 51100,N'Invalid option ID.',1;
 INSERT @o SELECT a.QuestionID,CONVERT(int,o.value) FROM @a a CROSS APPLY OPENJSON(a.Options) o;
 IF EXISTS(SELECT 1 FROM @o o LEFT JOIN dbo.SurveyQuestionOptions s ON s.OptionID=o.OptionID AND s.QuestionID=o.QuestionID WHERE s.OptionID IS NULL) THROW 51100,N'Option belongs to a different question.',1;
 IF EXISTS(SELECT 1 FROM @a a JOIN dbo.SurveyQuestions q ON q.QuestionID=a.QuestionID CROSS APPLY(SELECT COUNT(*) n FROM @o o WHERE o.QuestionID=a.QuestionID)x WHERE
 (q.Type='Text' AND (NULLIF(LTRIM(RTRIM(a.TextValue)),'') IS NULL OR a.RatingValue IS NOT NULL OR x.n<>0)) OR
 (q.Type='Rating' AND (a.RatingValue IS NULL OR a.RatingValue<q.RatingMin OR a.RatingValue>q.RatingMax OR a.TextValue IS NOT NULL OR x.n<>0)) OR
 (q.Type IN ('SingleChoice','MultipleChoice') AND (a.TextValue IS NOT NULL OR a.RatingValue IS NOT NULL OR x.n=0 OR (q.Type='SingleChoice' AND x.n<>1)))) THROW 51100,N'Answer type or rating bounds are invalid.',1;
 DECLARE @map TABLE(ResponseID int,QuestionID int);
 INSERT dbo.SurveyResponses(SurveyID,QuestionID,CustomerID,TextValue,RatingValue) OUTPUT inserted.ResponseID,inserted.QuestionID INTO @map SELECT @SurveyID,QuestionID,@CustomerID,TextValue,RatingValue FROM @a;
 INSERT dbo.SurveyAnswerOptions(ResponseID,OptionID,QuestionID) SELECT m.ResponseID,o.OptionID,o.QuestionID FROM @o o JOIN @map m ON m.QuestionID=o.QuestionID;
 UPDATE dbo.SurveyRecipients SET CompletedAt=SYSUTCDATETIME() WHERE SurveyID=@SurveyID AND CustomerID=@CustomerID;
 COMMIT;
 END TRY BEGIN CATCH IF @@TRANCOUNT>0 ROLLBACK; THROW; END CATCH;
END;
GO
CREATE OR ALTER PROCEDURE dbo.SaveAddress
 @ActorID int,@CustomerID int,@Label nvarchar(50),@RecipientName nvarchar(120),@Phone varchar(20),@AddressText nvarchar(500),@IsDefault bit=0,@AddressID int=NULL,@Note nvarchar(300)=NULL
AS
BEGIN
 SET NOCOUNT ON; SET XACT_ABORT ON;
 BEGIN TRY
 BEGIN TRAN;
 IF dbo.IsCustomer(@ActorID,@CustomerID)=0 THROW 51103,N'Cannot change this address book.',1;
 DECLARE @lock int;
 SELECT @lock=CustomerID FROM dbo.Customers WITH(UPDLOCK,HOLDLOCK) WHERE CustomerID=@CustomerID;
 IF NULLIF(@Phone,'') IS NULL OR NULLIF(@AddressText,'') IS NULL OR NULLIF(@RecipientName,'') IS NULL THROW 51100,N'Address details cannot be blank.',1;
 IF @AddressID IS NOT NULL AND NOT EXISTS(SELECT 1 FROM dbo.CustomerAddresses WHERE AddressID=@AddressID AND CustomerID=@CustomerID) THROW 51103,N'Address not owned by customer.',1;
 IF @IsDefault=1 UPDATE dbo.CustomerAddresses SET IsDefault=0 WHERE CustomerID=@CustomerID AND IsDefault=1;
 IF @AddressID IS NULL BEGIN
  INSERT dbo.CustomerAddresses(CustomerID,Label,RecipientName,Phone,AddressText,Note,IsDefault) VALUES(@CustomerID,@Label,@RecipientName,@Phone,@AddressText,@Note,@IsDefault);
  SET @AddressID=SCOPE_IDENTITY();
 END ELSE UPDATE dbo.CustomerAddresses SET Label=@Label,RecipientName=@RecipientName,Phone=@Phone,AddressText=@AddressText,Note=@Note,IsDefault=@IsDefault,IsActive=1 WHERE AddressID=@AddressID;
 COMMIT; SELECT @AddressID AS AddressID;
 END TRY BEGIN CATCH IF @@TRANCOUNT>0 ROLLBACK; THROW; END CATCH;
END;
GO
CREATE OR ALTER PROCEDURE dbo.SubmitFeedback
 @ActorID int,@CustomerID int,@Content nvarchar(4000),@Type varchar(15),@Rating tinyint=NULL,@OrderID int=NULL,@BranchID int=NULL,@ProductID int=NULL
AS
BEGIN
 SET NOCOUNT ON; SET XACT_ABORT ON;
 IF dbo.IsCustomer(@ActorID,@CustomerID)=0 THROW 51103,N'Cannot send feedback for another customer.',1;
 IF NULLIF(LTRIM(RTRIM(@Content)),'') IS NULL THROW 51100,N'Feedback cannot be blank.',1;
 IF @OrderID IS NOT NULL BEGIN
  DECLARE @actual int;
  SELECT @actual=BranchID FROM dbo.Orders WHERE OrderID=@OrderID AND CustomerID=@CustomerID;
  IF @actual IS NULL THROW 51103,N'Order does not belong to customer.',1;
  IF @BranchID IS NOT NULL AND @BranchID<>@actual THROW 51100,N'Feedback branch differs from order branch.',1;
  SET @BranchID=@actual;
  IF @ProductID IS NOT NULL AND NOT EXISTS(SELECT 1 FROM dbo.OrderDetails WHERE OrderID=@OrderID AND ProductID=@ProductID) THROW 51100,N'Product does not occur in this order.',1;
 END;
 INSERT dbo.Feedbacks(CustomerID,OrderID,BranchID,ProductID,Type,Content,Rating) VALUES(@CustomerID,@OrderID,@BranchID,@ProductID,@Type,@Content,@Rating);
 SELECT CONVERT(int,SCOPE_IDENTITY()) AS FeedbackID;
END;
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
-- Xóa mềm: thu hồi phân công cửa hàng; nếu xóa quản lý chính thì chuyển ManagerID cho quản lý khác còn hoạt động.
CREATE OR ALTER PROCEDURE dbo.SoftDeleteUser @ActorID int,@UserID int
AS
BEGIN
 SET NOCOUNT ON; SET XACT_ABORT ON;
 BEGIN TRAN;
 IF dbo.CanScope(@ActorID,'Chain',NULL,NULL,'Security.Manage')=0
    AND NOT (dbo.CanScope(@ActorID,'Chain',NULL,NULL,'Customers.Manage')=1
             AND EXISTS(SELECT 1 FROM dbo.Customers WHERE UserID=@UserID))
    THROW 51103,N'Không có quyền xóa tài khoản này.',1;
 IF @UserID=@ActorID THROW 51121,N'Không thể tự xóa tài khoản đang đăng nhập.',1;
 DECLARE @role varchar(30);
 SELECT @role=r.Code FROM dbo.Users u WITH(UPDLOCK,HOLDLOCK) JOIN dbo.Roles r ON r.RoleID=u.RoleID WHERE u.UserID=@UserID AND u.DeletedAt IS NULL;
 IF @role IS NULL THROW 51104,N'Account not found.',1;
 IF @role='Owner' AND NOT EXISTS(SELECT 1 FROM dbo.Users u JOIN dbo.Roles r ON r.RoleID=u.RoleID
    WHERE r.Code='Owner' AND u.UserID<>@UserID AND u.IsActive=1 AND u.DeletedAt IS NULL)
    THROW 51122,N'Phải còn ít nhất một chủ cửa hàng đang hoạt động.',1;
 DECLARE @email nvarchar(254)=CONCAT(N'deleted-',@UserID,N'@deleted.invalid');
 UPDATE dbo.Users SET IsActive=0,DeletedAt=SYSUTCDATETIME(),Email=@email,NormalizedEmail=UPPER(@email),
    FullName=CASE WHEN @role='Customer' THEN N'Khách đã xóa' ELSE N'Tài khoản đã xóa' END,Phone=NULL
 WHERE UserID=@UserID;
 UPDATE dbo.UserSessions SET RevokedAt=SYSUTCDATETIME() WHERE UserID=@UserID AND RevokedAt IS NULL;
 UPDATE dbo.StaffBranches SET IsActive=0 WHERE UserID=@UserID;
 IF @role='BranchManager'
  UPDATE b SET ManagerID=(SELECT TOP(1) s.UserID FROM dbo.StaffBranches s JOIN dbo.Users u ON u.UserID=s.UserID
       JOIN dbo.Roles r ON r.RoleID=u.RoleID
       WHERE s.BranchID=b.BranchID AND s.IsActive=1 AND r.Code='BranchManager' AND u.IsActive=1 AND u.UserID<>@UserID ORDER BY s.AssignedAt)
  FROM dbo.Branches b WHERE b.ManagerID=@UserID;
 INSERT dbo.AuditLogs(ActorID,Action,EntityType,EntityID,Details) VALUES(@ActorID,'Account.SoftDelete','Users',@UserID,@role);
 COMMIT;
END;
GO
