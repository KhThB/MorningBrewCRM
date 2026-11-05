-- Dữ liệu giao dịch demo cho phạm vi CRM (1 cửa hàng, 30 khách).
-- Mọi ID được tra theo email / thứ tự hiển thị, không gán cứng.
SET NOCOUNT ON;
DECLARE @r TABLE(ID int);
DECLARE @voucher int,@order int,@payment int,@refund int,@feedback int,@key uniqueidentifier=NEWID();
DECLARE @owner int=(SELECT UserID FROM dbo.Users WHERE Email='owner@morningbrew.test'),
        @manager int=(SELECT UserID FROM dbo.Users WHERE Email='manager.1@morningbrew.test'),
        @staff int=(SELECT UserID FROM dbo.Users WHERE Email='staff.1@morningbrew.test'),
        @lanUser int=(SELECT UserID FROM dbo.Users WHERE Email='lan@morningbrew.test'),
        @minhUser int=(SELECT UserID FROM dbo.Users WHERE Email='minh@morningbrew.test');
DECLARE @lan int=(SELECT CustomerID FROM dbo.Customers WHERE UserID=@lanUser),
        @minh int=(SELECT CustomerID FROM dbo.Customers WHERE UserID=@minhUser),
        @branch int=(SELECT BranchID FROM dbo.Branches WHERE Code='MB01'),
        @minhAddress int;
SELECT @minhAddress=AddressID FROM dbo.CustomerAddresses WHERE CustomerID=@minh AND IsDefault=1;

-- ===== 1. Các luồng mẫu gốc (Lan, Minh, POS) =====
INSERT @r EXEC dbo.IssueVoucher @lanUser,@lan,1,@key;
SELECT @voucher=ID FROM @r; DELETE @r;
SET @key=NEWID();
INSERT @r EXEC dbo.CreateOrder @ActorID=@lanUser,@BranchID=@branch,@Source='App',@Fulfillment='Pickup',@PaymentMode='Prepaid',@Items=N'[{"variantId":1,"quantity":2,"optionIds":[1,3,5]}]',@RequestKey=@key,@CustomerID=@lan,@CustomerPromotionID=@voucher;
SELECT @order=ID FROM @r; DELETE @r;
SET @key=NEWID(); INSERT @r EXEC dbo.StartPayment @lanUser,@order,@key; SELECT @payment=ID FROM @r; DELETE @r;
EXEC dbo.SimulatePayment @lanUser,@payment,'Failed',N'Khách hủy lần thanh toán đầu';
SET @key=NEWID(); INSERT @r EXEC dbo.StartPayment @lanUser,@order,@key; SELECT @payment=ID FROM @r; DELETE @r;
EXEC dbo.SimulatePayment @lanUser,@payment,'Succeeded';
EXEC dbo.AdvanceOrder @staff,@order,'Confirmed'; EXEC dbo.AdvanceOrder @staff,@order,'Preparing'; EXEC dbo.AdvanceOrder @staff,@order,'Ready'; EXEC dbo.AdvanceOrder @staff,@order,'Completed';
INSERT @r EXEC dbo.SubmitFeedback @ActorID=@lanUser,@CustomerID=@lan,@Content=N'Đồ uống ngon và phục vụ nhanh',@Type='Service',@Rating=5,@OrderID=@order,@BranchID=@branch;
SELECT @feedback=ID FROM @r; DELETE @r;
EXEC dbo.HandleFeedback @manager,@feedback,'Resolved',N'Cảm ơn bạn đã ghé Morning Brew!';
SET @key=NEWID();
INSERT @r EXEC dbo.CreateOrder @ActorID=@minhUser,@BranchID=@branch,@Source='App',@Fulfillment='Delivery',@PaymentMode='COD',@Items=N'[{"variantId":3,"quantity":1,"optionIds":[1,3]}]',@RequestKey=@key,@CustomerID=@minh,@AddressID=@minhAddress;
SELECT @order=ID FROM @r; DELETE @r;
EXEC dbo.AdvanceOrder @staff,@order,'Confirmed'; EXEC dbo.AdvanceOrder @staff,@order,'Preparing'; EXEC dbo.AdvanceOrder @staff,@order,'Ready';
EXEC dbo.AdvanceOrder @ActorID=@staff,@OrderID=@order,@NextStatus='Delivering',@CourierName=N'Anh Bình',@CourierPhone='0908888888';
EXEC dbo.UpdateDelivery @staff,@order,'Failed',N'Khách chưa nghe điện thoại';
EXEC dbo.UpdateDelivery @staff,@order,'Delivering';
SET @key=NEWID();
INSERT @r EXEC dbo.CreateOrder @ActorID=@staff,@BranchID=@branch,@Source='POS',@Fulfillment='Pickup',@PaymentMode='COD',@Items=N'[{"variantId":4,"quantity":1}]',@RequestKey=@key;
SELECT @order=ID FROM @r; DELETE @r;
SET @key=NEWID(); INSERT @r EXEC dbo.StartPayment @staff,@order,@key; SELECT @payment=ID FROM @r; DELETE @r;
EXEC dbo.SimulatePayment @staff,@payment,'Succeeded';
EXEC dbo.AdvanceOrder @staff,@order,'Confirmed'; EXEC dbo.AdvanceOrder @staff,@order,'Preparing'; EXEC dbo.AdvanceOrder @staff,@order,'Ready'; EXEC dbo.AdvanceOrder @staff,@order,'Completed';
SET @key=NEWID();
INSERT @r EXEC dbo.CreateOrder @ActorID=@lanUser,@BranchID=@branch,@Source='App',@Fulfillment='Pickup',@PaymentMode='Prepaid',@Items=N'[{"variantId":4,"quantity":1}]',@RequestKey=@key,@CustomerID=@lan;
SELECT @order=ID FROM @r; DELETE @r;
EXEC dbo.CancelOrder @lanUser,@order,N'Khách đổi ý trước thanh toán';
SET @key=NEWID();
INSERT @r EXEC dbo.CreateOrder @ActorID=@minhUser,@BranchID=@branch,@Source='App',@Fulfillment='Delivery',@PaymentMode='Prepaid',@Items=N'[{"variantId":4,"quantity":1}]',@RequestKey=@key,@CustomerID=@minh,@AddressID=@minhAddress;
SELECT @order=ID FROM @r; DELETE @r;
SET @key=NEWID(); INSERT @r EXEC dbo.StartPayment @minhUser,@order,@key; SELECT @payment=ID FROM @r; DELETE @r;
EXEC dbo.SimulatePayment @minhUser,@payment,'Succeeded';
EXEC dbo.CancelOrder @minhUser,@order,N'Khách hủy trước xác nhận';
SELECT @refund=RefundID FROM dbo.Refunds WHERE OrderID=@order;
EXEC dbo.SimulateRefund @manager,@refund,'Failed',N'Lỗi mô phỏng lần đầu';
EXEC dbo.SimulateRefund @manager,@refund,'Succeeded';

