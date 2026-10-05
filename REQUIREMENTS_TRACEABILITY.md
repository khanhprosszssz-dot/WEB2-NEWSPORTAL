# Đối chiếu PROWEBSITE-V2.txt

Phạm vi chốt theo yêu cầu ngày 30/09/2026: hoàn tất bản chạy và demo trên máy này. Hosting công khai/IIS từ xa không nằm trong đợt bàn giao local. Các câu “có thể”, “nếu phù hợp”, “nếu còn thời gian” trong TXT là tùy chọn, không phải điều kiện bắt buộc.

Tài liệu này đối chiếu tính năng với mã nguồn và bằng chứng; không có nghĩa phần mềm được bảo đảm không còn bất kỳ lỗi nào trong mọi tình huống.

## Yêu cầu chức năng và kiến trúc

| Mục TXT | Nội dung | Mã nguồn / bằng chứng |
|---|---|---|
| I–III | ASP.NET API + MVC, HTTP/JSON, SQL Server, EF Core | Hai project net10.0; Web chỉ dùng PortalApiClient/gateway, không tham chiếu DbContext/API project |
| IV | Thực hiện theo phase, build/test và giữ mã hiện có | IMPLEMENTATION_PLAN.md; FEATURE_CHECKLIST.md; restore/build và tests local |
| V | Reader và CMS | portal.js, cms.js, community.js; các kiểm thử Chrome public và có đăng nhập |
| VI–VII | Entities, quan hệ, PK/FK, indexes, migrations | Entities.cs, Community.cs, ApplicationDbContext.cs, InitialCreate/CommunityFeatures migrations; SQL Server thật |
| VIII | DTO, không expose Identity secrets/entity | DTOs/Contracts.cs, InteractionDtos.cs; projection trong controllers/services; test user detail không có passwordHash |
| IX–XI | REST, HTTP status, Articles CRUD và các route domain | ArticlesController; Swagger; ArticleCrudWorkflowConcurrencyAndInteractions |
| XII–XIII | Paging/sort/filter/search, IQueryable/async/LINQ | ArticleService.ListAsync: filter/order/skip/take trước Select/ToListAsync; PublicPagingFilteringSortingAndValidation |
| XIV–XV | Category cha/con, tag N-N, CRUD | CategoriesController, TagsController; chống vòng lặp/xóa category còn bài; test API và UI CRUD |
| XVI | Identity, register/login/me và JWT | AuthController, AuthConfiguration; API và form đăng ký/đăng nhập thật |
| XVII | 5 vai trò, kiểm tra ở API | Authorize + ownership trong service; AuthenticationAndRolesAreEnforced; token thu hồi khi khóa/đổi vai trò |
| XVIII | Draft → PendingReview → Approved/Rejected → Published → Archived, revision | EditorialService/ScheduledPublisher; tests API và browser đi qua từ chối, gửi lại, duyệt/xuất bản |
| XIX | Scheduled publishing | BackgroundService ScheduledPublisher; ScheduledArticleBecomesPublic |
| XX | Comment/reply/edit/delete và moderation | CommentsController; API ownership/notification tests; browser gửi/sửa/xóa/duyệt/trả lời |
| XXI | Một reaction/user/article, đổi và bỏ cảm xúc | Unique index ArticleId/UserId; ReaderController; UI interaction tests |
| XXII | Bookmark/save/remove/list | ReaderController, trang /da-luu; tests API và browser |
| XXIII | Search Title/Summary/Content + các bộ lọc | ArticleService; trang /tim-kiem có số kết quả, category, sort, pagination |
| XXIV | ViewCount chống reload, popular | ReaderController giới hạn user/IP trong 30 phút; API test hai lượt xem chỉ tăng một; popular theo ViewCount |
| XXV | Upload JPG/JPEG/PNG/WebP, MIME/size/signature, media library | MediaController; gateway multipart đã sửa; test API upload không hợp lệ, test HTTP gateway và browser file input |
| XXVI–XXVII | Validation, cross-field, error handling | DataAnnotations/IValidatableObject, ApiController, ExceptionHandler; 400/404/409 và ProblemDetails |
| XXVIII | Logging, không ghi mật khẩu/token | ILogger startup/login/authorization/mutations/editorial/exception; AuditFilter |
| XXIX | Cache công khai và invalidation | IMemoryCache + ETag revalidation; PublicCacheFilter; tests 304 và invalidation sau mutation |
| XXX | CORS chính xác theo môi trường | Program.cs đọc Cors:Origins; không dùng AllowAnyOrigin + AllowCredentials |
| XXXI | Home: featured/breaking/latest/popular/choice/category | portal.js home; bổ sung nhóm tin theo 6 chuyên mục đầu và liên kết tới toàn bộ chuyên mục |
| XXXII | Category: tên, tiêu điểm, danh sách, paging | listing: category-lead chọn featured, fallback bài mới nhất; Chrome desktop/mobile |
| XXXIII | Article detail đầy đủ thành phần | detail: breadcrumb, title/subtitle, author/date, ảnh, content, tags, views, reaction, save/share, related, comments |
| XXXIV–XXXV | Search, Login/Register UI gọi API | Browser workflow + WebCookieSessionUsesCsrfAndDoesNotExposeJwt |
| XXXVI–XXXVIII | CMS, article filters/table/editor/review/settings | cms.js; 10 mục CMS, tác giả/category/status/sort/page; form SEO/preview/flags, review theo trạng thái và quyền |
| XXXIX | Dashboard từ database | AdminController.Dashboard: 7 cards + nhóm chuyên mục; Chrome xác minh 7 cards |
| XL | Users search/view/disable/role | API và CMS user detail; browser thay vai trò/khóa/mở tài khoản QA; API xác minh token cũ bị thu hồi |
| XLI | API client dùng chung, xử lý lỗi/auth | PortalApiClient + ApiProxyController + hàm api; cookie HttpOnly, CSRF, lỗi form/toast |
| XLII–XLIII | Swagger schema/method/route/status/Bearer/XML | ResponseDocumentation, ResponseContracts, XML docs; OpenApiDocumentsSuccessSchemasAndEndpointSecurity |
| XLIV | Auth, injection/XSS/upload/secrets | LINQ tham số hóa; plain-text escape; kiểm tra upload; token không lưu JS; .local bị gitignore; tests API/CSRF/role/browser |
| XLV | Config development/production | appsettings theo environment; secret qua env từ file .local; hướng dẫn production trong README |
| XLVI–XLVII | Seed tiếng Việt và 5 tài khoản vai trò | SeedData: 10 categories, 15 tags, 35 articles, 3 nhóm trạng thái; TEST_ACCOUNTS.md |
| XLVIII–L | SEO, robots/sitemap/RSS/notifications | PortalController/FeedsController/ReaderController; SEO browser test, feed/sitemap tests, ownership notification test |
| LI | Newsletter tùy chọn | Đã có subscribe/unsubscribe/admin list; browser workflow; không tích hợp gửi email tự động |
| LII | Performance | IQueryable, AsNoTracking, DTO projection, phân trang; user roles lấy theo batch thay vì N+1 |
| LIII | Tests CRUD/query/auth/RBAC/frontend | NewsPortal.Tests: 21 tests; Test-ReaderBrowser.ps1 -Full chạy Chrome thật qua HTTP |
| LIV–LV | /api/v1, tránh công nghệ thừa | Prefix v1; không GraphQL/gRPC/Redis/microservices/framework SPA |

