# Kiểm chứng chuẩn hóa API v1

Ngày kiểm tra: 2026-10-05. .NET 10, SQL Server LocalDB, Windows Authentication.

- Build solution: thành công, 0 warning, 0 error.
- `database/tests/verify.py`: **49/49** kiểm tra đạt, trên `MorningBrewCRM_VerifyV1_20261005`.
- `tests/integration.py` + `tests/api_contract.py`: **19/19** nhóm đạt, trên `MorningBrewCRM_TestApiV1_20261005`.
- Các test thay đổi dữ liệu chỉ chạy trên database Test/Verify, không chạy trên `MorningBrewCRM`.

## Phạm vi đã kiểm tra

- Đơn hàng, giá snapshot, thanh toán, hoàn tiền, sổ điểm, voucher, cạnh tranh đồng thời và phân quyền SQL.
- Đăng nhập, thu hồi phiên, phân quyền chi nhánh/khu vực, khảo sát và phản hồi.
- Admin đăng nhập cookie, render các trang, CSRF và gửi thay đổi tới API/SQL.
- Lỗi JSON/validation/auth/routing đồng nhất; trường sai tên và bool bắt buộc.
- Phân trang số/khóa ghép, chèn mới giữa các trang, dùng token sai người/bộ lọc/tài nguyên, giới hạn kích thước.
- PATCH chỉ sửa trường gửi lên, cho phép null đúng trường, chặn sửa điểm/quyền, chặn ghi đè khi hai request cùng ETag.
- Owner sửa giá toàn chuỗi, quản lý chi nhánh bị từ chối, đơn mới bỏ qua số tiền override cũ, đơn cũ giữ snapshot.
- Swagger có v1, DTO snake_case, bearer authentication và phản hồi 201/lỗi.
- CORS chỉ cho origin cấu hình, xử lý preflight; rate limit trả 429 và Retry-After.

## Thay đổi kỳ vọng demo do giá chung

Giá cà phê M lấy BasePrice 30.000 thay vì giá riêng quán 1 là 32.000. Đơn demo mới gồm 2 ly và topping 5.000/ly có subtotal 70.000, voucher 10.000, thanh toán 60.000 và tích 6 điểm. Số liệu lịch sử trên database đã cài trước đây không được viết lại.

Chi tiết kết quả chạy nằm trong `database/qa/` (không commit log môi trường). Chạy lại theo [hướng dẫn API v1](api-v1.md). Kết quả này xác minh các chức năng hiện có; chưa bao gồm API đặt đơn, cổng thanh toán thật hoặc app mobile chưa được triển khai.

## Nâng cấp database đang dùng

Đã áp dụng migration giá chung cho `MorningBrewCRM`, sau khi kiểm chứng nâng cấp từ procedure cũ trên database Verify. Script xác nhận số bản ghi và tổng giá trị chi tiết đơn/sổ điểm không thay đổi. Bản sao procedure trước nâng cấp: `database/.local/shared-prices-before-20261005-162657/restore-procedures.sql` (chỉ lưu local, không commit).
