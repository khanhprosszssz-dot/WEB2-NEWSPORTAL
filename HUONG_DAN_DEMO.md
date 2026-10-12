# Hướng dẫn chạy và demo News Portal

## Điều kiện và cấu hình

Cần Windows/.NET SDK 10 và SQL Server thực sự đang chạy. Cấu hình `.\SQLEXPRESS` không hoạt động trên máy rà soát. Thay tên instance phù hợp; **không dùng database thật để chạy bộ test**. Các test tạo/sửa/xóa dữ liệu thử, thao tác tài khoản seed và có thể để lại audit/history.

Mở PowerShell tại thư mục solution sau khi giải nén source:

```powershell
# Thay YOUR_SQL_INSTANCE bằng instance thật, giữ một DB demo riêng mới.
$env:ConnectionStrings__DefaultConnection = 'Server=YOUR_SQL_INSTANCE;Database=NewsPortal_Demo_Review;Integrated Security=True;TrustServerCertificate=True'
dotnet restore NewsPortal.sln
powershell -NoProfile -ExecutionPolicy Bypass -File scripts/Initialize-Development.ps1
powershell -NoProfile -ExecutionPolicy Bypass -File scripts/Start-Development.ps1
Invoke-RestMethod http://localhost:5100/health/ready
```

Readiness phải trả 200 với status ready. Nếu 503, kiểm tra SQL service, instance, kết nối và quyền; chưa chạy bộ test dữ liệu. Không dùng `/health` 200 để bỏ qua kiểm tra DB. Giữ các lệnh trong cùng cửa sổ để biến môi trường truyền tới API và tests.

Mở [Swagger](http://localhost:5100/swagger), [Reader](http://localhost:5200/) và [CMS](http://localhost:5200/cms). API sử dụng port 5100, Web sử dụng 5200. Không đổi về port Book Management 7245 theo cuộc trò chuyện cũ.

Tài khoản seed: administrator@newsportal.local, editor@newsportal.local, journalist@newsportal.local, moderator@newsportal.local, user@newsportal.local. Mật khẩu được script sinh trong `.local/development.json`, trường Password. Chỉ xem trên máy demo; không chụp key/password/token. Seed không đặt lại mật khẩu tài khoản đã có, vì vậy nên dùng DB thử mới.

## Chạy kiểm thử

```powershell
dotnet test NewsPortal.sln --no-restore --logger 'trx;LogFileName=full-review.trx' --results-directory .local/review-results
powershell -NoProfile -ExecutionPolicy Bypass -File scripts/Test-ReaderBrowser.ps1 -Full
```

Đọc TRX và báo cáo browser, đối chiếu từng case. Kết quả hiện tại của bộ bàn giao chỉ là 2 test được lọc đã Pass; chưa có kết quả chạy đủ 21 ca. Nếu thay port phải sửa các địa chỉ hard-code trong tests/scripts tương ứng; không chạy test vào ứng dụng khác đang chiếm port.

## Kịch bản demo và tiêu chí cần quan sát

| Thứ tự | Thao tác | Kỳ vọng sau khi DB hoạt động |
| --- | --- | --- |
| 1 | Swagger; GET `/api/v1/articles?pageSize=3&sortColumn=title&sortOrder=asc` | 200; tối đa 3 bài Published, thứ tự title đúng |
| 2 | Lọc category/tag/search; so trang 1/2 | Dữ liệu đáp ứng mọi bộ lọc, không trùng ID do phân trang |
| 3 | GET với pageSize=0; từ ngày sau đến ngày | 400, thông báo validation |
| 4 | Không token POST articles; reader GET admin/users | 401; sau login reader phải 403 |
| 5 | Journalist tạo bài có title, slug duy nhất, summary, content, categoryId, tagIds | 201 và ID; public GET chưa thấy nháp |
| 6 | Sửa với RowVersion mới rồi gửi lại RowVersion cũ | 204 rồi 409 |
| 7 | POST articles/{id}/review action submit → editor approve → publish | 204 theo từng bước; sau publish public GET 200 |
| 8 | User bình luận; moderator duyệt | Bình luận chưa duyệt không công khai; sau duyệt xuất hiện |
| 9 | Reaction/bookmark/history | Lưu, đổi/bỏ đúng tài khoản; không đọc dữ liệu riêng người khác |
| 10 | Upload PNG thật, PNG giả, file >5 MB | Hợp lệ 201; không hợp lệ bị từ chối theo validation/giới hạn request |
| 11 | GET media/{id}/download bằng editor | 200, Content-Disposition attachment, tên/MIME/byte file đúng |
| 12 | Không token download; ID không tồn tại sau login | 401; 404 |
| 13 | Approve với ScheduledAt tương lai | Xuất bản khi tới lịch và qua chu kỳ xử lý; kiểm tra UTC |
| 14 | Poll/newsletter | Đăng ký/hủy; mỗi user một lựa chọn; khảo sát đóng không nhận vote |
| 15 | MVC thực hiện CRUD và multipart | Dữ liệu phản ánh API; thiếu CSRF bị 400; JWT không lộ ra JS |
| 16 | Xem audit/log/RSS/sitemap | Chỉ nội dung công khai, log không có secret |

Đây là **kịch bản dự kiến**, không phải các kết quả đã chạy thành công trong phiên 12/10/2026. Tạo ảnh và kết quả mới cho từng thao tác sau khi có DB.

## Chuẩn bị báo cáo trước khi nộp

Điền thông tin bìa, GVHD/MSSV/nhóm và tiến độ thực tế. Thay chỗ trống ảnh demo bằng ảnh chạy có dữ liệu, ghi môi trường/ngày kiểm tra. Cập nhật Chương 3 bằng kết quả mới, giữ nguyên sự phân biệt Pass/Fail/Blocked. Trong Word, cập nhật mục lục và danh mục hình bằng Update Field rồi xem lại toàn bộ trang.

Kết thúc demo:

```powershell
powershell -NoProfile -ExecutionPolicy Bypass -File scripts/Stop-Development.ps1
```

Chưa có hướng dẫn/khẳng định hệ thống đã triển khai production. Trước khi nộp theo toàn bộ bài mẫu, giải quyết Repository Pattern P4, Serilog P9 và minh chứng GitHub P1 như ma trận đánh giá.
