# MorningBrewApp — App khách hàng (.NET MAUI Blazor Hybrid)

## Chạy app

1. Chạy backend: trong solution `MorningBrewCRM-integration-crm-sqlserver`, chạy **MorningBrew.Api** bằng profile **http** (`http://localhost:5029`).
2. Mở `MorningBrewApp/MauiApp1/MauiApp1.slnx` bằng Visual Studio 2026 (đã cài workload **.NET MAUI**).
3. Chọn target:
   - **Windows Machine**: dễ demo nhất, không cần emulator. Cửa sổ mở sẵn ở kích thước điện thoại 430×900.
   - **Android Emulator**: app tự gọi `http://10.0.2.2:5029`.
4. Đăng nhập bằng tài khoản seed: `lan@morningbrew.test` / `MorningBrew!2026`.

Đổi địa chỉ API ở `Services/ApiClient.cs` → `ApiSettings.BaseUrl`.

## Màn hình

| Tab | Màn hình | API |
|---|---|---|
| — | Đăng nhập, Đăng ký | `/v1/auth/login`, `/v1/auth/register` (*cần bổ sung*) |
| Trang chủ | Thẻ thành viên, banner khảo sát mới, đơn gần đây, ưu đãi, gợi ý món | profile, promotions, surveys |
| Thực đơn | Chọn cửa hàng, lọc danh mục, tìm món, ♡ yêu thích, chọn size + số lượng | `/v1/me/menu` (*cần bổ sung*), preferences |
| (Giỏ hàng) | Đổi số lượng, chọn voucher, đặt hàng (thanh toán mô phỏng) | wallet, `/v1/me/orders` (*cần bổ sung*) |
| Đơn hàng | Danh sách, chi tiết, tiến trình, **Đánh giá đơn** | orders → feedbacks |
| Ưu đãi | Đổi điểm, voucher của tôi, lịch sử điểm | promotions, redeem, wallet, loyalty |
| Tài khoản | Hồ sơ (ETag), Yêu thích, Phản hồi, **Khảo sát (tab có badge)**, Đăng xuất | profile, preferences, feedbacks, surveys |

Khi backend chưa có endpoint (trả 404), app dùng **dữ liệu mẫu** cho thực đơn và đơn hàng, và hiện nhãn "Dữ liệu mẫu". Đơn mẫu tự chuyển trạng thái mỗi 6 giây cho đến **Hoàn tất** để demo được nút đánh giá. Xem `API_CAN_BO_SUNG.md`.

## Cấu trúc code

```
MauiApp1/
├─ Services/          ApiClient (Bearer, lỗi tiếng Việt), ApiJson (snake_case), AuthService,
│                     CrmService (/v1/me/...), ShopService (thực đơn, giỏ, đơn + dữ liệu mẫu), Ui (định dạng, toast)
├─ Components/
│  ├─ Layout/         MainLayout (tab bar 5 mục + thanh giỏ hàng), BlankLayout (đăng nhập/đăng ký)
│  ├─ Shared/         Icon, MemberCard, ProductThumb, StarRating, PageHeader, StateView, Toast
│  └─ Pages/          Home, MenuPage, Cart, Orders, OrderDetail, Rewards, Account, ProfileEdit,
│                     Favorites, Feedbacks, FeedbackNew, Surveys, SurveyAnswer, Login, Register
└─ wwwroot/app.css    Bảng màu espresso #1c1917 + caramel #d6a76c, nền kem #f6f3ee
```

`Counter.razor`, `Weather.razor`, `NavMenu.razor` là file mẫu của template, không còn dùng, có thể xóa.
