# Kế hoạch NewsPortal

Theo PROWEBSITE-V2.txt: ASP.NET Core 10 Web API + MVC, SQL Server Express,
EF Core migrations, Identity/JWT; Web chỉ giao tiếp API qua HTTP.

## Giai đoạn
1. Foundation: giữ hai project, solution, cấu hình, Swagger.
2. Database: entities, quan hệ, indexes, migration, seed tiếng Việt.
3. REST CRUD: categories, tags, articles.
4. Query: DTO, IQueryable, paging, sorting, filtering, search.
5. Validation và ProblemDetails.
6. ILogger và audit.
7. Cache public và invalidation.
8. Identity/JWT và tài khoản development.
9. RBAC, quyền sở hữu tài nguyên.
10. Quy trình biên tập và revision.
11. Comment/reply, reaction, bookmark, history.
12. Reader MVC gọi API qua HttpClient/Fetch.
13. CMS, dashboard, users, media, settings.
14. OpenAPI và tài liệu endpoints.
15. Lịch xuất bản, notification, SEO/RSS khi core ổn định.
16. xUnit và kiểm thử HTTP thực tế, security review.
17. Production configuration.
18. Publish và final verification.

Sau mỗi giai đoạn: restore/build; khi có tests: test. Không chuyển giai đoạn nếu build lỗi.
Không triển khai lên hosting công khai khi chưa có môi trường đích.

## Môi trường
- SDK 10.0.401; runtime 10.0.12; target net10.0.
- SQL Server SQLEXPRESS đang chạy; sqlcmd có sẵn.
- Hai project có API và Reader nền tảng; đã bổ sung CMS, media/admin API, scheduled worker, RSS và tests ngày 29/09/2026.
- Git CLI không có trong PATH.

## Kết quả triển khai 29/09/2026

- Restore/build sạch với SDK 10.0.401; migration/seed trên SQL Server Express thành công.
- Hoàn thiện CMS còn thiếu, admin/media/settings API và scheduled publishing.
- Tests phát hiện và sửa lỗi response cache max-age=0; dùng ETag với no-cache revalidation.
- Thêm README, API_DOCUMENTATION, DEMO_SCRIPT, scripts start/stop và production config.
- 8 tests tích hợp HTTP + SQL Server pass; publish Release hai ứng dụng thành công.
- Chưa deploy hosting công khai vì chưa có môi trường đích. Visual QA chưa thực hiện do không có browser kết nối; xem FEATURE_CHECKLIST.

## Tiếp tục ngày 30/09/2026

- Xác nhận SDK 10.0.401 x64 và SQL Server Express hoạt động; sandbox không thấy SDK, chạy các lệnh kiểm tra ngoài sandbox bằng đường dẫn cài đặt.
- Restore/build 0 warnings/errors; initialize migration/seed thành công; khởi chạy Reader/API.
- Cả 13 tests tích hợp pass, gồm đăng ký/hồ sơ/thu hồi token, cookie/CSRF, newsletter/polls, reply/notifications và sitemap.
- Thêm script Chrome headless kiểm tra 4 trang public ở 1440/390 px; 8 trường hợp pass, lưu ảnh và báo cáo trong .local/browser-qa.
- Sửa phân trang để báo lỗi callback và tránh gửi yêu cầu lặp khi đang tải.
- Cập nhật tài liệu các module cộng đồng đã có trong mã nguồn. Còn kiểm tra thao tác CMS, keyboard và deployment trên môi trường đích.

## Chốt bản demo local ngày 30/09/2026

- Người dùng chọn hoàn tất bản chạy/demo trên máy này trước; không yêu cầu triển khai hosting công khai trong đợt này.
- Bổ sung nhóm tin chuyên mục ở home, tiêu điểm category, CMS user detail, Swagger response schema/status/XML và log authorization bị từ chối.
- Chrome workflow phát hiện lỗi upload qua MVC gateway: form value provider đã đọc Request.Body. Sửa bằng chuyển tiếp form đã parse qua MultipartFormDataContent; thêm test hồi quy HTTP gateway.
- Build 0 warnings/errors; 15 tests pass. Browser 8 smoke cases và 26 workflow checkpoints pass, gồm XSS text, upload/editorial/interactions, taxonomy/users/settings/community và responsive 10 mục CMS.
- Verify-Published.ps1 đã publish, chạy artifacts/api + artifacts/web local, test HTTP và workflow thành công rồi phục hồi development. Các kiểm tra UI bổ sung sau đó cũng pass trên development; JS đã đối chiếu hash khớp bản publish.
- Thêm script tìm SDK khi PATH chưa cập nhật, readiness check khởi chạy và hướng dẫn tái kiểm tra.
- Bổ sung REQUIREMENTS_TRACEABILITY.md; cập nhật checklist, API/database/demo docs. Final read-only check: 2 migrations, 10 categories, 15 tags, 35 articles, 5 roles; không còn tài khoản QA; 3 endpoint kiểm tra trả 200.

## Rà soát bổ sung theo yêu cầu chắc chắn hơn

- Viết tests mới trước khi sửa: phát hiện cache key trùng giữa các lần restart và API chấp nhận ảnh đúng định dạng đường dẫn nhưng chưa tồn tại.
- Thêm namespace cache cho mỗi process; kiểm tra Media trước khi lưu thumbnail; validation độ dài title/content sau trim.
- Bổ sung tests query thực tế và ownership/visibility/RSS/sitemap; restore/build 0 warnings/errors, 19/19 tests pass.
- Cập nhật bản publish và chạy Verify-Published.ps1 để kiểm chứng lại các thay đổi trên artifact local.
- Kết quả artifact mới: 19/19 tests (18 HTTP/SQL Server, 1 unit cache), 8/8 browser smoke checks và 26/26 workflow checkpoints pass; script đã khôi phục hai ứng dụng development.

## Nghiệm thu cài mới

- Thêm Verify-CleanInstall.ps1: copy source không chứa bin/obj/uploads/secret, restore bằng thư mục NuGet mới, migration/seed database riêng, publish và kiểm thử, sau đó phục hồi demo.
- Lần đầu script phát hiện lỗi đặt thuộc tính SqlConnectionStringBuilder trong PowerShell; sửa sang khóa Initial Catalog. Lần thành công: 67220437046f4d7eba52b5af8cb9a02d.
- Bổ sung tests JWT hết hạn/sai issuer/audience/chữ ký và optimistic concurrency với hai PUT đồng thời.
- Snapshot sạch: build 0 warnings/errors; seed hai lần giữ đúng 10 categories/15 tags/35 articles/5 roles/users; 21/21 tests, 8/8 smoke cases, 26/26 workflow checkpoints pass.
- Test-ServiceRecovery.ps1 dùng snapshot này trên 5300/5400: CORS allow/deny, Reader báo lỗi khi API dừng và phục hồi sau restart đều pass. Sửa việc chờ Chrome child process ghi hoàn tất DOM trên Windows trong script kiểm thử.
- Lưu TRX, JSON, ảnh và manifest SHA-256; tạo ACCEPTANCE_REPORT.md với phạm vi, tiêu chí và link chứng cứ cụ thể. Database demo hiện tại được giữ nguyên; database nghiệm thu riêng được giữ để kiểm tra lại.
