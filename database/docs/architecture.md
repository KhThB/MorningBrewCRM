# Hệ thống quản lý chuỗi quán cà phê kết hợp CRM

## Giới thiệu và mục tiêu đồ án

Morning Brew Coffee được giả định là chuỗi quán cà phê tại Thành phố Hồ Chí Minh, phục vụ đồ uống, bánh và món ăn nhẹ. Đối tượng khách hàng chủ yếu là sinh viên, nhân viên văn phòng và người trẻ. Hệ thống tập trung hóa dữ liệu của các chi nhánh, hỗ trợ bán hàng cơ bản và lấy quản lý quan hệ khách hàng làm trọng tâm.

Thiết kế này phát triển từ tài liệu Task_2_3_He_thong_quan_ly_chuoi_quan_ca_phe_CRM.docx và erd.drawio. Hai tệp nguồn có 21 bảng thực tế. Bản cập nhật có 41 bảng, gồm 21 bảng được điều chỉnh và 20 bảng mới để đáp ứng app đặt hàng, thực đơn có tùy chọn, thanh toán, hoàn tiền và kiểm soát dữ liệu. Số lượng này đã tính các bảng trung gian; không cộng thêm bảng chỉ vì có đường nối trên ERD.

Mục tiêu là phục vụ ba giao diện: web Admin cho cấp quản lý, web POS cho nhân viên và app khách hàng. Các giao diện gọi backend ASP.NET Core; backend là thành phần duy nhất giữ thông tin kết nối SQL Server. Phần bàn giao này xây dựng cơ sở dữ liệu, thao tác nghiệp vụ, dữ liệu mẫu và tài liệu; giao diện và các endpoint HTTP được triển khai ở giai đoạn tiếp theo.

## Tổ chức và phạm vi chức năng

Chủ doanh nghiệp quản lý toàn chuỗi. Quản lý khu vực phụ trách một hoặc nhiều khu vực. Quản lý chi nhánh phụ trách quán được giao. Nhân viên sử dụng POS tại chi nhánh đang được phân công. Khách hàng sử dụng app để giao dịch và chăm sóc tài khoản cá nhân. Một tài khoản có một vai trò theo yêu cầu gốc; một người cần hai vai trò sử dụng tài khoản riêng trong phiên bản này.

Admin quản lý tổ chức, tài khoản, phân quyền, thực đơn, giá chi nhánh, đơn hàng, khách hàng, điểm, voucher, phản hồi và khảo sát. Quản lý cấp dưới chỉ được thao tác trong phạm vi của mình. Danh mục và món dùng chung toàn chuỗi do chủ doanh nghiệp quản lý; giá và trạng thái bán được điều chỉnh theo chi nhánh.

POS hiển thị thực đơn của quán, cho nhân viên tra cứu thành viên bằng số điện thoại, chọn món và tùy chọn, tạo đơn, ghi nhận thu tiền và xử lý đơn từ app. POS cũng cập nhật pha chế, bàn giao và giao hàng. Nhân viên được xác nhận tiền đã thu, nhưng quyền hủy sau xác nhận và hoàn tiền thuộc cấp quản lý. Khách vãng lai có thể mua tại POS mà không cần tạo hồ sơ khách hàng.

App cho khách đăng ký và đăng nhập bằng email/mật khẩu, quản lý hồ sơ và nhiều địa chỉ, xem menu theo quán, lưu món yêu thích, đặt nhận tại quán hoặc giao hàng, theo dõi đơn và lịch sử mua. Khách có thể xem điểm, đổi điểm lấy voucher, gửi phản hồi, trả lời khảo sát và đọc thông báo trong app.

Phiên bản đầu không quản lý tồn kho nguyên liệu, công thức pha chế, kế toán chuyên sâu, ứng dụng tài xế, định tuyến giao hàng hoặc dự báo bằng AI. Thanh toán được mô phỏng trong database để kiểm thử đầy đủ trạng thái; chưa xử lý tiền thật hoặc tích hợp SMS, email và push notification.

