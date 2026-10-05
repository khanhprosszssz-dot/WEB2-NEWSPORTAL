# Feature checklist

Chốt ngày 30/09/2026 cho phạm vi **bản chạy và demo local** theo lựa chọn của người dùng. Restore/build sạch (0 warnings/errors); **21/21 tests (20 HTTP/SQL Server, 1 unit cache) pass**, **8/8 kiểm tra trang public desktop/mobile pass**, **26 điểm kiểm tra workflow trình duyệt pass**, gồm 10 mục CMS ở cả 1440 và 390 px. Bản publish Release đã chạy và được kiểm thử local. Không tuyên bố đã triển khai hosting công khai.

- [x] Phase 1: foundation + Swagger + build (0 warnings/errors)
- [x] Phase 2: SQL Server + EF migrations + seed
- [x] Phase 3: REST CRUD articles/categories/tags
- [x] Phase 4: paging/sorting/filter/search + DTO
- [x] Phase 5: validation + error handling
- [x] Phase 6: logging + audit
- [x] Phase 7: IMemoryCache + ETag revalidation/invalidation; sửa lỗi max-age=0
- [x] Phase 8: Identity + JWT login; register đã triển khai
- [x] Phase 9: RBAC 401/403, Journalist/Editor workflow
- [x] Phase 10: editorial workflow, row version; revisions đã triển khai
- [x] Phase 11: comment moderation/reactions/bookmarks/view count; history/reply đã triển khai
- [x] Phase 12: Reader, nhóm tin chuyên mục, category lead, search/detail/auth/profile/interactions; Chrome workflow và responsive pass
- [x] Phase 13: CMS/forms/upload/users/detail/dashboard/taxonomy/settings; sửa lỗi multipart gateway; workflow và 10 mục CMS desktop/mobile pass
- [x] Phase 14: Swagger JSON + Bearer theo endpoint + response schemas/status + XML summary; OpenAPI regression test pass
- [x] Phase 15: scheduled publishing test pass; RSS/SEO/robots/notifications/sitemap/newsletter/polls đã triển khai; community API tests pass
- [x] Phase 16: 21 tests tích hợp, 8 browser smoke cases, 26 workflow checkpoints; JS exceptions, Tab, XSS text, CRUD và các vai trò được kiểm tra
- [x] Phase 17: production config mẫu + hướng dẫn cấu hình secret/HTTPS
- [x] Phase 18 (local): publish Release API/Web; chạy executable trong artifacts, 19 tests + browser workflow pass; khôi phục development

Browser QA dùng Chrome DevTools qua scripts/Test-ReaderBrowser.ps1 -Full; ảnh, report.json và workflow-report.json trong .local/browser-qa (không commit). Các kiểm tra là bằng chứng trong những tình huống đã chạy, không phải bảo đảm không còn lỗi trong mọi trình duyệt/tình huống. Đã xem ảnh dashboard desktop, bảng bài/người dùng/media mobile và các trang Reader.
Tests ở NewsPortal.Tests/PortalTests.cs và ExtendedTests.cs chạy trên HTTP và SQL Server development thật, dọn dữ liệu kiểm thử; vẫn phát sinh audit logs.
Newsletter lưu đăng ký, chưa gửi email tự động. Nội dung bài là plain text; chưa có rich HTML editor. Phân trang đã xử lý lỗi callback và khóa nút trong lúc tải.

Kiểm tra cuối database: 2 migrations, 10 categories, 15 tags, 35 articles, 5 roles, 0 tài khoản browser QA còn lại. API /health, Swagger JSON và website trả 200.

Chi tiết đối chiếu từng mục TXT: [REQUIREMENTS_TRACEABILITY.md](REQUIREMENTS_TRACEABILITY.md). Hosting/IIS/HTTPS production nằm ngoài phạm vi demo local đã chọn.

Rà soát bổ sung: đã tái hiện và sửa lỗi ETag trùng sau restart và chấp nhận thumbnail chưa tồn tại; bổ sung validation sau trim. Các test mới xác minh sắp xếp thực tế, trang không trùng dữ liệu, bộ lọc kết hợp, quyền sở hữu bài và ẩn nội dung theo trạng thái chuyên mục khỏi API/RSS/sitemap. Kết quả: 19/19 tests pass.

Nghiệm thu cài mới sau đó: **21/21 tests (20 HTTP/SQL, 1 unit cache)** pass trên source/database/packages/artifacts mới; **8 smoke cases, 26 workflow checkpoints, 3 kiểm tra CORS/phục hồi** pass. Bổ sung concurrent PUT và JWT expiry/issuer/audience/signature. Seed chạy hai lần không tạo trùng. Xem [ACCEPTANCE_REPORT.md](ACCEPTANCE_REPORT.md) và chứng cứ TRX/JSON/SHA-256.
