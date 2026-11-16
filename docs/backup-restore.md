# Phương án sao lưu và phục hồi CSDL (yêu cầu II.1)

> Cập nhật 09/11/2026 [UI2-W3a]: toàn bộ sao lưu, phục hồi và sao lưu tự động làm trên **web Admin** (API), không còn dùng `Backup.ps1` / `Restore.ps1`.

## 1. Mục tiêu
- **RPO** (dữ liệu tối đa có thể mất): **24 giờ** nhờ sao lưu tự động hằng ngày; giảm thêm bằng sao lưu thủ công trước thao tác lớn.
- **RTO** (thời gian khôi phục): **dưới 5 phút** với dữ liệu demo – chỉ cần chọn file và bấm Phục hồi.
- Mọi bản sao lưu đều **được kiểm tra** (CHECKSUM + `RESTORE VERIFYONLY`); phục hồi luôn tạo **bản an toàn** trước khi ghi đè.

## 2. Thành phần
| Thành phần | Vai trò |
|---|---|
| Admin web → **Sao lưu & phục hồi** (chỉ chủ cửa hàng) | Nút **Sao lưu database**; danh sách file `.bak` (loại, dung lượng, thời gian); nút **Phục hồi** trên từng file |
| API `POST v1/admin/backups` | Sao lưu FULL, `COPY_ONLY`, `CHECKSUM` ra file `.bak`, kiểm tra lại bằng `RESTORE VERIFYONLY`, ghi `AuditLogs` (`Database.Backup`) |
| API `POST v1/admin/backups:restore` | Kiểm tra file → sao lưu bản *Trước khi phục hồi* → kết nối `master`: `SINGLE_USER WITH ROLLBACK IMMEDIATE` → `RESTORE DATABASE … WITH REPLACE, CHECKSUM` → `MULTI_USER`; ghi `AuditLogs` (`Database.Restore`) |
| Dịch vụ nền `AutoBackupService` (trong API) | Mỗi giờ kiểm tra: nếu bản tự động mới nhất đã quá 24 giờ thì sao lưu; chỉ giữ **14** bản tự động mới nhất (bản thủ công không bị xóa) |
| Mã nguồn | `MorningBrew.Api/Infrastructure/DatabaseBackup.cs`, `Controllers/AdminBackup.cs`, `MorningBrew.Admin/Pages/Admin/Backup.cshtml` |

Thư mục lưu mặc định: `database/.local/backups` (không đưa lên Git). Cấu hình trong `appsettings.json` của API:

```json
"Backup": { "Directory": "D:\\MorningBrewBackups", "Keep": 14, "Auto": true }
```

Tên file cho biết loại bản sao lưu:
| Loại | Ví dụ tên file |
|---|---|
| Thủ công | `MorningBrewCRM_Crm_20261109_213000.bak` |
| Tự động | `MorningBrewCRM_Crm_auto_20261109_090102.bak` |
| Trước khi phục hồi | `MorningBrewCRM_Crm_truoc-phuc-hoi_20261109_213512.bak` |

## 3. Lịch sao lưu
| Loại | Tần suất | Thực hiện | Lưu giữ |
|---|---|---|---|
| FULL tự động | Mỗi 24 giờ khi API chạy | `AutoBackupService` | 14 bản gần nhất |
| FULL thủ công | Trước khi nâng cấp / chạy script mới / trước demo | Nút **Sao lưu database** | Theo nhu cầu |
| Bản an toàn | Mỗi lần phục hồi | Tự động trước khi ghi đè | Theo nhu cầu |
| Bản sao ngoài máy | Hằng tuần | Chép thư mục backups sang ổ ngoài / cloud drive | 4 tuần |

Khi triển khai thật (SQL Server bản Standard, nhiều giao dịch): chuyển recovery model sang FULL, dùng SQL Server Agent sao lưu **differential** mỗi 6 giờ và **transaction log** mỗi 15–30 phút để giảm RPO xuống vài phút.

## 4. Quy trình phục hồi
1. Đăng nhập Admin bằng tài khoản chủ cửa hàng → **Sao lưu & phục hồi**.
2. Chọn file `.bak` mới nhất **trước** thời điểm sự cố → bấm **Phục hồi**, tích ô xác nhận → **Phục hồi**.
3. Hệ thống: kiểm tra file → tạo bản *Trước khi phục hồi* → ghi đè database → bật lại chế độ nhiều người dùng.
4. Đăng nhập lại (phiên đăng nhập cũng quay về thời điểm sao lưu), kiểm tra Tổng quan, danh sách khách, Nhật ký hệ thống (có dòng `Database.Restore`).
5. Nếu chọn nhầm file: phục hồi lại từ bản *Trước khi phục hồi* vừa tạo.

Lưu ý: dữ liệu phát sinh **sau** thời điểm của file sao lưu sẽ mất. App khách / POS đang mở có thể bị đăng xuất.

## 5. Kiểm tra định kỳ
- Mỗi lần sao lưu đã tự chạy `RESTORE VERIFYONLY`.
- Mỗi tuần: sao lưu thủ công → tạo một đơn thử → phục hồi bản vừa sao lưu → đơn thử biến mất, số khách/phản hồi khớp với trước → ghi biên bản.