## Lựa chọn công nghệ và quy ước

Database dùng SQL Server 2022 trở lên, compatibility level 160, ngôn ngữ T-SQL. Các bảng thuộc schema dbo. Backend hiện tại của dự án dùng .NET 10; có thể kết nối bằng Microsoft.Data.SqlClient hoặc EF Core SQL Server. Bộ script SQL là nguồn chuẩn triển khai database; tránh tạo một bộ EF migration độc lập làm thay đổi schema ngoài tài liệu.

Tên bảng và cột dùng tiếng Anh; nội dung nghiệp vụ tiếng Việt dùng NVARCHAR. Khóa định danh đơn dùng INT IDENTITY, khóa liên kết thuần dùng khóa ghép. Mã nghiệp vụ như OrderCode và mã voucher có UNIQUE để tra cứu nhưng không thay thế kiểm tra quyền sở hữu.

Tiền được lưu bằng DECIMAL(18,2), đơn vị VND; không dùng FLOAT. Phần trăm được áp dụng trên tiền hàng và làm tròn xuống đồng. DATETIME2(3) lưu thời điểm UTC. Giờ mở và đóng cửa là TIME theo giờ Việt Nam UTC+7; trong bản đầu quán không mở xuyên nửa đêm. Khoảng báo cáo dùng đầu bao gồm, cuối không bao gồm để không trùng dữ liệu giữa hai kỳ.

ROWVERSION là dãy byte tự đổi mỗi lần cập nhật, không phải thời gian. Backend dùng giá trị này khi cập nhật hồ sơ/cấu hình theo mẫu WHERE ID = @id AND Version = @version; không cập nhật được dòng nào nghĩa là dữ liệu đã đổi và cần tải lại. Các thao tác tài chính dùng transaction và khóa hàng thay cho việc tin vào số dư do app gửi.

## Tài khoản và phân quyền

Users lưu email, tên, số điện thoại và hash mật khẩu; Customers chỉ lưu phần CRM. NormalizedEmail phải viết hoa và bỏ khoảng trắng ở hai đầu. Email là duy nhất; điện thoại không duy nhất để không loại trừ trường hợp dùng chung số. POS tra cứu số điện thoại có thể nhận nhiều kết quả và phải chọn đúng khách.

Mật khẩu mẫu dùng định dạng ASP.NET PasswordHasher V3, PBKDF2 SHA512. Mật khẩu thô không được lưu trong bảng. UserSessions lưu SHA256 của refresh token ngẫu nhiên, hạn phiên và thời điểm thu hồi. Luồng xác thực HTTP, khóa tài khoản, cấp token và kiểm tra token còn hiệu lực được thực hiện trong backend khi xây dựng app.

Roles xác định vai trò; Permissions và RolePermissions xác định hành động. Quyền thao tác không tự cho phép đọc mọi chi nhánh. CanBranch, CanScope và IsCustomer kết hợp tài khoản hoạt động, quyền thao tác, phân công và quyền sở hữu để bảo vệ các stored procedure.

MorningBrewRuntime là database role của backend tin cậy, bị cấm đọc/ghi bảng trực tiếp và được gọi stored procedure. Tham số ActorID luôn lấy từ danh tính đã xác thực ở server. Không nhận ActorID do app tự khai và không cấp kết nối SQL cho người dùng cuối, vì database không tự xác thực một số nguyên ActorID.

Chức năng CRUD cấu hình chưa có procedure riêng phải đi qua dịch vụ quản trị tin cậy với kiểm tra tương tự. Không dùng tài khoản db_owner cho các request công khai. Bộ database không thay thế việc cài đặt authorization ở API; các thử nghiệm database kiểm tra quy tắc phạm vi bên trong các procedure đã bàn giao.

## Thực đơn có size và tùy chọn

Products lưu mô tả món; ProductVariants lưu size và giá cơ sở. Món không có size có phiên bản Standard. BranchProducts quyết định quán có kinh doanh món hay không. BranchProductVariants tùy chọn ghi đè giá và tạm hết từng size. Nếu chưa có giá riêng, hệ thống dùng giá cơ sở.

