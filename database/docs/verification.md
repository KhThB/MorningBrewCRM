# Kết quả kiểm thử database Morning Brew CRM

Đã chạy 49/49 kiểm tra đạt trên SQL Server LocalDB thực tế. Database kiểm thử: `MorningBrewCRM_VerifyFinal`.

Database bàn giao `MorningBrewCRM` được tạo riêng với dữ liệu mẫu sạch sau khi kiểm thử đạt. Các phép thử race dùng hai kết nối SQL đồng thời; lỗi deadlock không được tính là kết quả nghiệp vụ đạt.

Đã render và kiểm tra trực quan toàn bộ 43 trang Word. Đối chiếu cuối xác nhận SQL có 41 bảng; từ điển Word có đủ 314 trường; ERD Draw.io có đủ 41 bảng. Database bàn giao có 24 stored procedure và 5 đơn mẫu; kiểm tra ràng buộc không phát hiện lỗi, số dư điểm của từng khách khớp sổ giao dịch điểm.

| STT | Kịch bản | Kết quả |
|---:|---|---|
| 1 | 41 tables and trusted foreign keys | Đạt |
| 2 | Pickup price, voucher and awarded points | Đạt |
| 3 | Idempotent completion and successful payment callback | Đạt |
| 4 | Guest POS has no customer or points | Đạt |
| 5 | Unpaid COD cannot complete | Đạt |
| 6 | Customer cannot acknowledge COD collection | Đạt |
| 7 | Paid delivery completes with merchandise-only points | Đạt |
| 8 | Deny other branch staff reading order | Đạt |
| 9 | Deny other area manager reading order | Đạt |
| 10 | Deny other customer reading order | Đạt |
| 11 | Allow area manager own area | Đạt |
| 12 | Deny price edit outside branch | Đạt |
| 13 | Reject missing required sugar/ice | Đạt |
| 14 | Reject topping on cake | Đạt |
| 15 | Reject unavailable branch option | Đạt |
| 16 | Reject zero quantity | Đạt |
| 17 | Reject another customer address | Đạt |
| 18 | Historical snapshots survive menu/address changes | Đạt |
| 19 | First idempotent order request | Đạt |
| 20 | Replay same order request | Đạt |
| 21 | Reject different payload under same key | Đạt |
| 22 | Gift voucher cannot be self-issued | Đạt |
| 23 | Branch manager cannot issue chain gift | Đạt |
| 24 | Concurrent orders cannot reserve same voucher | Đạt |
| 25 | Voucher has exactly one reservation | Đạt |
| 26 | Cancel releases reservation without changing expiry | Đạt |
| 27 | Concurrent redemptions cannot overspend points | Đạt |
| 28 | No overspending after concurrent redemption | Đạt |
| 29 | Prepare low-point voucher for reversal case | Đạt |
| 30 | Refund retries once and allows negative clawback balance | Đạt |
| 31 | Negative balance blocks paid redemption | Đạt |
| 32 | Customer cannot cancel after confirmation | Đạt |
| 33 | Expired unpaid order is cancelled by sweep | Đạt |
| 34 | Reject incomplete survey | Đạt |
| 35 | Reject invalid rating | Đạt |
| 36 | Reject option from outside question | Đạt |
| 37 | Atomic multi-choice survey submission | Đạt |
| 38 | Reject duplicate survey submission | Đạt |
| 39 | Published questions are frozen | Đạt |
| 40 | Published options are frozen | Đạt |
| 41 | Loyalty history is immutable | Đạt |
| 42 | Foreign keys reject incorrect variant/product | Đạt |
| 43 | Setup runtime role principal | Đạt |
| 44 | Runtime cannot read raw tables | Đạt |
| 45 | Runtime can invoke scoped procedures | Đạt |
| 46 | Concurrent default address updates leave one default | Đạt |
| 47 | Single default address invariant | Đạt |
| 48 | Financial and loyalty reconciliation | Đạt |
| 49 | Revenue is scoped and accounts for actual refunds | Đạt |

Kiểm thử bao phủ tầng database và stored procedure. Chưa có kiểm thử UI, HTTP authentication, tải lớn hoặc thanh toán thật vì các thành phần đó chưa được triển khai trong công việc này.

Chạy lại: tạo một database Verify/Test mới bằng `tools/Apply.ps1`, rồi gọi `tests/verify.py --database <tên>`. Không chạy bộ mutation test trên database bàn giao.
