# MorningBrewCRM

API CRM xây dựng trên ASP.NET Core và .NET 10.

## Tiến trình dự án

### Customers
- Tạo, lấy, cập nhật Customer profile.
- Tìm kiếm và lọc theo từ khóa, điểm Loyalty, ngày đăng ký và phân trang.
- Đã sửa ánh xạ `AppUser.Id` sang `Customer.Id` từ JWT.

### Loyalty
- Tích điểm, trừ điểm, điều chỉnh điểm và lịch sử điểm.
- Kiểm tra không cho số dư điểm âm.

### Promotions và CustomerPromotions
- Tạo, cập nhật Promotion.
- Phát voucher, lấy ví voucher và áp dụng voucher.
- Kiểm tra Customer tồn tại và ngăn tạo bản ghi trùng.

### Feedbacks
- Khách hàng gửi Feedback.
- Admin và quản lý xem, lọc, cập nhật trạng thái và phản hồi.
- `BranchId`, `ProductId` và `HandledBy` hỗ trợ nullable `int`.

### Surveys
- Tạo khảo sát và câu hỏi.
- Gửi khảo sát cho khách hàng.
- Lấy khảo sát đang mở và gửi câu trả lời.
- Thống kê số người nhận và hoàn thành.
- Kiểm tra câu hỏi thuộc đúng khảo sát, ngăn trả lời trùng.

### CRM Stats
- Thống kê khách hàng, Feedback, Survey và Loyalty points.
- Thống kê Feedback theo trạng thái.
- Hỗ trợ lọc theo `branchId`:

`GET /api/admin/stats/crm/overview?branchId=1`

## Thay đổi chung

- Các khóa chính và khóa ngoại CRM liên quan dùng `int` tự tăng.
- Đã loại bỏ `Guid.NewGuid()` và `Guid.TryParse()` khỏi API CRM.
- Route ID dùng constraint `:int`.
- Đã cấu hình các quan hệ chính trong `ApplicationDbContext`.
- Đã bổ sung xử lý các trường nullable như `BranchId`, `ProductId` và `HandledBy`.

## API chính

- `GET/PUT /api/Customer/me`
- `POST /api/Customer`
- `POST /api/Customer/search`
- `POST /api/Loyalty/earn/{customerId}`
- `POST /api/Loyalty/redeem/{customerId}`
- `POST /api/Loyalty/adjust/{customerId}`
- `GET /api/Loyalty/history/{customerId}`
- `POST/PUT /api/admin/AdminPromotions`
- `POST /api/admin/AdminPromotions/assign/{promotionId}`
- `GET /api/Promotions/public`
- `GET /api/Promotions/wallet/{customerId}`
- `POST /api/Feedbacks`
- `POST /api/admin/AdminFeedbacks/list`
- `PUT /api/admin/AdminFeedbacks/{id}/reply`
- `POST /api/admin/AdminSurveys`
- `POST /api/admin/AdminSurveys/send/{surveyId}`
- `GET /api/Surveys/active`
- `POST /api/Surveys/{id}/submit`
- `GET /api/admin/AdminSurveys/{id}/analytics`
- `GET /api/admin/stats/crm/overview`

## Kiểm tra

- Build toàn bộ solution: thành công.
- Chưa có project test tự động trong solution.
- Môi trường phát triển hiện sử dụng InMemory Database.

## Chạy dự án

```powershell
dotnet restore
dotnet build
dotnet run --project MorningBrew.Api/MorningBrew.Api.csproj
```

Swagger: `/swagger`