-- ===== 2. Khảo sát: phát hành và mời toàn bộ 30 khách =====
DECLARE @s1 int=(SELECT SurveyID FROM dbo.Surveys WHERE Title=N'Khẩu vị và trải nghiệm'),
        @s2 int=(SELECT SurveyID FROM dbo.Surveys WHERE Title=N'Thăm dò món mới mùa đông');
DECLARE @s1q1 int=(SELECT QuestionID FROM dbo.SurveyQuestions WHERE SurveyID=@s1 AND DisplayOrder=1),
        @s1q2 int=(SELECT QuestionID FROM dbo.SurveyQuestions WHERE SurveyID=@s1 AND DisplayOrder=2),
        @s1q3 int=(SELECT QuestionID FROM dbo.SurveyQuestions WHERE SurveyID=@s1 AND DisplayOrder=3),
        @s2q1 int=(SELECT QuestionID FROM dbo.SurveyQuestions WHERE SurveyID=@s2 AND DisplayOrder=1),
        @s2q2 int=(SELECT QuestionID FROM dbo.SurveyQuestions WHERE SurveyID=@s2 AND DisplayOrder=2),
        @s2q3 int=(SELECT QuestionID FROM dbo.SurveyQuestions WHERE SurveyID=@s2 AND DisplayOrder=3),
        @s2q4 int=(SELECT QuestionID FROM dbo.SurveyQuestions WHERE SurveyID=@s2 AND DisplayOrder=4);