## Phases, tài liệu và bàn giao

| Mục TXT | Đáp ứng trong bản local |
|---|---|
| LVI–LXV | Phases 1–10 | Foundation/database/CRUD/query/validation/logging/cache/Identity/RBAC/workflow đã triển khai; build sạch và API tests |
| LXVI–LXX | Phases 11–15 | Interactions, Reader, CMS, Swagger và news extensions; kiểm thử UI thật |
| LXXI | Testing/security | 21 tests HTTP/SQL; Chrome workflow; đo responsive và theo dõi JS exceptions; kiểm tra XSS text và CSRF |
| LXXII | Production configuration | Có config mẫu và hướng dẫn HTTPS/secret/IIS/Data Protection; không giả mạo môi trường production |
| LXXIII | Publish | Verify-Published.ps1 publish Release, chạy artifacts/api và artifacts/web trên localhost, test API + UI, khôi phục development |
| LXXIV–LXXVII | 7 tài liệu yêu cầu | README, IMPLEMENTATION_PLAN, FEATURE_CHECKLIST, DATABASE_DESIGN, API_DOCUMENTATION, TEST_ACCOUNTS, DEMO_SCRIPT |
| LXXVIII | Code quality | Web không DB; HttpClient tập trung; nghiệp vụ Article/Editorial ở service; async EF/DTO; không TODO/NotImplementedException trong chức năng chính |
| LXXIX–LXXX | Done/final verification | FEATURE_CHECKLIST.md và báo cáo .local/browser-qa; minh chứng cụ thể cho bản local thay vì tuyên bố đã deploy public |
| LXXXI–LXXXIII | Cách thực hiện/ưu tiên | Tiếp tục mã hiện có, sửa lỗi phát hiện từ test, restore/build/test/publish, hoàn thiện demo local theo lựa chọn người dùng |

