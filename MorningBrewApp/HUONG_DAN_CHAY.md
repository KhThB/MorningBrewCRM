# MorningBrewApp — App khách hàng (.NET MAUI Blazor Hybrid)

## Chạy app

1. Tạo database bằng `database/sql_ssms/morningbrew_fullscript.sql` (xem `database/README.md`).
2. Chạy **MorningBrew.Api** bằng profile **http** (`http://localhost:5029`).
3. Mở `MorningBrewCRM.slnx` ở thư mục gốc bằng Visual Studio (đã cài workload **.NET MAUI**), chọn project `MorningBrewwApp`.
4. Chọn target:
   - **Windows Machine**: dễ demo nhất, cửa sổ mở sẵn kích thước điện thoại 430×900.
   - **Android Emulator**: app tự gọi `http://10.0.2.2:5029`.
5. Đăng nhập: `lan@morningbrew.test` / `MorningBrew!2026`.

Đổi địa chỉ API ở `Services/ApiClient.cs` → `ApiSettings.BaseUrl`. Khi không kết nối được API, các trang báo lỗi kèm nút **Thử lại**.

## Màn hình

| Tab | Màn hình | API |
|---|---|---|
| — | Đăng nhập, Đăng ký (bắt buộc số điện thoại để tích điểm tại quầy) | `/v1/auth/login`, `/v1/auth/register` |
| Trang chủ | Thẻ thành viên, chọn nhanh Nhận tại quán / Giao hàng, khảo sát mới, đơn gần đây, ưu đãi, gợi ý món (bấm mở thẳng món) | profile, promotions, surveys, menu |
| Thực đơn | Lần đầu mở → trang **Hình thức nhận hàng** (`/order-type`); lọc danh mục, tìm món, ♡ yêu thích, chọn size + Đá/Nóng | `/v1/me/menu`, preferences |
| (Giỏ hàng) | Đổi số lượng, voucher (tính đúng mức tối đa / đơn tối thiểu), địa chỉ đã lưu, thanh toán tiền mặt / chuyển khoản mô phỏng | wallet, `/v1/me/addresses`, `/v1/me/orders` |
| Đơn hàng | Danh sách (tự cập nhật 15 giây), chi tiết, tiến trình, hủy đơn (có xác nhận), đánh giá đơn | orders, feedbacks |
| Ưu đãi | Đổi điểm (có xác nhận), voucher của tôi, lịch sử điểm | promotions, redeem, wallet, loyalty |
| Tài khoản | Hồ sơ (ETag), Yêu thích, Phản hồi, Khảo sát, Thông báo, Đổi mật khẩu, Đăng xuất | profile, preferences, feedbacks, surveys, notifications |

## Cấu trúc code

```
MauiApp1/
├─ Services/          ApiClient (Bearer, lỗi tiếng Việt), ApiJson (snake_case), AuthService, TokenStore,
│                     CrmService (/v1/me/...), ShopService (thực đơn, giỏ, đơn), Models, Ui (định dạng, toast)
├─ Components/
│  ├─ Layout/         MainLayout (tab bar 5 mục + thanh giỏ hàng), BlankLayout (đăng nhập/đăng ký)
│  ├─ Shared/         Icon, MemberCard, ProductThumb, StarRating, PageHeader, StateView, Toast, ConfirmSheet
│  └─ Pages/          Home, OrderType, MenuPage, Cart, Orders, OrderDetail, Rewards, Account, ProfileEdit,
│                     Favorites, Feedbacks, FeedbackNew, Surveys, SurveyAnswer, Notifications, ChangePassword,
│                     Login, Register, NotFound
└─ wwwroot/app.css    Bảng màu espresso #1c1917 + caramel #d6a76c, nền kem #f6f3ee
```