ModifierGroups chia tùy chọn thành đường, đá và topping. ModifierOptions lưu các lựa chọn và phụ phí. ProductModifierGroups xác định nhóm nào áp dụng cho món và số lựa chọn tối thiểu/tối đa. BranchModifierOptions ghi đè phụ phí hoặc đánh dấu tạm hết ở một chi nhánh. Không có bản ghi ghi đè nghĩa là dùng cấu hình dùng chung.

Mỗi lựa chọn topping xuất hiện tối đa một lần trên một ly. Nếu hai ly có tùy chọn khác nhau thì tạo hai dòng OrderDetails. Quantity biểu thị số ly giống nhau hoàn toàn. Đường và đá chọn tối đa một giá trị mỗi nhóm; MinSelections bằng 1 để bắt buộc khách chọn rõ. Phiên bản đầu chưa cho chọn hai phần của cùng topping trên một ly.

GetMenu trả menu và tùy chọn hợp lệ; CreateOrder kiểm tra lại tại server trước khi tính tiền. App không gửi giá để server tin theo. Đơn có tối đa 100 dòng và mỗi dòng tối đa 100 sản phẩm nhằm giới hạn đầu vào của bản đồ án.

## Quy trình đơn hàng

Một đơn luôn thuộc một chi nhánh. Source phân biệt App/POS; Fulfillment phân biệt Pickup/Delivery. Đơn bán ngay tại quầy dùng Pickup. PaymentMode phân biệt Prepaid/COD; với Pickup, COD nghĩa là thanh toán khi đến quầy.

CreateOrder nhận JSON danh sách phiên bản, số lượng và mã tùy chọn. RequestKey là UUID do client tạo một lần cho một ý định đặt hàng. RequestHash được tính từ toàn bộ payload đầu vào; gửi lại cùng khóa và cùng payload trả về đơn cũ, nhưng gửi lại cùng khóa với nội dung khác bị từ chối. Khi retry phải giữ nguyên payload JSON, kể cả cách biểu diễn chuỗi.

Đơn trả trước bắt đầu ở AwaitingPayment, có 15 phút để thanh toán. Sau khi nhận kết quả thành công, đơn chuyển sang Pending. Đơn COD bắt đầu ở Pending. POS xử lý Pending → Confirmed → Preparing → Ready. Đơn pickup chuyển từ Ready sang Completed khi giao cho khách và đã thu đủ tiền. Đơn delivery chuyển từ Ready sang Delivering rồi Completed.

OrderDetails chụp tên món, tên size và giá một ly. OrderItemModifiers chụp tên nhóm, tên tùy chọn và phụ phí. Vì vậy, sửa menu không làm hóa đơn cũ thay đổi. Một dòng có LineTotal = Quantity × (UnitPrice + ModifierTotal). Orders.Subtotal là tổng các dòng; TotalAmount = Subtotal − DiscountAmount + DeliveryFee.

Số tiền tổng được lưu cùng đơn để giữ hóa đơn và truy vấn báo cáo thuận tiện. Đây là dữ liệu dư có chủ đích; chỉ CreateOrder tính và ghi cùng transaction. Kiểm tra đối chiếu trong bộ test bảo đảm tổng các dòng và phụ phí khớp các trường đã lưu. Người dùng runtime không được ghi thẳng vào các bảng để phá vỡ quy tắc này.

Đơn giao hàng có một Deliveries lưu tên người nhận, điện thoại, địa chỉ và chỉ dẫn chốt tại thời điểm đặt. CustomerAddresses chỉ là sổ địa chỉ có thể sửa; không dùng FK để đọc địa chỉ mới cho đơn cũ. App có thể chọn AddressID thuộc chính khách; POS có thể nhập địa chỉ nhận trực tiếp.