## Chạy lại bằng chứng

```powershell
powershell -NoProfile -ExecutionPolicy Bypass -File scripts/Initialize-Development.ps1
powershell -NoProfile -ExecutionPolicy Bypass -File scripts/Start-Development.ps1
dotnet test NewsPortal.sln
powershell -NoProfile -ExecutionPolicy Bypass -File scripts/Test-ReaderBrowser.ps1 -Full
powershell -NoProfile -ExecutionPolicy Bypass -File scripts/Verify-Published.ps1
```

Các tests sử dụng database development, tạo dữ liệu QA và dọn theo định danh riêng; có phát sinh audit log và lịch sử thao tác tài khoản demo. Không chạy trên production. Bản publish được kiểm chứng bằng cấu hình Development trên localhost, không phải triển khai IIS/HTTPS production.

Các lựa chọn không bắt buộc: không dùng Serilog/Chart.js/GraphQL/gRPC, không thêm v2 giả, không triển khai forgot-password hoặc rich HTML editor. Caching dùng ETag với no-cache để luôn revalidate và tránh nội dung cũ sau thao tác; dữ liệu công khai được lưu bằng IMemoryCache. Ảnh thumbnail cũng được dùng làm ảnh đầu bài.

## Rà soát bổ sung sau yêu cầu kiểm chứng chặt chẽ

- `PublicCacheEtagsCannotRepeatAfterAnApplicationRestart`: key/ETag khác giữa hai lần khởi tạo API; test đã phát hiện lỗi cũ và pass sau khi thêm namespace cho mỗi process.
- `ArticlesRejectMissingMediaAndContentTooShortAfterTrimming`: trả 400 cho ảnh chưa upload, tiêu đề/nội dung chỉ đạt độ dài nhờ khoảng trắng; dữ liệu tạo thử trên phiên bản lỗi được dọn trước khi báo thất bại.
- `QueryOrderingPagingAndCombinedFiltersMatchReturnedArticles`: xác minh thứ tự ngày asc/desc, các trang không trùng ID, category/tag/author/date kết hợp và khoảng ngày không hợp lệ.
- `OwnershipAndInactiveCategoriesPreventPrivateContentDisclosure`: phóng viên không đọc quản trị/sửa/xóa/gửi duyệt bài người khác; bài thuộc category tắt bị loại khỏi detail/list/comments/reactions/bookmarks/RSS/sitemap.

Các điểm đã kiểm chứng được ghi cụ thể để phân biệt “đáp ứng yêu cầu local đã kiểm tra” với cam kết không còn lỗi trong mọi tình huống.

## Nghiệm thu cài mới hoàn tất

[ACCEPTANCE_REPORT.md](ACCEPTANCE_REPORT.md) ghi bằng chứng từ snapshot sạch và database mới: 21 tests pass, 8 smoke cases, 26 workflow checkpoints; CORS và mất kết nối/phục hồi API cũng pass. Bổ sung kiểm tra JWT hết hạn/sai issuer/audience/chữ ký và hai cập nhật đồng thời cùng RowVersion. Seed được chạy hai lần để xác nhận không tạo trùng dữ liệu. Phạm vi vẫn là demo local theo lựa chọn của người dùng.