DECLARE @o1a int=(SELECT OptionID FROM dbo.SurveyQuestionOptions WHERE QuestionID=@s1q1 AND DisplayOrder=1),
        @o1b int=(SELECT OptionID FROM dbo.SurveyQuestionOptions WHERE QuestionID=@s1q1 AND DisplayOrder=2),
        @o1c int=(SELECT OptionID FROM dbo.SurveyQuestionOptions WHERE QuestionID=@s1q1 AND DisplayOrder=3),
        @newCacao int=(SELECT OptionID FROM dbo.SurveyQuestionOptions WHERE QuestionID=@s2q1 AND DisplayOrder=1),
        @newGinger int=(SELECT OptionID FROM dbo.SurveyQuestionOptions WHERE QuestionID=@s2q1 AND DisplayOrder=2),
        @newLatte int=(SELECT OptionID FROM dbo.SurveyQuestionOptions WHERE QuestionID=@s2q1 AND DisplayOrder=3),
        @newMatcha int=(SELECT OptionID FROM dbo.SurveyQuestionOptions WHERE QuestionID=@s2q1 AND DisplayOrder=4),
        @tMorning int=(SELECT OptionID FROM dbo.SurveyQuestionOptions WHERE QuestionID=@s2q3 AND DisplayOrder=1),
        @tNoon int=(SELECT OptionID FROM dbo.SurveyQuestionOptions WHERE QuestionID=@s2q3 AND DisplayOrder=2),
        @tAfternoon int=(SELECT OptionID FROM dbo.SurveyQuestionOptions WHERE QuestionID=@s2q3 AND DisplayOrder=3),
        @tEvening int=(SELECT OptionID FROM dbo.SurveyQuestionOptions WHERE QuestionID=@s2q3 AND DisplayOrder=4);
EXEC dbo.PublishSurvey @owner,@s1;
EXEC dbo.PublishSurvey @owner,@s2;
DECLARE @c int,@u int;
DECLARE invites CURSOR LOCAL FAST_FORWARD FOR SELECT CustomerID FROM dbo.Customers ORDER BY CustomerID;
OPEN invites; FETCH NEXT FROM invites INTO @c;
WHILE @@FETCH_STATUS=0
BEGIN
 EXEC dbo.InviteSurvey @owner,@s1,@c;
 EXEC dbo.InviteSurvey @owner,@s2,@c;
 FETCH NEXT FROM invites INTO @c;
END;
CLOSE invites; DEALLOCATE invites;
-- Lan đã trả lời khảo sát 1; khảo sát 2 để trống cho phần demo trên app.
DECLARE @answers nvarchar(max)=CONCAT(N'[{"questionId":',@s1q1,N',"optionIds":[',@o1a,N',',@o1b,N']},{"questionId":',@s1q2,N',"rating":5},{"questionId":',@s1q3,N',"text":"Thêm món theo mùa"}]');
EXEC dbo.SubmitSurvey @lanUser,@s1,@lan,@answers;