Người giao được ghi bằng tên và điện thoại. Khi giao thất bại, Deliveries.Status thành Failed còn đơn vẫn ở Delivering để quản lý biết đơn chưa kết thúc. Nhân viên có thể cho giao lại hoặc quản lý hủy đơn. AttemptCount đếm số lần bắt đầu giao. Mỗi lần cập nhật được lưu AuditLogs; OrderStatusHistory lưu các chuyển trạng thái vận hành của đơn.

## Thanh toán và hoàn tiền

PaymentAttempts lưu một bản ghi cho mỗi lần thử. Một đơn được có tối đa một lần Pending và tối đa một lần Succeeded bằng unique filtered index. Sau một lần Failed, client tạo RequestKey mới để thử lại. Gửi lặp kết quả Succeeded không ghi thêm tiền hoặc điểm; không được đổi kết quả đã kết thúc sang trạng thái khác.

StartPayment và SimulatePayment cung cấp mô phỏng Pending, Failed, Succeeded. Với COD, chỉ nhân viên có Payments.Manage ở đúng chi nhánh mới được xác nhận đã thu tiền. Đơn chỉ hoàn tất khi có một giao dịch thành công. Amount của giao dịch được lấy từ đơn, không lấy từ client.

Khách tự hủy khi đơn còn AwaitingPayment hoặc Pending. Sau xác nhận cần Orders.Cancel, mặc định chỉ cấp quản lý. Hủy phải có lý do. Khi hủy, voucher đang giữ được giải phóng; giao dịch Pending chuyển Failed; nếu đã thu tiền thì tạo Refunds ở trạng thái Pending.

Refunds là một yêu cầu hoàn toàn bộ một giao dịch. PaymentID duy nhất ngăn tạo hai yêu cầu hoàn cùng tiền. Thử lại hoàn tiền cập nhật cùng bản ghi và tăng AttemptCount. SimulateRefund cho phép thử thất bại rồi thành công; gửi thành công lặp không thu hồi điểm lần nữa.

Đơn đã Completed giữ trạng thái vận hành Completed ngay cả khi hoàn tiền về sau; Refunds phản ánh nghiệp vụ tài chính. Đơn đang xử lý phải được hủy trước khi yêu cầu hoàn tiền. Chưa hỗ trợ trả một món hoặc hoàn một phần; tính năng đó cần bổ sung cấu trúc phân bổ chi tiết.

ExpireUnpaidOrders được thiết kế để backend gọi mỗi phút, hủy đơn hết hạn và giải phóng voucher. Database không tự chạy lịch và LocalDB không có SQL Agent cho quy trình này. Khi tích hợp app, cần cấu hình background service thực hiện job; bộ test gọi job trực tiếp.

Khi bổ sung cổng thanh toán thật, backend phải xác minh chữ ký callback, mã đơn, số tiền và kết quả đối soát trước khi ghi nhận. Cần mở rộng danh sách Method hiện chỉ có Simulator/Cash, thay thao tác mô phỏng bằng adapter nhà cung cấp, và xử lý callback đến muộn sau hủy bằng hoàn tiền/đối soát. Provider và ProviderReference đã có để lưu định danh ngoài. SimulatePayment không nhận tiền thật nên cố ý từ chối thu sau khi hết hạn.

## Điểm và voucher

LoyaltyPolicies không cho thời gian hiệu lực chồng nhau. Tỷ lệ tại lúc tạo đơn được chụp vào Orders.AmountPerPoint. Chính sách mẫu là 10.000 đồng tiền hàng sau giảm = 1 điểm, làm tròn xuống; phí giao không được tính điểm. Chỉ cộng khi đơn đã hoàn tất và thanh toán đủ.

LoyaltyTransactions là sổ điểm có dấu. Earn cộng điểm, Redeem trừ khi đổi voucher, Reverse thu hồi do hoàn tiền, Adjust điều chỉnh có ghi chú. Không sửa/xóa lịch sử điểm; điều chỉnh phải ghi bút toán mới. Customers.LoyaltyPoints là số dư phục vụ đọc nhanh và phải khớp tổng sổ điểm.

