# Phương án sao lưu và phục hồi CSDL (yêu cầu III.1)

## 1. Mục tiêu
- **RPO** (dữ liệu tối đa có thể mất): 24 giờ với lịch sao lưu hằng ngày; có thể giảm bằng sao lưu thủ công trước thao tác lớn.
- **RTO** (thời gian khôi phục): dưới 15 phút với dữ liệu demo (vài MB).
- Bản sao lưu phải **kiểm tra được** (CHECKSUM + RESTORE VERIFYONLY) và **phục hồi thử** được sang database khác mà không ảnh hưởng hệ thống đang chạy.

## 2. Công cụ
| Thành phần | Mô tả |
|---|---|
| `database/tools/Backup.ps1` | Sao lưu FULL có CHECKSUM, kiểm tra VERIFYONLY, giữ N bản mới nhất (mặc định 14) |
| `database/tools/Restore.ps1` | Kiểm tra file, đọc tên file logic, phục hồi sang database mới (mặc định) hoặc ghi đè (`-Replace`) |
| Admin web → **Sao lưu & phục hồi** | Chủ cửa hàng bấm "Sao lưu database" (FULL, COPY_ONLY, có kiểm tra); xem danh sách bản sao lưu; mỗi lần sao lưu ghi `AuditLogs` (`Database.Backup`) |

Thư mục mặc định: `database/.local/backups` (không đưa lên Git). Cấu hình khác bằng `Backup:Directory` trong appsettings của API.

## 3. Lịch sao lưu đề xuất
| Loại | Tần suất | Thực hiện | Lưu giữ |
|---|---|---|---|
| FULL | Hằng ngày 23:00 | Task Scheduler chạy `Backup.ps1` | 14 bản gần nhất |
| FULL thủ công (COPY_ONLY) | Trước khi nâng cấp/migrate, trước demo | Nút trên Admin hoặc `Backup.ps1` | Theo nhu cầu |
| Bản sao ngoài máy | Hằng tuần | Chép thư mục backups sang ổ ngoài / cloud drive | 4 tuần |

Khi triển khai thật (SQL Server bản Standard, nhiều giao dịch): chuyển recovery model sang FULL, thêm sao lưu **differential** mỗi 6 giờ và **transaction log** mỗi 15–30 phút để giảm RPO xuống vài phút.

Lệnh tạo lịch:
```powershell
schtasks /Create /SC DAILY /ST 23:00 /TN "MorningBrew Backup" /TR "powershell -File D:\repoclone\T5Project\MorningBrewCRM\database\tools\Backup.ps1 -Database MorningBrewCRM_Crm"
```

## 4. Quy trình phục hồi
1. Chọn file `.bak` (mới nhất trước sự cố).
2. **Phục hồi thử** sang database mới và kiểm tra số liệu:
   `./database/tools/Restore.ps1 -BackupFile <file.bak> -Database MorningBrewCRM_Restore`
3. Nếu đúng: **dừng API**, phục hồi đè database chính:
   `./database/tools/Restore.ps1 -BackupFile <file.bak> -Database MorningBrewCRM_Crm -Replace`
4. Khởi động API, đăng nhập Admin kiểm tra (tổng quan, danh sách khách, nhật ký).
5. Ghi lại sự cố, file đã dùng, thời điểm vào biên bản.

Nếu bước 3 lỗi giữa chừng, database có thể còn ở chế độ SINGLE_USER: chạy `ALTER DATABASE [MorningBrewCRM_Crm] SET MULTI_USER;` rồi phục hồi lại.

## 5. Kiểm tra định kỳ
Mỗi tuần phục hồi thử bản mới nhất sang `MorningBrewCRM_Restore`, so sánh số dòng `Users`, `Customers`, `Feedbacks`, `SurveyResponses` với database chính, rồi xóa database thử.