-- ===== 3. 28 khách còn lại: đơn hoàn tất, phản hồi, trả lời khảo sát =====
-- Phân bố theo CustomerID để dữ liệu cố định giữa các lần cài, không dùng số ngẫu nhiên.
DECLARE @items nvarchar(200),@variant int,@product int,@qty int,@rating int,@type varchar(15),@content nvarchar(400),@opts nvarchar(100),@text nvarchar(300);
DECLARE people CURSOR LOCAL FAST_FORWARD FOR SELECT CustomerID,UserID FROM dbo.Customers WHERE CustomerID NOT IN (@lan,@minh) ORDER BY CustomerID;
OPEN people; FETCH NEXT FROM people INTO @c,@u;
WHILE @@FETCH_STATUS=0
BEGIN
 SET @order=NULL;
 -- 4/5 số khách có 1 đơn hoàn tất (món không cần chọn topping/đường/đá).
 IF @c % 5 <> 0
 BEGIN
  SET @variant=CHOOSE(@c % 6 + 1, 4, 5, 6, 7, 8, 10);
  SET @qty=1 + @c % 2;
  SELECT @product=ProductID FROM dbo.ProductVariants WHERE VariantID=@variant;
  SET @key=NEWID();
  SET @items=CONCAT(N'[{"variantId":',@variant,N',"quantity":',@qty,N'}]');
  INSERT @r EXEC dbo.CreateOrder @ActorID=@u,@BranchID=@branch,@Source='App',@Fulfillment='Pickup',@PaymentMode='Prepaid',
       @Items=@items,@RequestKey=@key,@CustomerID=@c;
  SELECT @order=ID FROM @r; DELETE @r;
  SET @key=NEWID(); INSERT @r EXEC dbo.StartPayment @u,@order,@key; SELECT @payment=ID FROM @r; DELETE @r;
  EXEC dbo.SimulatePayment @u,@payment,'Succeeded';
  EXEC dbo.AdvanceOrder @staff,@order,'Confirmed'; EXEC dbo.AdvanceOrder @staff,@order,'Preparing';
  EXEC dbo.AdvanceOrder @staff,@order,'Ready'; EXEC dbo.AdvanceOrder @staff,@order,'Completed';
 END;
 -- Phản hồi: gắn với món trong đơn; đủ 3 trạng thái Mới / Đang xử lý / Đã giải quyết.
 IF @order IS NOT NULL AND @c % 3 <> 0
 BEGIN
  SET @rating=CHOOSE(@c % 4 + 1, 5, 4, 3, 2);
  SET @content=CHOOSE(@c % 6 + 1, N'Món rất ngon, sẽ quay lại', N'Hơi ngọt so với khẩu vị của tôi', N'Đồ uống ra hơi chậm giờ cao điểm',
       N'Giá hợp lý, chất lượng ổn định', N'Ly nhỏ hơn mong đợi', N'Bánh mới, thơm, rất đáng thử');
  INSERT @r EXEC dbo.SubmitFeedback @ActorID=@u,@CustomerID=@c,@Content=@content,@Type='Product',@Rating=@rating,@OrderID=@order,@BranchID=@branch,@ProductID=@product;
  SELECT @feedback=ID FROM @r; DELETE @r;
  IF @c % 4 = 0 EXEC dbo.HandleFeedback @manager,@feedback,'Resolved',N'Cảm ơn bạn! Quán đã ghi nhận và điều chỉnh.';
  ELSE IF @c % 4 = 1 EXEC dbo.HandleFeedback @manager,@feedback,'Processing',N'Quán đang kiểm tra lại công thức.';
 END
 ELSE IF @order IS NULL AND @c % 2 = 1
 BEGIN
  SET @type=CASE WHEN @c % 4 = 1 THEN 'Space' ELSE 'Service' END;
  INSERT @r EXEC dbo.SubmitFeedback @ActorID=@u,@CustomerID=@c,@Content=N'Không gian thoáng nhưng thiếu ổ cắm điện',@Type=@type,@Rating=4,@BranchID=@branch;
  DELETE @r;
 END;
 -- Khảo sát 1: khoảng 3/4 số khách trả lời.
 IF @c % 4 <> 3
 BEGIN
  SET @opts=CASE @c % 3 WHEN 0 THEN CONCAT(@o1a,N'') WHEN 1 THEN CONCAT(@o1a,N',',@o1c) ELSE CONCAT(@o1b,N',',@o1c) END;
  SET @text=CASE WHEN @c % 5 = 1 THEN N'Mong có thêm món ít đường' ELSE NULL END;
  SET @answers=CONCAT(N'[{"questionId":',@s1q1,N',"optionIds":[',@opts,N']},{"questionId":',@s1q2,N',"rating":',3 + @c % 3,N'}',
       CASE WHEN @text IS NULL THEN N'' ELSE CONCAT(N',{"questionId":',@s1q3,N',"text":"',@text,N'"}') END,N']');
  EXEC dbo.SubmitSurvey @u,@s1,@c,@answers;
 END;
 -- Khảo sát 2 (thăm dò món mới): khoảng 2/3 số khách trả lời, Cacao muối dẫn đầu.
 IF @c % 3 <> 1
 BEGIN
  SET @opts=CONCAT(CASE WHEN @c % 7 IN (0,1,2) THEN @newCacao WHEN @c % 7 IN (3,4) THEN @newLatte WHEN @c % 7 = 5 THEN @newGinger ELSE @newMatcha END,N'');
  SET @text=CASE WHEN @c % 6 = 0 THEN N'Nên có size nhỏ cho món mới' ELSE NULL END;
  SET @answers=CONCAT(N'[{"questionId":',@s2q1,N',"optionIds":[',@opts,N']},{"questionId":',@s2q2,N',"rating":',2 + @c % 4,N'},',
       N'{"questionId":',@s2q3,N',"optionIds":[',CASE @c % 3 WHEN 0 THEN CONCAT(@tMorning,N'') WHEN 1 THEN CONCAT(@tMorning,N',',@tAfternoon) ELSE CONCAT(@tNoon,N',',@tEvening) END,N']}',
       CASE WHEN @text IS NULL THEN N'' ELSE CONCAT(N',{"questionId":',@s2q4,N',"text":"',@text,N'"}') END,N']');
  EXEC dbo.SubmitSurvey @u,@s2,@c,@answers;
 END;
 FETCH NEXT FROM people INTO @c,@u;
END;
CLOSE people; DEALLOCATE people;
SELECT 'Demo ready' AS Result,
 (SELECT COUNT(*) FROM dbo.Customers) AS Customers,
 (SELECT COUNT(*) FROM dbo.Orders WHERE Status='Completed') AS CompletedOrders,
 (SELECT COUNT(*) FROM dbo.Feedbacks) AS Feedbacks,
 (SELECT COUNT(*) FROM dbo.SurveyRecipients WHERE CompletedAt IS NOT NULL) AS SurveyAnswers;
GO