Promotions lưu chương trình; CustomerPromotions lưu từng voucher đã cấp, gồm điểm đã trả, hạn và điều kiện giảm tại lúc cấp. PointsCost = NULL nghĩa là quà tặng do quản lý cấp, 0 nghĩa là khách tự nhận miễn phí, lớn hơn 0 nghĩa là đổi điểm. TotalLimit và PerCustomerLimit đếm toàn bộ lần cấp, kể cả voucher đã dùng; hủy đơn không làm phát sinh quyền nhận mới.

Voucher chỉ có phạm vi toàn chuỗi, một khu vực hoặc một chi nhánh. Phạm vi chương trình không được sửa sau lần cấp đầu; điều kiện giảm và thời hạn đã được chụp vào voucher. Tắt chương trình dừng cấp mới, còn voucher đã cấp vẫn được dùng theo điều kiện cũ cho đến hết hạn.

Một đơn dùng tối đa một voucher. Đơn tối thiểu tính trên tiền hàng trước giảm, không gồm phí giao. Voucher giảm Amount hoặc Percent, có trần giảm; giảm không vượt tiền hàng. Available → Reserved khi tạo đơn, Reserved → Used khi hoàn tất. Hủy đơn trả voucher về Available và giữ nguyên hạn; nếu hạn đã qua thì voucher vẫn không dùng được.

Hoàn toàn bộ đơn đã hoàn tất sẽ thu hồi điểm từng cộng và trả voucher về Available nếu voucher còn đang gắn với chính đơn đó. Điểm có thể âm nếu khách đã tiêu điểm trước khi hoàn tiền. Số dư âm ngăn đổi voucher tốn điểm; hệ thống không xóa giao dịch cũ để làm số dư đẹp hơn.

IssueVoucher khóa chương trình và khách khi kiểm tra hạn mức/số dư. CreateOrder khóa voucher khi giữ; AdvanceOrder và SimulateRefund cập nhật điểm cùng transaction với sổ điểm. Backend phải retry lỗi deadlock 1205 hoặc bận 51109 theo giới hạn và giữ nguyên khóa yêu cầu; không retry vô hạn lỗi nghiệp vụ 51100 hoặc thiếu quyền 51103.

## CRM phản hồi khảo sát và thông báo

CustomerPreferences biểu thị món khách chọn yêu thích. SegmentCustomers lọc theo tuổi tính từ ngày sinh, giới tính, món yêu thích, điểm, mức chi và thời gian không quay lại. Phân nhóm trong bản đầu là kết quả truy vấn, chưa có bảng định nghĩa phân nhóm lưu sẵn. Chi tiêu và lần mua gần nhất được tính trong phạm vi quản lý; thông tin hồ sơ CRM và điểm là thông tin dùng chung của khách.

Quản lý khu vực/chi nhánh chỉ phân nhóm khách từng có đơn Completed trong phạm vi. Chủ chuỗi có thể xem cả khách chưa mua. Khi cấp ưu đãi hoặc gửi khảo sát, CustomerPromotions và SurveyRecipients ghi nhận người nhận thực tế nên kết quả không thay đổi khi khách tăng tuổi hoặc thay sở thích.

Feedbacks lưu phản hồi chung hoặc gắn với quán, món, đơn. Nếu gắn đơn, đơn phải thuộc khách và chi nhánh phải khớp; nếu gắn cả món thì món phải nằm trong đơn. Quản lý chi nhánh xử lý phản hồi của quán, quản lý khu vực xử lý trong vùng, chủ chuỗi xử lý cả phản hồi chung chưa gắn chi nhánh. Bản đầu có một câu trả lời chính thức, chưa phải hệ thống chat hỗ trợ.

Khảo sát có Draft/Open/Closed, một phạm vi và khoảng mở. Câu hỏi hỗ trợ SingleChoice, MultipleChoice, Rating, Text. Mỗi câu chọn đáp án cần ít nhất hai lựa chọn khi công bố. Sau khi công bố, câu hỏi, đáp án, phạm vi và thời hạn không được sửa; muốn đổi nội dung phải tạo khảo sát mới để giữ ý nghĩa thống kê.

