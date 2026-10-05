# NewsPortal — Nhịp Sống

Đồ án ASP.NET Core 10 Web API + MVC, EF Core và SQL Server. Giao diện tiếng Việt gồm Reader và CMS. Nội dung demo không phải tin tức thực tế.

## Chạy local

Yêu cầu .NET SDK 10 và SQL Server Express instance `SQLEXPRESS` (Windows Authentication). Đổi connection string development nếu dùng instance khác.

```powershell
dotnet restore NewsPortal.sln
powershell -NoProfile -ExecutionPolicy Bypass -File scripts/Initialize-Development.ps1
powershell -NoProfile -ExecutionPolicy Bypass -File scripts/Start-Development.ps1
```

- Website: http://localhost:5200
- CMS: http://localhost:5200/cms
- Swagger: http://localhost:5100/swagger
- RSS: http://localhost:5200/rss

Tài khoản: [TEST_ACCOUNTS.md](TEST_ACCOUNTS.md). Mật khẩu development trong `.local/development.json`, trường `Password`. Không đưa file này lên Git. Script initialize chạy migration và seed 5 vai trò, 10 chuyên mục, 15 tag, 35 bài. Script không reset mật khẩu tài khoản đã tồn tại.

Dừng demo bằng `powershell -NoProfile -ExecutionPolicy Bypass -File scripts/Stop-Development.ps1`. Log trong `.local/`. Script chỉ dừng ứng dụng có đường dẫn executable được xác minh thuộc bản source này.

## Kiến trúc

Browser Fetch → MVC `/gateway` → HttpClient → REST API `/api/v1` → services → EF Core → SQL Server.

Web không tham chiếu API project hoặc DbContext. JWT được giữ trong authentication cookie đã mã hóa, HttpOnly; JavaScript không lưu JWT vào localStorage. Các mutation qua gateway yêu cầu antiforgery token. API kiểm tra vai trò, chủ sở hữu, IsActive và SecurityStamp trên mỗi token.

Bài viết là văn bản thuần, được escape khi render. Ảnh được kiểm tra extension/MIME/chữ ký file và giới hạn 5 MB. Không hỗ trợ rich HTML. RowVersion ngăn ghi đè phiên bản khi sửa bài. Cache công khai dùng IMemoryCache + ETag revalidation và được vô hiệu hóa sau mutation.

## Kiểm thử

Khởi chạy hai ứng dụng trước, sau đó:

```powershell
dotnet test NewsPortal.sln --no-restore
```

Tests gọi HTTP thật vào database development; tạo dữ liệu kiểm thử rồi dọn chúng, có phát sinh audit logs. Không dùng database production cho bộ test này. Ngày 30/09/2026: cả 21 tests pass, gồm hồi quy upload multipart qua gateway và kiểm tra response/security trong Swagger.

Kiểm tra Reader bằng Chrome headless khi hai ứng dụng đang chạy:

```powershell
powershell -NoProfile -ExecutionPolicy Bypass -File scripts/Test-ReaderBrowser.ps1
powershell -NoProfile -ExecutionPolicy Bypass -File scripts/Test-ReaderBrowser.ps1 -Full
```

Script kiểm tra trang chủ, tìm kiếm, đăng nhập và cộng đồng ở viewport 1440 và 390 px; lưu ảnh và report.json trong `.local/browser-qa`. Thêm `-Full` để chạy đăng ký/đăng nhập, upload file, viết/sửa/duyệt bài, comment/reaction/bookmark, users/taxonomy/settings/newsletter/polls và toàn bộ mục CMS desktop/mobile. Báo cáo từng luồng ở workflow-report.json; có kiểm tra HTML được escape, lỗi phân trang, Tab và JavaScript exception. Chỉ chạy một bộ test tại một thời điểm vì tài khoản demo dùng chung.

Nếu `dotnet` chưa có trong PATH nhưng SDK đã cài ở vị trí mặc định, chạy `$env:PATH = 'C:\Program Files\dotnet;' + $env:PATH` trong PowerShell trước các lệnh trên.

## Nghiệm thu cài mới

[Báo cáo nghiệm thu local](ACCEPTANCE_REPORT.md) ghi lại kết quả cài mới từ source, database và thư mục NuGet riêng: 21 tests, 8 smoke cases, 26 workflow checkpoints và kiểm tra CORS/mất kết nối/phục hồi đều pass.

```powershell
powershell -NoProfile -ExecutionPolicy Bypass -File scripts/Verify-CleanInstall.ps1
```

Script giữ lại snapshot và database nghiệm thu trong `.local/acceptance`, khôi phục demo hiện tại khi xong. Đây là cài mới ứng dụng trên máy đã có SDK/SQL Server/Chrome, không phải cài lại hệ điều hành. Xem báo cáo để chạy thêm `Test-ServiceRecovery.ps1` trên snapshot.

## Cộng đồng bạn đọc

Trang `/cong-dong` hỗ trợ đăng ký/hủy bản tin và bình chọn khảo sát. Administrator quản lý danh sách đăng ký, tạo/đóng/mở/xóa khảo sát trong CMS. Migration `CommunityFeatures` tạo các bảng liên quan. Chưa gửi email tự động. `/sitemap.xml` liệt kê bài công khai qua API.

## Publish

```powershell
dotnet publish NewsPortal.Api/NewsPortal.Api.csproj -c Release -o artifacts/api
dotnet publish NewsPortal.Web/NewsPortal.Web.csproj -c Release -o artifacts/web
```

Kiểm chứng bản publish trên máy này:

```powershell
powershell -NoProfile -ExecutionPolicy Bypass -File scripts/Verify-Published.ps1
```

Script publish hai ứng dụng, dừng development, chạy trực tiếp executable trong `artifacts`, chạy tests HTTP và browser, rồi khôi phục development. Dùng SQL Server và cấu hình Development local. Có thể tự chạy bản publish bằng `Start-Development.ps1 -Published` sau khi dừng ứng dụng hiện tại. Các script tự tìm SDK x64 nếu PATH chưa cập nhật.

Hosting Windows: cài ASP.NET Core Hosting Bundle phù hợp, tạo hai IIS sites và cấp quyền database cho application identity. Thiết lập environment variables: `ASPNETCORE_ENVIRONMENT=Production`, `ConnectionStrings__DefaultConnection`, `Jwt__Key` (ngẫu nhiên ít nhất 32 bytes), `Cors__Origins__0`, `AllowedHosts`; Web cần `Api__BaseUrl` là URL HTTPS của API. Gán domain thực tế thay `localhost` trong production config. Cấp quyền ghi thư mục uploads cho API, cấu hình HTTPS và lưu Data Protection keys bền vững cho Web. Backup database và uploads.

Chạy API với `--initialize` trong môi trường Production để áp dụng migration bằng tài khoản triển khai có quyền DDL; không seed tài khoản demo ở Production. Sau đó chạy API bằng tài khoản hạn chế quyền. Chưa triển khai hosting công khai; cần cấu hình môi trường đích và kiểm tra HTTPS/database/upload sau deployment.

Xem [API_DOCUMENTATION.md](API_DOCUMENTATION.md), [DATABASE_DESIGN.md](DATABASE_DESIGN.md), [DEMO_SCRIPT.md](DEMO_SCRIPT.md), [FEATURE_CHECKLIST.md](FEATURE_CHECKLIST.md), [đối chiếu từng mục TXT](REQUIREMENTS_TRACEABILITY.md).
