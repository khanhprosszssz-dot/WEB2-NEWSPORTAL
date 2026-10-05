# Nghiệm thu bản demo local — 30/09/2026

**Kết quả: đạt các tiêu chí nghiệm thu local bên dưới.** Phạm vi do người dùng chọn là chạy/demo trên máy này; không bao gồm triển khai IIS/hosting công khai hoặc chứng nhận hoạt động trên mọi hệ điều hành/trình duyệt.

Nguồn yêu cầu: PROWEBSITE-V2.txt. Đối chiếu chức năng/kiến trúc và các mục bắt buộc, tùy chọn: [REQUIREMENTS_TRACEABILITY.md](REQUIREMENTS_TRACEABILITY.md).

## Bằng chứng cài mới

Run ID: `67220437046f4d7eba52b5af8cb9a02d`.

- Copy mã nguồn và thư viện web đi kèm, loại bỏ bin/obj, uploads, artifacts và thông tin đăng nhập local.
- Restore vào thư mục NuGet mới, build với .NET SDK 10.0.401: 0 warning, 0 error.
- Database SQL Server riêng: `NewsPortal_Acceptance_67220437046f4d7eba52b5af8cb9a02d`.
- Migration từ database mới: 2 migrations. Seed chạy hai lần vẫn có đúng 10 categories, 15 tags, 35 articles, 5 roles và 5 tài khoản.
- Sinh thông tin đăng nhập development mới; không dùng mật khẩu/JWT secret của demo hiện tại.
- Publish Release vào thư mục artifacts mới, chạy executable từ bản publish và kiểm thử.
- 140 file nguồn trong snapshot được đối chiếu SHA-256 với workspace; connection string là thay đổi có chủ đích để dùng database riêng.
- Giữ database và snapshot nghiệm thu để kiểm tra lại. Các tiến trình nghiệm thu đã dừng; demo ban đầu được khôi phục.

Đây là **cài mới ứng dụng/database trên máy hiện có SDK, SQL Server và Chrome**, không phải cài lại Windows hoặc thử trên một máy vật lý khác.

## Các tiêu chí và kết quả

| Tiêu chí | Kết quả | Bằng chứng |
|---|---|---|
| Mã nguồn tự restore/build, không phụ thuộc bin/obj cũ | PASS | Clean-install result.json, nguồn và thư mục packages mới |
| Database tạo bằng migration, seed đủ và không nhân đôi | PASS | Seed counters sau hai lần initialize |
| Web tách biệt API qua HTTP, không dùng EF/DbContext | PASS | Rà soát .cs/.csproj của Web; HTTP gateway tests |
| REST CRUD, DTO, paging/sort/filter/search/validation | PASS | Tests API, query kết hợp và browser CRUD |
| Identity/JWT và 5 vai trò, quyền sở hữu dữ liệu | PASS | Register/login, token revoked, ownership tests |
| Token hết hạn/sai issuer/sai audience/sai chữ ký bị từ chối | PASS | ExpiredWrongIssuerAudienceAndSignatureTokensAreRejected |
| Sửa đồng thời không ghi đè nội dung | PASS | Hai PUT cùng RowVersion: một 204, một 409; chỉ một revision được lưu |
| Quy trình biên tập, lịch xuất bản và lịch sử sửa | PASS | API scheduled test; browser draft/reject/resubmit/approve/publish/archive |
| Comment/reply/moderation, reaction, bookmark/history | PASS | Tests API và các thao tác trình duyệt |
| Upload file từ CMS, kiểm tra MIME/size/signature và ảnh tham chiếu | PASS | HTTP gateway regression, browser file input; từ chối media chưa tồn tại |
| Caching/ETag/invalidation và restart | PASS | Tests revalidation/invalidation và cache namespace theo process |
| Reader/CMS gọi API thật, desktop/mobile | PASS | 8 smoke cases; 26 workflow checkpoints gồm 10 mục CMS ở hai viewport |
| XSS text, CSRF, không lộ token cho JavaScript | PASS | Text escaping browser test; cookie/CSRF tests |
| Swagger status/schema/Bearer, XML docs | PASS | OpenAPI regression test và source review |
| SEO/RSS/sitemap/notifications/community | PASS | API, HTML metadata và browser tests; bài ẩn bị loại khỏi feeds |
| CORS chỉ cho origin được cấu hình | PASS | Preflight cho origin đúng và origin không được phép trên API riêng |
| API dừng: Reader hiển thị lỗi và không lộ secret | PASS | Chrome DOM khi API dừng trên cổng nghiệm thu |
| API chạy lại: Reader phục hồi dữ liệu | PASS | Chrome DOM sau restart có hero-grid và không còn error state |
| Bản publish hoạt động từ thư mục cài mới | PASS | Toàn bộ tests chạy trên executable Release của snapshot |
| Tài liệu và lệnh chạy lại có sẵn | PASS | README, API/DATABASE/DEMO/TEST_ACCOUNTS/PLAN/CHECKLIST/TRACEABILITY |

Kết quả máy đọc được: **21 tests = 20 HTTP/SQL Server + 1 unit cache; 21 passed, 0 failed, 0 skipped.** Thêm **8 smoke cases**, **26 workflow checkpoints** và **3 kiểm tra CORS/mất kết nối/phục hồi** đều pass. Các nhóm này có mục đích khác nhau, không cộng lại thành phần trăm bao phủ mã nguồn.

## Tệp chứng cứ

Các tệp sau nằm trong `.local` và không đưa vào Git vì snapshot có cấu hình development riêng:

- [Kết quả cài sạch](.local/acceptance/67220437046f4d7eba52b5af8cb9a02d/result.json)
- [Kết quả tests TRX](.local/acceptance/67220437046f4d7eba52b5af8cb9a02d/source/.local/test-results/published.trx)
- [Báo cáo desktop/mobile](.local/acceptance/67220437046f4d7eba52b5af8cb9a02d/source/.local/browser-qa/report.json)
- [Báo cáo workflow](.local/acceptance/67220437046f4d7eba52b5af8cb9a02d/source/.local/browser-qa/workflow-report.json)
- [Manifest SHA-256](.local/acceptance/67220437046f4d7eba52b5af8cb9a02d/source-manifest.json)
- [Kết quả CORS/phục hồi](.local/acceptance/67220437046f4d7eba52b5af8cb9a02d/recovery/result.json)

HTML trước/sau phục hồi nằm cùng thư mục recovery. Ảnh chụp các trang nằm cùng thư mục browser-qa.

## Chạy lại

```powershell
powershell -NoProfile -ExecutionPolicy Bypass -File scripts/Verify-CleanInstall.ps1
```

Script in đường dẫn source và database riêng của lần chạy mới. Dùng đường dẫn đó để kiểm tra phục hồi:

```powershell
powershell -NoProfile -ExecutionPolicy Bypass -File scripts/Test-ServiceRecovery.ps1 -SourceRoot '<source của lần nghiệm thu>'
```

Chỉ chạy một bộ nghiệm thu tại một thời điểm. Bộ cài sạch dùng cổng 5100/5200 trong lúc kiểm thử rồi phục hồi demo; bộ recovery dùng 5300/5400. Các kiểm tra không xóa database demo hiện tại. Database nghiệm thu được giữ lại có chủ đích.

Không dùng kết quả này để tuyên bố “phần mềm tuyệt đối không còn lỗi”. Kết luận áp dụng cho yêu cầu/phạm vi và môi trường đã ghi rõ ở trên; các thay đổi tiếp theo cần chạy lại kiểm thử liên quan.
