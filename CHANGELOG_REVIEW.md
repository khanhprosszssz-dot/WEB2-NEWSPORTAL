# Changelog rà soát News Portal ngày 12/10/2026

1. `NewsPortal.Api/Program.cs`: thêm GET `/health/ready`, kiểm tra kết nối bằng EF Core CanConnectAsync; 200 khi kết nối được, 503 khi không. Giữ `/health` làm liveness.
2. `NewsPortal.Api/Controllers/MediaController.cs`: thêm GET `/api/v1/media/{id}/download`, dùng quyền biên tập sẵn có, kiểm tên file GUID, xử lý thiếu metadata/file bằng 404, trả PhysicalFile attachment với tên gốc.
3. `NewsPortal.Api/Documentation/ResponseDocumentation.cs`: mô tả response download dạng binary trong Swagger.
4. `NewsPortal.Tests/ExtendedTests.cs`: kiểm Swagger binary và anonymous 401 của download; cleanup ưu tiên biến môi trường `ConnectionStrings__DefaultConnection` trước appsettings, phù hợp cấu hình DB thử.
5. README và REVIEW_STATUS.md: bổ sung tình trạng kiểm chứng hiện tại; phân biệt kết quả lịch sử với kết quả chạy lại.

Không thay đổi schema/migrations, seed, framework hoặc dependency; không chuyển nghiệp vụ sang quản lý sách. Chưa áp dụng Repository interface/Serilog vì cần kiểm thử hồi quy với DB và xác định mức độ bắt buộc của rubric.

## Đã kiểm chứng trên bản cuối

- Build: 0 warning, 0 error.
- Filtered xUnit: 2 Passed, 0 Failed, 0 Skipped; 19 test khác chưa chạy.
- 19 HTTP probe: 16 Pass, 3 Blocked vì SQL Server.
- Download: schema và bảo vệ anonymous đã Pass; stream file thực, quyền giữa các tài khoản và 404 với metadata thiếu còn cần SQL để test.
- Readiness: nhánh SQL unavailable trả 503 đã Pass; nhánh ready 200 chưa kiểm chứng.
- Cleanup override: biên dịch thành công; chưa thực hiện xóa dữ liệu test vì DB không khả dụng.

Chi tiết request/status và thời điểm nằm trong BANG_CHUNG_KIEM_TRA.zip. Không coi các thay đổi đã biên dịch thành công là đã được nghiệm thu đủ nghiệp vụ.