Khách chỉ gửi khảo sát đang mở, còn thời hạn và đã được mời. SubmitSurvey nhận cả bộ câu trả lời trong một transaction, kiểm tra câu bắt buộc, loại dữ liệu, khoảng điểm và đáp án thuộc đúng câu. SurveyResponses có duy nhất một dòng theo câu hỏi/khách; SurveyAnswerOptions chứa từng đáp án được chọn. Gửi lần hai bị từ chối, không thêm kết quả trùng.

Notifications lưu thông báo cá nhân phát sinh từ đơn, voucher, phản hồi và khảo sát. ReadAt đánh dấu đã đọc. Chưa gửi qua hạ tầng bên ngoài. AuditLogs lưu hành động và người thực hiện, không chứa password hash, refresh token hoặc thông tin bí mật thanh toán. Nhật ký trong bản đầu được các procedure nghiệp vụ ghi; CRUD cấu hình của backend cũng phải ghi nhật ký khi được triển khai.

## Báo cáo và đối chiếu

RevenueReport ghi nhận doanh thu tại thời điểm hoàn tất và trừ hoàn tiền theo thời điểm hoàn thành hoàn tiền. Hoàn tiền cho đơn đã thu nhưng bị hủy trước hoàn tất không được trừ vào doanh thu đã ghi nhận, vì đơn đó chưa tạo doanh thu. Chỉ tiêu trong bản đầu bao gồm phí giao, không phải lợi nhuận và chưa phân tích thuế/giá vốn.

LoyaltyReconciliation so sánh số dư cache với tổng sổ điểm. Bộ kiểm thử còn đối chiếu Subtotal với tổng OrderDetails và ModifierTotal với các tùy chọn. Chênh lệch phải được điều tra, không tự sửa lịch sử. Báo cáo có thể được mở rộng từ các bảng nhưng luôn phải lọc phạm vi và tránh JOIN làm nhân số tiền lên theo số món hoặc số lần thử thanh toán.

## Triển khai và giới hạn tích hợp

Các script 01 đến 07 chạy theo thứ tự được ghi trong README. Apply.ps1 chỉ tạo database mới, dừng nếu tên đã tồn tại; không DROP hoặc ghi đè dữ liệu. Database mới có lỗi cài đặt cần được kiểm tra trước khi thử tiếp. Không tự động chạy lại seed trên database đã có dữ liệu.

Dữ liệu mẫu có hai khu vực, ba chi nhánh, đủ năm vai trò, hai khách hàng, menu nhiều size và tùy chọn, các kiểu đơn, voucher, phản hồi và khảo sát. Quán mẫu mở 00:00–23:59:59 để chạy bài tập gần như mọi thời điểm; khi triển khai cần đặt giờ thực tế. Phí giao mẫu tùy quán, không phụ thuộc bản đồ.

Database bàn giao không tự tạo các màn hình Admin/POS/app, không triển khai HTTP authentication và không cài cổng thanh toán thật. Nó cung cấp cấu trúc, dữ liệu và quy trình đã được thực thi trên SQL Server để làm nền cho các phần đó. Tệp README có cách chạy demo, tài khoản, hợp đồng JSON và các bước tích hợp tiếp theo.

## Cơ sở tham khảo kỹ thuật

Tài liệu nghiệp vụ gốc và erd.drawio do người thực hiện đồ án cung cấp là cơ sở chức năng. Phần mở rộng theo các quyết định đã thống nhất về pickup, giao hàng, tùy chọn món, đổi voucher và thanh toán thử nghiệm.

Tài liệu Microsoft về transaction: https://learn.microsoft.com/en-us/sql/relational-databases/sql-server-transaction-locking-and-row-versioning-guide

Tài liệu Microsoft về filtered index: https://learn.microsoft.com/en-us/sql/relational-databases/indexes/create-filtered-indexes

Tài liệu EF Core SQL Server: https://learn.microsoft.com/en-us/ef/core/providers/sql-server/
